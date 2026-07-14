/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes an <c>indices_boost</c> dictionary (<see cref="IDictionary{IndexName, Double}"/>)
	/// as an ordered JSON array of single-key objects (e.g. <c>[{"project":1.4},{"devs":1.3}]</c>)
	/// and deserializes either that array form or a plain object form. Replaces the Utf8Json
	/// <c>IndicesBoostFormatter</c>.
	/// </summary>
	internal sealed class IndicesBoostConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesBoostConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert == typeof(IDictionary<IndexName, double>) ||
			typeToConvert == typeof(Dictionary<IndexName, double>);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new IndicesBoostConverter(_settings);
	}

	internal sealed class IndicesBoostConverter : JsonConverter<IDictionary<IndexName, double>>
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesBoostConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IDictionary<IndexName, double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.StartObject:
				{
					var dictionary = new Dictionary<IndexName, double>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
					{
						if (reader.TokenType != JsonTokenType.PropertyName)
							continue;

						var indexName = (IndexName)reader.GetString();
						reader.Read();
						dictionary[indexName] = reader.GetDouble();
					}
					return dictionary;
				}
				case JsonTokenType.StartArray:
				{
					var dictionary = new Dictionary<IndexName, double>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						if (reader.TokenType != JsonTokenType.StartObject)
							continue;

						while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
						{
							if (reader.TokenType != JsonTokenType.PropertyName)
								continue;

							var indexName = (IndexName)reader.GetString();
							reader.Read();
							dictionary[indexName] = reader.GetDouble();
						}
					}
					return dictionary;
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, IDictionary<IndexName, double> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();
			foreach (var entry in value)
			{
				writer.WriteStartObject();
				var indexName = _settings.Inferrer.IndexName(entry.Key);
				writer.WriteNumber(indexName, entry.Value);
				writer.WriteEndObject();
			}
			writer.WriteEndArray();
		}
	}
}
