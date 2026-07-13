/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class SpanGapQueryFormatter : IJsonFormatter<ISpanGapQuery>
	{
		public void Serialize(ref JsonWriter writer, ISpanGapQuery value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null || SpanGapQuery.IsConditionless(value))
			{
				writer.WriteNull();
				return;
			}

			writer.WriteBeginObject();
			var inferrer = formatterResolver.GetConnectionSettings().Inferrer;
			writer.WritePropertyName(inferrer.Field(value.Field));
			writer.WriteInt32(value.Width.Value);
			writer.WriteEndObject();
		}

		public ISpanGapQuery Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() == JsonToken.Null)
			{
				reader.ReadNext();
				return null;
			}

			var count = 0;
			var query = new SpanGapQuery();

			while (reader.ReadIsInObject(ref count))
			{
				if (count > 1)
					continue;

				query.Field = reader.ReadPropertyName();
				query.Width = reader.ReadInt32();
			}

			return query;
		}
	}

}
