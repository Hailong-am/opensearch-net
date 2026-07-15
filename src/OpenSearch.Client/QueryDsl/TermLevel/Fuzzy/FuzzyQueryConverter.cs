/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IFuzzyQuery"/> that handles the
	/// field-name-wrapping pattern and polymorphic dispatch to the correct fuzzy query
	/// subtype (string, numeric, date) based on the JSON shape of the <c>value</c> property.
	/// <para>
	/// The OpenSearch fuzzy query JSON shape is:
	/// <code>{ "field_name": { "value": ..., "fuzziness": ..., ... } }</code>
	/// A numeric <c>value</c> indicates <see cref="FuzzyNumericQuery"/>; a date-like string
	/// value indicates <see cref="FuzzyDateQuery"/>; any other string indicates <see cref="FuzzyQuery"/>.
	/// </para>
	/// <para>
	/// This is the System.Text.Json equivalent of the Utf8Json <c>FuzzyQueryFormatter</c>.
	/// </para>
	/// </summary>
	internal sealed class FuzzyQueryConverter : JsonConverter<IFuzzyQuery>
	{
		private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> BodyOptionsCache = new();

		private readonly IConnectionSettingsValues _settings;

		public FuzzyQueryConverter(IConnectionSettingsValues settings) => _settings = settings;

		private static JsonSerializerOptions GetBodyOptions(JsonSerializerOptions options) =>
			BodyOptionsCache.GetValue(options, static o =>
			{
				var clone = new JsonSerializerOptions(o);
				for (var i = clone.Converters.Count - 1; i >= 0; i--)
				{
					if (clone.Converters[i] is FuzzyQueryConverter)
						clone.Converters.RemoveAt(i);
				}
				return clone;
			});

		public override IFuzzyQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			reader.Read(); // move to first token inside the outer object

			if (reader.TokenType == JsonTokenType.EndObject)
				return null;

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				reader.Skip();
				return null;
			}

			var fieldName = reader.GetString();
			reader.Read(); // move to the body value

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				reader.Read(); // consume outer EndObject
				return null;
			}

			// Buffer the body so we can inspect the "value" token to determine the concrete type.
			using var doc = JsonDocument.ParseValue(ref reader);
			var body = doc.RootElement;

			IFuzzyQuery query;
			if (body.TryGetProperty("value", out var valueElement))
			{
				switch (valueElement.ValueKind)
				{
					case JsonValueKind.Number:
						query = JsonSerializer.Deserialize<FuzzyNumericQuery>(body.GetRawText(), GetBodyOptions(options));
						break;
					case JsonValueKind.String when IsDateValue(valueElement.GetString()):
						query = JsonSerializer.Deserialize<FuzzyDateQuery>(body.GetRawText(), GetBodyOptions(options));
						break;
					default:
						query = JsonSerializer.Deserialize<FuzzyQuery>(body.GetRawText(), GetBodyOptions(options));
						break;
				}
			}
			else
			{
				query = JsonSerializer.Deserialize<FuzzyQuery>(body.GetRawText(), GetBodyOptions(options));
			}

			reader.Read(); // consume outer EndObject

			if (query is FieldNameQueryBase fnq)
				fnq.Field = fieldName;

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IFuzzyQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var field = (value as IFieldNameQuery)?.Field;
			var resolvedField = field == null ? null : _settings.Inferrer.Field(field);
			if (string.IsNullOrEmpty(resolvedField))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(resolvedField);

			// Serialize the body using the actual runtime type so all properties (including the
			// concrete Value/Fuzziness) are emitted, using the body options that exclude this
			// converter (avoids recursion).
			JsonSerializer.Serialize(writer, value, value.GetType(), GetBodyOptions(options));

			writer.WriteEndObject();
		}

		private static bool IsDateValue(string value)
		{
			if (string.IsNullOrEmpty(value))
				return false;

			if (value.Contains("||"))
				return true;

			if (value.Length >= 10 && value[4] == '-' && value[7] == '-')
				return true;

			if (value.StartsWith("now", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}
	}
}
