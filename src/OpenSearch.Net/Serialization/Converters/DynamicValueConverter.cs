/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="DynamicValue"/> that handles all primitive
	/// JSON types (string, number, bool, null) as well as nested objects and arrays.
	/// </summary>
	internal class DynamicValueConverter : JsonConverter<DynamicValue>
	{
		public override DynamicValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = ReadValue(ref reader, options);
			return new DynamicValue(value);
		}

		public override void Write(Utf8JsonWriter writer, DynamicValue value, JsonSerializerOptions options)
		{
			if (value == null || !value.HasValue)
			{
				writer.WriteNullValue();
				return;
			}

			WriteValue(writer, value.Value, options);
		}

		internal static object ReadValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;

				case JsonTokenType.True:
					return true;

				case JsonTokenType.False:
					return false;

				case JsonTokenType.String:
					return reader.GetString();

				case JsonTokenType.Number:
					// Try long first, then double for floating-point values
					if (reader.TryGetInt64(out var longValue))
						return longValue;
					return reader.GetDouble();

				case JsonTokenType.StartObject:
					return ReadObject(ref reader, options);

				case JsonTokenType.StartArray:
					return ReadArray(ref reader, options);

				default:
					throw new JsonException($"Unexpected token type: {reader.TokenType}");
			}
		}

		private static Dictionary<string, object> ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					return dict;

				if (reader.TokenType != JsonTokenType.PropertyName)
					throw new JsonException("Expected property name");

				var key = reader.GetString();
				reader.Read();
				dict[key] = ReadValue(ref reader, options);
			}

			throw new JsonException("Unexpected end of JSON while reading object");
		}

		private static List<object> ReadArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var list = new List<object>();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndArray)
					return list;

				list.Add(ReadValue(ref reader, options));
			}

			throw new JsonException("Unexpected end of JSON while reading array");
		}

		internal static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value)
			{
				case DynamicValue dv:
					if (!dv.HasValue)
						writer.WriteNullValue();
					else
						WriteValue(writer, dv.Value, options);
					break;

				case DynamicDictionary dd:
					JsonSerializer.Serialize(writer, dd, options);
					break;

				case string s:
					writer.WriteStringValue(s);
					break;

				case bool b:
					writer.WriteBooleanValue(b);
					break;

				case int i:
					writer.WriteNumberValue(i);
					break;

				case long l:
					writer.WriteNumberValue(l);
					break;

				case float f:
					if (!float.IsNaN(f) && !float.IsInfinity(f)
						&& Math.Floor((double)f) == f && Math.Abs(f) < 1e7)
						writer.WriteRawValue(f.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
					else
						writer.WriteNumberValue(f);
					break;

				case double d:
					if (!double.IsNaN(d) && !double.IsInfinity(d)
						&& Math.Floor(d) == d && Math.Abs(d) < 1e16)
						writer.WriteRawValue(d.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
					else
						writer.WriteNumberValue(d);
					break;

				case decimal dec:
					if (decimal.Truncate(dec) == dec)
						writer.WriteRawValue(dec.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
					else
						writer.WriteNumberValue(dec);
					break;

				case short sh:
					writer.WriteNumberValue(sh);
					break;

				case byte by:
					writer.WriteNumberValue(by);
					break;

				case uint ui:
					writer.WriteNumberValue(ui);
					break;

				case ulong ul:
					writer.WriteNumberValue(ul);
					break;

				case DateTime dt:
					writer.WriteStringValue(dt);
					break;

				case DateTimeOffset dto:
					writer.WriteStringValue(dto);
					break;

				case IDictionary<string, object> dict:
					writer.WriteStartObject();
					foreach (var kvp in dict)
					{
						writer.WritePropertyName(kvp.Key);
						WriteValue(writer, kvp.Value, options);
					}
					writer.WriteEndObject();
					break;

				case IDictionary<string, DynamicValue> dvDict:
					writer.WriteStartObject();
					foreach (var kvp in dvDict)
					{
						writer.WritePropertyName(kvp.Key);
						WriteValue(writer, kvp.Value?.Value, options);
					}
					writer.WriteEndObject();
					break;

				case IList<object> list:
					writer.WriteStartArray();
					foreach (var item in list)
						WriteValue(writer, item, options);
					writer.WriteEndArray();
					break;

				default:
					// Fallback: use STJ default serialization for unknown types
					JsonSerializer.Serialize(writer, value, value.GetType(), options);
					break;
			}
		}
	}
}
