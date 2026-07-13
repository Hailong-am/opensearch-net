/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Runtime.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Formatters;

namespace OpenSearch.Client
{
	internal class TotalHitsFormatter : IJsonFormatter<TotalHits>
	{
		private static readonly byte[] ValueField = JsonWriter.GetEncodedPropertyNameWithoutQuotation("value");
		private static readonly byte[] RelationField = JsonWriter.GetEncodedPropertyNameWithoutQuotation("relation");
		private static readonly EnumFormatter<TotalHitsRelation> RelationFormatter = new EnumFormatter<TotalHitsRelation>(true);

		public TotalHits Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			switch (reader.GetCurrentJsonToken())
			{
				case JsonToken.BeginObject:
					var count = 0;
					long value = -1;
					TotalHitsRelation? relation = null;
					while (reader.ReadIsInObject(ref count))
					{
						var propertyName = reader.ReadPropertyNameSegmentRaw();
						if (propertyName.EqualsBytes(ValueField))
							value = reader.ReadInt64();
						else if (propertyName.EqualsBytes(RelationField))
							relation = RelationFormatter.Deserialize(ref reader, formatterResolver);
						else
							reader.ReadNextBlock();
					}

					return new TotalHits { Value = value, Relation = relation };
				case JsonToken.Number:
					return new TotalHits { Value = reader.ReadInt64() };
				default:
					reader.ReadNextBlock();
					return null;
			}
		}

		public void Serialize(ref JsonWriter writer, TotalHits value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			if (value.Relation.HasValue)
			{
				writer.WriteBeginObject();
				writer.WritePropertyName("value");
				writer.WriteInt64(value.Value);
				writer.WriteValueSeparator();
				writer.WritePropertyName("relation");
				RelationFormatter.Serialize(ref writer, value.Relation.Value, formatterResolver);
				writer.WriteEndObject();
			}
			else
				writer.WriteInt64(value.Value);
		}
	}

}
