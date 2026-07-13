/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class DistanceFeatureQueryFormatter : IJsonFormatter<IDistanceFeatureQuery>
	{
		private static readonly UnionFormatter<GeoCoordinate, DateMath> OriginUnionFormatter = new UnionFormatter<GeoCoordinate, DateMath>(true);
		private static readonly UnionFormatter<Distance, Time> PivotUnionFormatter = new UnionFormatter<Distance, Time>();

		public void Serialize(ref JsonWriter writer, IDistanceFeatureQuery value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteBeginObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName("_name");
				writer.WriteString(value.Name);
				writer.WriteValueSeparator();
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName("boost");
				writer.WriteDouble(value.Boost.Value);
				writer.WriteValueSeparator();
			}

			writer.WritePropertyName("field");
			var fieldFormatter = formatterResolver.GetFormatter<Field>();
			fieldFormatter.Serialize(ref writer, value.Field, formatterResolver);
			writer.WriteValueSeparator();

			writer.WritePropertyName("origin");
			OriginUnionFormatter.Serialize(ref writer, value.Origin, formatterResolver);
			writer.WriteValueSeparator();

			writer.WritePropertyName("pivot");
			PivotUnionFormatter.Serialize(ref writer, value.Pivot, formatterResolver);

			writer.WriteEndObject();
		}

		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "field", 0 },
			{ "origin", 1 },
			{ "pivot", 2 },
			{ "boost", 3 },
			{ "_name", 4 }
		};

		public IDistanceFeatureQuery Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.ReadIsNull())
				return null;

			var query = new DistanceFeatureQuery();
			var count = 0;
			while (reader.ReadIsInObject(ref count))
			{
				if (Fields.TryGetValue(reader.ReadPropertyNameSegmentRaw(), out var value))
				{
					switch (value)
					{
						case 0:
							query.Field = formatterResolver.GetFormatter<Field>().Deserialize(ref reader, formatterResolver);
							break;
						case 1:
							query.Origin = OriginUnionFormatter.Deserialize(ref reader, formatterResolver);
							break;
						case 2:
							query.Pivot = PivotUnionFormatter.Deserialize(ref reader, formatterResolver);
							break;
						case 3:
							query.Boost = reader.ReadDouble();
							break;
						case 4:
							query.Name = reader.ReadString();
							break;
					}
				}
				else
					reader.ReadNextBlock();
			}

			return query;
		}
	}

}
