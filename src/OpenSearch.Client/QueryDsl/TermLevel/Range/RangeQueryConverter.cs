/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IRangeQuery"/> that handles
	/// the field-name-wrapping pattern and polymorphic dispatch to the correct range
	/// query subtype (Date, Numeric, Long, Term).
	/// <para>
	/// The OpenSearch range query JSON shape is:
	/// <code>
	/// { "field_name": { "gte": ..., "lte": ..., "format": "...", ... } }
	/// </code>
	/// The presence of <c>format</c> or <c>time_zone</c> indicates a date range;
	/// a decimal number indicates numeric; an integer indicates long; otherwise term range.
	/// </para>
	/// </summary>
	internal sealed class RangeQueryConverter : JsonConverter<IRangeQuery>
	{
		private readonly IConnectionSettingsValues _settings;

		public RangeQueryConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IRangeQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			// We need to buffer the entire value to inspect it and determine the concrete type
			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			string fieldName = null;
			JsonElement fieldBody = default;

			// The range query is { "field_name": { range params } }
			foreach (var property in root.EnumerateObject())
			{
				fieldName = property.Name;
				fieldBody = property.Value;
				break; // Only one property expected (the field name)
			}

			if (fieldName == null || fieldBody.ValueKind != JsonValueKind.Object)
				return null;

			// Determine the range type by inspecting the field body
			var isDate = false;
			var isDouble = false;
			var isLong = false;

			foreach (var prop in fieldBody.EnumerateObject())
			{
				switch (prop.Name)
				{
					case "format":
					case "time_zone":
						isDate = true;
						break;
					case "gt":
					case "gte":
					case "lt":
					case "lte":
						if (!isDate)
						{
							switch (prop.Value.ValueKind)
							{
								case JsonValueKind.String:
									var strValue = prop.Value.GetString();
									if (IsDateValue(strValue))
										isDate = true;
									break;
								case JsonValueKind.Number:
									if (!isDouble)
									{
										if (prop.Value.TryGetInt64(out _))
											isLong = true;
										else
											isDouble = true;
									}
									break;
							}
						}
						break;
				}

				if (isDate || isDouble)
					break;
			}

			// Deserialize the field body into the appropriate range query type
			var fieldBodyJson = fieldBody.GetRawText();
			IRangeQuery result;

			if (isDate)
				result = JsonSerializer.Deserialize<DateRangeQuery>(fieldBodyJson, options);
			else if (isDouble)
				result = JsonSerializer.Deserialize<NumericRangeQuery>(fieldBodyJson, options);
			else if (isLong)
				result = JsonSerializer.Deserialize<LongRangeQuery>(fieldBodyJson, options);
			else
				result = JsonSerializer.Deserialize<TermRangeQuery>(fieldBodyJson, options);

			if (result is FieldNameQueryBase fnq)
				fnq.Field = fieldName;

			return result;
		}

		public override void Write(Utf8JsonWriter writer, IRangeQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// The range query is wrapped in a field name object:
			// { "field_name": { "gte": ..., "lte": ..., ... } }
			var field = (value as IFieldNameQuery)?.Field;
			var fieldName = field == null ? null : _settings.Inferrer.Field(field);
			if (string.IsNullOrEmpty(fieldName))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(fieldName);

			// Write the inner range parameters based on the concrete type
			switch (value)
			{
				case IDateRangeQuery dateRange:
					WriteRangeBody(writer, dateRange, options);
					break;
				case INumericRangeQuery numericRange:
					WriteRangeBody(writer, numericRange, options);
					break;
				case ILongRangeQuery longRange:
					WriteRangeBody(writer, longRange, options);
					break;
				case ITermRangeQuery termRange:
					WriteRangeBody(writer, termRange, options);
					break;
				default:
					writer.WriteStartObject();
					writer.WriteEndObject();
					break;
			}

			writer.WriteEndObject();
		}

		private static void WriteRangeBody(Utf8JsonWriter writer, IDateRangeQuery value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			WriteQueryMeta(writer, value, options);

			if (value.GreaterThan != null)
			{
				writer.WritePropertyName("gt");
				JsonSerializer.Serialize(writer, value.GreaterThan, options);
			}
			if (value.GreaterThanOrEqualTo != null)
			{
				writer.WritePropertyName("gte");
				JsonSerializer.Serialize(writer, value.GreaterThanOrEqualTo, options);
			}
			if (value.LessThan != null)
			{
				writer.WritePropertyName("lt");
				JsonSerializer.Serialize(writer, value.LessThan, options);
			}
			if (value.LessThanOrEqualTo != null)
			{
				writer.WritePropertyName("lte");
				JsonSerializer.Serialize(writer, value.LessThanOrEqualTo, options);
			}
			if (!string.IsNullOrEmpty(value.Format))
			{
				writer.WritePropertyName("format");
				writer.WriteStringValue(value.Format);
			}
			if (!string.IsNullOrEmpty(value.TimeZone))
			{
				writer.WritePropertyName("time_zone");
				writer.WriteStringValue(value.TimeZone);
			}
			if (value.Relation.HasValue)
			{
				writer.WritePropertyName("relation");
				JsonSerializer.Serialize(writer, value.Relation.Value, options);
			}

			writer.WriteEndObject();
		}

		private static void WriteRangeBody(Utf8JsonWriter writer, INumericRangeQuery value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			WriteQueryMeta(writer, value, options);

			if (value.GreaterThan.HasValue)
			{
				writer.WritePropertyName("gt");
				JsonSerializer.Serialize(writer, value.GreaterThan.Value, options);
			}
			if (value.GreaterThanOrEqualTo.HasValue)
			{
				writer.WritePropertyName("gte");
				JsonSerializer.Serialize(writer, value.GreaterThanOrEqualTo.Value, options);
			}
			if (value.LessThan.HasValue)
			{
				writer.WritePropertyName("lt");
				JsonSerializer.Serialize(writer, value.LessThan.Value, options);
			}
			if (value.LessThanOrEqualTo.HasValue)
			{
				writer.WritePropertyName("lte");
				JsonSerializer.Serialize(writer, value.LessThanOrEqualTo.Value, options);
			}
			if (value.Relation.HasValue)
			{
				writer.WritePropertyName("relation");
				JsonSerializer.Serialize(writer, value.Relation.Value, options);
			}

			writer.WriteEndObject();
		}

		private static void WriteRangeBody(Utf8JsonWriter writer, ILongRangeQuery value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			WriteQueryMeta(writer, value, options);

			if (value.GreaterThan.HasValue)
			{
				writer.WritePropertyName("gt");
				writer.WriteNumberValue(value.GreaterThan.Value);
			}
			if (value.GreaterThanOrEqualTo.HasValue)
			{
				writer.WritePropertyName("gte");
				writer.WriteNumberValue(value.GreaterThanOrEqualTo.Value);
			}
			if (value.LessThan.HasValue)
			{
				writer.WritePropertyName("lt");
				writer.WriteNumberValue(value.LessThan.Value);
			}
			if (value.LessThanOrEqualTo.HasValue)
			{
				writer.WritePropertyName("lte");
				writer.WriteNumberValue(value.LessThanOrEqualTo.Value);
			}
			if (value.Relation.HasValue)
			{
				writer.WritePropertyName("relation");
				JsonSerializer.Serialize(writer, value.Relation.Value, options);
			}

			writer.WriteEndObject();
		}

		private static void WriteRangeBody(Utf8JsonWriter writer, ITermRangeQuery value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			WriteQueryMeta(writer, value, options);

			if (!string.IsNullOrEmpty(value.GreaterThan))
			{
				writer.WritePropertyName("gt");
				writer.WriteStringValue(value.GreaterThan);
			}
			if (!string.IsNullOrEmpty(value.GreaterThanOrEqualTo))
			{
				writer.WritePropertyName("gte");
				writer.WriteStringValue(value.GreaterThanOrEqualTo);
			}
			if (!string.IsNullOrEmpty(value.LessThan))
			{
				writer.WritePropertyName("lt");
				writer.WriteStringValue(value.LessThan);
			}
			if (!string.IsNullOrEmpty(value.LessThanOrEqualTo))
			{
				writer.WritePropertyName("lte");
				writer.WriteStringValue(value.LessThanOrEqualTo);
			}

			writer.WriteEndObject();
		}

		private static void WriteQueryMeta(Utf8JsonWriter writer, IQuery value, JsonSerializerOptions options)
		{
			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName("_name");
				writer.WriteStringValue(value.Name);
			}
			if (value.Boost.HasValue)
			{
				writer.WritePropertyName("boost");
				JsonSerializer.Serialize(writer, value.Boost.Value, options);
			}
		}

		private static bool IsDateValue(string value)
		{
			if (string.IsNullOrEmpty(value))
				return false;

			// Check for date math separators (||) or common date patterns
			if (value.Contains("||"))
				return true;

			// Check for ISO date format indicators (contains T or dashes in date-like pattern)
			if (value.Length >= 10 && value[4] == '-' && value[7] == '-')
				return true;

			// Check if it contains "now" (date math)
			if (value.StartsWith("now", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}
	}
}
