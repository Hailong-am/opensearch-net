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
	/// Serializes an <see cref="IDictionary{TKey,TValue}"/> keyed by <see cref="Field"/>, resolving
	/// the JSON property name for each key through the settings-aware <see cref="Field"/> converter.
	/// <para>
	/// System.Text.Json skips options-registered converter factories when resolving dictionary keys at
	/// depth, so a plain <c>IDictionary&lt;Field, TValue&gt;</c> property would otherwise fall back to
	/// the default object converter and throw. This converter is pinned onto such properties by
	/// <see cref="InterfaceDataContractModifier"/>.
	/// </para>
	/// </summary>
	internal sealed class FieldKeyedDictionaryConverter<TValue> : JsonConverter<IDictionary<Field, TValue>>
	{
		public override IDictionary<Field, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var result = new Dictionary<Field, TValue>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var key = new Field(reader.GetString());
				reader.Read();
				var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
				result[key] = value;
			}

			return result;
		}

		public override void Write(Utf8JsonWriter writer, IDictionary<Field, TValue> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var keyConverter = (JsonConverter<Field>)options.GetConverter(typeof(Field));

			writer.WriteStartObject();
			foreach (var kvp in value)
			{
				keyConverter.WriteAsPropertyName(writer, kvp.Key, options);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}
