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
	internal sealed class FieldConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public FieldConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Field);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new FieldConverter(_settings);
	}

	internal sealed class FieldConverter : JsonConverter<Field>
	{
		private readonly IConnectionSettingsValues _settings;

		public FieldConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override Field Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType == JsonTokenType.String)
			{
				var name = reader.GetString();
				return name == null ? null : new Field(name);
			}

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				string fieldName = null;
				double? boost = null;
				string format = null;

				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					if (reader.TokenType != JsonTokenType.PropertyName)
						continue;

					var propertyName = reader.GetString();
					reader.Read();

					switch (propertyName)
					{
						case "field":
							fieldName = reader.GetString();
							break;
						case "boost":
							boost = reader.GetDouble();
							break;
						case "format":
							format = reader.GetString();
							break;
						default:
							reader.Skip();
							break;
					}
				}

				if (fieldName == null)
					return null;

				return new Field(fieldName, boost, format);
			}

			throw new JsonException($"Unexpected token {reader.TokenType} when deserializing {nameof(Field)}");
		}

		public override void Write(Utf8JsonWriter writer, Field value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var fieldName = _settings.Inferrer.Field(value);

			if (value.Boost.HasValue || !string.IsNullOrEmpty(value.Format))
			{
				writer.WriteStartObject();
				writer.WriteString("field", fieldName);

				if (value.Boost.HasValue)
					writer.WriteNumber("boost", value.Boost.Value);

				if (!string.IsNullOrEmpty(value.Format))
					writer.WriteString("format", value.Format);

				writer.WriteEndObject();
			}
			else
			{
				writer.WriteStringValue(fieldName);
			}
		}

		public override Field ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var name = reader.GetString();
			return name == null ? null : new Field(name);
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, Field value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			var fieldName = _settings.Inferrer.Field(value);
			writer.WritePropertyName(fieldName ?? string.Empty);
		}
	}
}
