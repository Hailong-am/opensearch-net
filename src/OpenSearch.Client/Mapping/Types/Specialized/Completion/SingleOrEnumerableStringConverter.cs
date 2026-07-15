/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Reads a JSON value that may be either a single string/number or an array into an
	/// <see cref="IEnumerable{String}"/>, and writes a single-element collection as a scalar
	/// string (not wrapped in an array). This matches the Utf8Json
	/// <c>SingleOrEnumerableFormatter&lt;string&gt;</c> write behavior used by
	/// GeoSuggestContext.Precision and similar fields.
	/// </summary>
	internal sealed class SingleOrEnumerableStringConverter : JsonConverter<IEnumerable<string>>
	{
		public override IEnumerable<string> Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return new[] { reader.GetString() };
				case JsonTokenType.Number:
					return new[] { reader.GetInt64().ToString() };
				case JsonTokenType.StartArray:
				{
					var list = new List<string>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						switch (reader.TokenType)
						{
							case JsonTokenType.Null:
								list.Add(null);
								break;
							case JsonTokenType.String:
								list.Add(reader.GetString());
								break;
							case JsonTokenType.Number:
								list.Add(reader.GetInt64().ToString());
								break;
							default:
								reader.Skip();
								break;
						}
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

			var list = value as IList<string> ?? value.ToList();

			if (list.Count == 1)
			{
				// Single element: write as scalar (matching Utf8Json SingleOrEnumerableFormatter behavior)
				writer.WriteStringValue(list[0]);
			}
			else
			{
				writer.WriteStartArray();
				foreach (var s in list)
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
}
