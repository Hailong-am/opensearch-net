/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Runtime.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class DynamicMappingFormatter : IJsonFormatter<Union<bool,DynamicMapping>>
	{
		private static readonly AutomataDictionary Values = new AutomataDictionary { { "true", 0 }, { "false", 1 }, { "strict", 2 } };

		public void Serialize(ref JsonWriter writer, Union<bool, DynamicMapping> value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			switch (value.Tag)
			{
				case 0:
					writer.WriteBoolean(value.Item1);
					break;
				case 1:
					writer.WriteString(value.Item2.GetStringValue());
					break;
			}
		}

		public Union<bool, DynamicMapping> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.ReadIsNull())
				return null;

			var token = reader.GetCurrentJsonToken();
			switch (token)
			{
				case JsonToken.True:
				case JsonToken.False:
					return new Union<bool, DynamicMapping>(reader.ReadBoolean());
				case JsonToken.String:
					var segment = reader.ReadStringSegmentUnsafe();
					if (Values.TryGetValue(segment, out var value))
					{
						switch (value)
						{
							case 0:
								return new Union<bool, DynamicMapping>(true);
							case 1:
								return new Union<bool, DynamicMapping>(false);
							case 2:
								return new Union<bool, DynamicMapping>(DynamicMapping.Strict);
						}
					}

					return null;
				default:
					throw new JsonParsingException($"Cannot parse Union<bool, DynamicMapping> from token '{token}'");
			}
		}
	}

}
