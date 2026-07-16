/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Reads a JSON value that may be either a single string or an array of strings into an
	/// <see cref="IEnumerable{String}"/>, and always writes an array. Replaces the Utf8Json
	/// <c>SingleOrEnumerableFormatter&lt;string&gt;</c> used on analysis <c>filter</c> / <c>char_filter</c>
	/// members, which OpenSearch may return either as a bare string or an array.
	/// </summary>
	internal sealed class SingleOrManyStringConverter : JsonConverter<IEnumerable<string>>
	{
		public override IEnumerable<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return new[] { reader.GetString() };
				case JsonTokenType.StartArray:
				{
					var list = new List<string>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						if (reader.TokenType == JsonTokenType.Null)
							list.Add(null);
						else
							list.Add(reader.GetString());
					}
					return list;
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, IEnumerable<string> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();
			foreach (var s in value)
			{
				if (s == null)
					writer.WriteNullValue();
				else
					writer.WriteStringValue(s);
			}
			writer.WriteEndArray();
		}
	}
}
