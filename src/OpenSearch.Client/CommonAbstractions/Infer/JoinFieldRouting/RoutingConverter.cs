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
	internal sealed class RoutingConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public RoutingConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Routing);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new RoutingConverter(_settings);
	}

	internal sealed class RoutingConverter : JsonConverter<Routing>
	{
		private readonly IConnectionSettingsValues _settings;

		public RoutingConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override Routing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;
			if (reader.TokenType == JsonTokenType.Number)
				return new Routing(reader.GetInt64());
			return new Routing(reader.GetString());
		}

		public override void Write(Utf8JsonWriter writer, Routing value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.Document != null)
			{
				var documentId = _settings.Inferrer.Routing(value.Document.GetType(), value.Document);
				writer.WriteStringValue(documentId);
			}
			else if (value.DocumentGetter != null)
			{
				var doc = value.DocumentGetter();
				var documentId = _settings.Inferrer.Routing(doc.GetType(), doc);
				writer.WriteStringValue(documentId);
			}
			else if (value.LongValue != null)
				writer.WriteNumberValue(value.LongValue.Value);
			else
				writer.WriteStringValue(value.StringValue);
		}
	}
}
