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
	internal sealed class IndexNameConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IndexNameConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(IndexName);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new IndexNameConverter(_settings);
	}

	internal sealed class IndexNameConverter : JsonConverter<IndexName>
	{
		private readonly IConnectionSettingsValues _settings;

		public IndexNameConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IndexName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var name = reader.GetString();
			return name == null ? null : (IndexName)name;
		}

		public override void Write(Utf8JsonWriter writer, IndexName value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var indexName = _settings.Inferrer.IndexName(value);
			writer.WriteStringValue(indexName);
		}

		public override IndexName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var name = reader.GetString();
			return name == null ? null : (IndexName)name;
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, IndexName value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			var indexName = _settings.Inferrer.IndexName(value);
			writer.WritePropertyName(indexName ?? string.Empty);
		}
	}
}
