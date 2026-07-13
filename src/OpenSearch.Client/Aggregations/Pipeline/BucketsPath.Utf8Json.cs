/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Collections.Generic;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class BucketsPathFormatter : IJsonFormatter<IBucketsPath>
	{
		public IBucketsPath Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();
			switch (token)
			{
				case JsonToken.String:
					return new SingleBucketsPath(reader.ReadString());
				case JsonToken.BeginObject:
					var formatter = formatterResolver.GetFormatter<Dictionary<string, string>>();
					var dict = formatter.Deserialize(ref reader, formatterResolver);
					return new MultiBucketsPath(dict);
				default:
					return null;
			}
		}

		public void Serialize(ref JsonWriter writer, IBucketsPath value, IJsonFormatterResolver formatterResolver)
		{
			if (value is SingleBucketsPath single)
				writer.WriteString(single.BucketsPath);
			else if (value is MultiBucketsPath multi)
			{
				writer.WriteBeginObject();
				var count = 0;
				foreach (var kv in multi)
				{
					if (count != 0)
						writer.WriteValueSeparator();
					writer.WritePropertyName(kv.Key);
					writer.WriteString(kv.Value);
					count++;
				}
				writer.WriteEndObject();
			}
			else
				writer.WriteNull();
		}
	}

}
