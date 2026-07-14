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
	/// A <see cref="JsonConverter{T}"/> for <see cref="DynamicDictionary"/> that provides
	/// round-trip serialization support. Replaces the Utf8Json-based <c>DynamicDictionaryFormatter</c>.
	/// </summary>
	internal class DynamicDictionaryConverter : JsonConverter<DynamicDictionary>
	{
		public override DynamicDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// Handle JSON array by indexing elements with their position
			if (reader.TokenType == JsonTokenType.StartArray)
			{
				var arrayDict = new Dictionary<string, object>();
				var index = 0;

				while (reader.Read())
				{
					if (reader.TokenType == JsonTokenType.EndArray)
						break;

					var value = DynamicValueConverter.ReadValue(ref reader, options);
					arrayDict[index.ToString(CultureInfo.InvariantCulture)] = new DynamicValue(value);
					index++;
				}

				return DynamicDictionary.Create(arrayDict);
			}

			// Handle JSON object
			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Unexpected token type {reader.TokenType} when deserializing DynamicDictionary");

			var dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					throw new JsonException("Expected property name");

				var key = reader.GetString();
				reader.Read();
				var val = DynamicValueConverter.ReadValue(ref reader, options);
				dictionary[key] = val;
			}

			return DynamicDictionary.Create(dictionary);
		}

		public override void Write(Utf8JsonWriter writer, DynamicDictionary value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			foreach (var kvp in (IDictionary<string, DynamicValue>)value)
			{
				writer.WritePropertyName(kvp.Key);
				DynamicValueConverter.WriteValue(writer, kvp.Value?.Value, options);
			}

			writer.WriteEndObject();
		}
	}
}
