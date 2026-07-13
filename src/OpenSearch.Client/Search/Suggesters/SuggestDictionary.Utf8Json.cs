/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Collections.Generic;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class SuggestDictionaryFormatter<T> : IJsonFormatter<ISuggestDictionary<T>>
		where T : class
	{
		public ISuggestDictionary<T> Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var formatter = formatterResolver.GetFormatter<Dictionary<string, ISuggest<T>[]>>();
			var dict = formatter.Deserialize(ref reader, formatterResolver);
			return new SuggestDictionary<T>(dict);
		}

		public void Serialize(ref JsonWriter writer, ISuggestDictionary<T> value, IJsonFormatterResolver formatterResolver)
		{
			var formatter = new VerbatimInterfaceReadOnlyDictionaryKeysFormatter<string, ISuggest<T>[]>();
			formatter.Serialize(ref writer, (SuggestDictionary<T>)value, formatterResolver);
		}
	}

}
