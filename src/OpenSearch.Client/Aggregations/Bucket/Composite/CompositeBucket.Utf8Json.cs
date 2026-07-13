/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class CompositeKeyFormatter : IJsonFormatter<CompositeKey>
	{
		private static readonly VerbatimInterfaceReadOnlyDictionaryKeysPreservingNullFormatter<string, object> DictionaryFormatter =
			new VerbatimInterfaceReadOnlyDictionaryKeysPreservingNullFormatter<string, object>();

		public void Serialize(ref JsonWriter writer, CompositeKey value, IJsonFormatterResolver formatterResolver) =>
			DictionaryFormatter.Serialize(ref writer, value, formatterResolver);

		public CompositeKey Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.ReadIsNull())
				return null;

			var dictionary = DictionaryFormatter.Deserialize(ref reader, formatterResolver);
			return new CompositeKey(dictionary);
		}
	}

}
