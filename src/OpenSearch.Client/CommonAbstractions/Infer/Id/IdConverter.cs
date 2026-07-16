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
	internal sealed class IdConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IdConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Id);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new IdConverter(_settings);
	}

	internal sealed class IdConverter : JsonConverter<Id>
	{
		private readonly IConnectionSettingsValues _settings;

		public IdConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;
			if (reader.TokenType == JsonTokenType.Number)
				return new Id(reader.GetInt64());
			return new Id(reader.GetString());
		}

		public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.Document != null)
			{
				var documentId = _settings.Inferrer.Id(value.Document.GetType(), value.Document);
				writer.WriteStringValue(documentId);
			}
			else if (value.LongValue != null)
				writer.WriteNumberValue(value.LongValue.Value);
			else
				writer.WriteStringValue(value.StringValue);
		}
	}
}
