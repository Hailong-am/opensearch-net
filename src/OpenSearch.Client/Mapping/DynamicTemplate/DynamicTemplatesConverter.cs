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
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IDynamicTemplateContainer"/> that serializes
	/// the container as a JSON array of single-key objects (<c>[ { "name": { ...template... } } ]</c>),
	/// matching the OpenSearch dynamic templates wire format, rather than as a plain object.
	/// </summary>
	internal sealed class DynamicTemplatesConverter : JsonConverter<IDynamicTemplateContainer>
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeof(IDynamicTemplateContainer).IsAssignableFrom(typeToConvert);

		public override IDynamicTemplateContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return null;
			}

			var container = new DynamicTemplateContainer();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (reader.TokenType != JsonTokenType.StartObject)
					continue;

				// Each array element is a single-key object { "<name>": { ...template... } }
				reader.Read();
				while (reader.TokenType != JsonTokenType.EndObject)
				{
					var name = reader.GetString();
					reader.Read();
					var template = JsonSerializer.Deserialize<IDynamicTemplate>(ref reader, options);
					if (name != null && template != null)
						container.Add(name, template);
					reader.Read();
				}
			}

			return container;
		}

		public override void Write(Utf8JsonWriter writer, IDynamicTemplateContainer value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();

			foreach (var kvp in value)
			{
				if (kvp.Value == null)
					continue;

				writer.WriteStartObject();
				writer.WritePropertyName(kvp.Key);
				JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
				writer.WriteEndObject();
			}

			writer.WriteEndArray();
		}
	}
}
