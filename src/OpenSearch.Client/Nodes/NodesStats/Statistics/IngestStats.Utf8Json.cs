/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Collections.Generic;
using System.Runtime.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class KeyedProcessorStatsFormatter : IJsonFormatter<KeyedProcessorStats>
	{
		public KeyedProcessorStats Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() != JsonToken.BeginObject)
				return null;

			var count = 0;
			var stats = new KeyedProcessorStats();
			while (reader.ReadIsInObject(ref count))
			{
				stats.Type = reader.ReadPropertyName();
				stats.Statistics = formatterResolver.GetFormatter<ProcessStats>()
					.Deserialize(ref reader, formatterResolver);
			}

			return stats;
		}

		public void Serialize(ref JsonWriter writer, KeyedProcessorStats value, IJsonFormatterResolver formatterResolver)
		{
			if (value?.Type == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteBeginObject();
			writer.WritePropertyName(value.Type);
			formatterResolver.GetFormatter<ProcessStats>().Serialize(ref writer, value.Statistics, formatterResolver);
			writer.WriteEndObject();
		}
	}

}
