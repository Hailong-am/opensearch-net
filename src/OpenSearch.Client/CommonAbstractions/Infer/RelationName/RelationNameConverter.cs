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
	internal sealed class RelationNameConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public RelationNameConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(RelationName);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new RelationNameConverter(_settings);
	}

	internal sealed class RelationNameConverter : JsonConverter<RelationName>
	{
		private readonly IConnectionSettingsValues _settings;

		public RelationNameConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override RelationName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var name = reader.GetString();
			return name == null ? null : (RelationName)name;
		}

		public override void Write(Utf8JsonWriter writer, RelationName value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var relationName = _settings.Inferrer.RelationName(value);
			writer.WriteStringValue(relationName);
		}

		public override RelationName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var name = reader.GetString();
			return name == null ? null : (RelationName)name;
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, RelationName value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			var relationName = _settings.Inferrer.RelationName(value);
			writer.WritePropertyName(relationName ?? string.Empty);
		}
	}
}
