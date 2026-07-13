/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Collections.Generic;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class StopWordsFormatter : IJsonFormatter<StopWords>
	{
		public StopWords Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();
			if (token == JsonToken.BeginArray)
			{
				var stopwords = formatterResolver.GetFormatter<IEnumerable<string>>()
					.Deserialize(ref reader, formatterResolver);
				return new StopWords(stopwords);
			}

			var stopword = reader.ReadString();
			return new StopWords(stopword);
		}

		public void Serialize(ref JsonWriter writer, StopWords value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			switch (value.Tag)
			{
				case 0:
					writer.WriteString(value.Item1);
					break;
				case 1:
					var formatter = formatterResolver.GetFormatter<IEnumerable<string>>();
					formatter.Serialize(ref writer, value.Item2, formatterResolver);
					break;
			}
		}
	}

}
