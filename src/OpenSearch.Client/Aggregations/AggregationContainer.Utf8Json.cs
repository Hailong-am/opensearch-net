/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class AggregationDictionaryFormatter : IJsonFormatter<AggregationDictionary>
	{
		private static readonly VerbatimDictionaryInterfaceKeysFormatter<string, IAggregationContainer> DictionaryKeysFormatter =
			new VerbatimDictionaryInterfaceKeysFormatter<string, IAggregationContainer>();

		public AggregationDictionary Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver) =>
			new AggregationDictionary(DictionaryKeysFormatter.Deserialize(ref reader, formatterResolver));

		public void Serialize(ref JsonWriter writer, AggregationDictionary value, IJsonFormatterResolver formatterResolver) =>
			DictionaryKeysFormatter.Serialize(ref writer, value, formatterResolver);
	}

}
