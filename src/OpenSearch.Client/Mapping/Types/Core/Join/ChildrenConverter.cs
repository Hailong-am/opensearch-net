/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="Children"/> that serializes a single
	/// relation as a scalar string and multiple relations as a JSON array of strings.
	/// Individual <see cref="RelationName"/> elements are delegated to the options-registered
	/// <see cref="RelationNameConverter"/> (settings-aware name inference).
	/// </summary>
	internal sealed class ChildrenConverter : JsonConverter<Children>
	{
		public override Children Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
		{
			var children = new Children();

			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return children;
				case JsonTokenType.String:
				{
					var relation = JsonSerializer.Deserialize<RelationName>(ref reader, options);
					if (relation != null) children.Add(relation);
					return children;
				}
				case JsonTokenType.StartArray:
				{
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						var relation = JsonSerializer.Deserialize<RelationName>(ref reader, options);
						if (relation != null) children.Add(relation);
					}
					return children;
				}
				default:
					reader.Skip();
					return children;
			}
		}

		public override void Write(Utf8JsonWriter writer, Children value, JsonSerializerOptions options)
		{
			if (value == null || value.Count == 0)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.Count == 1)
			{
				JsonSerializer.Serialize(writer, value[0], options);
				return;
			}

			writer.WriteStartArray();
			foreach (var child in value)
				JsonSerializer.Serialize(writer, child, options);
			writer.WriteEndArray();
		}
	}
}
