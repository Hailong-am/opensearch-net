/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;
using OpenSearch.Net.Utf8Json.Resolvers;

namespace OpenSearch.Client
{
	internal class CompositeAggregationSourceFormatter : IJsonFormatter<ICompositeAggregationSource>
	{
		public void Serialize(ref JsonWriter writer, ICompositeAggregationSource value, IJsonFormatterResolver formatterResolver)
		{
			writer.WriteBeginObject();
			writer.WritePropertyName(value.Name);
			writer.WriteBeginObject();
			writer.WritePropertyName(value.SourceType);

			switch (value)
			{
				case ITermsCompositeAggregationSource termsCompositeAggregationSource:
					Serialize(ref writer, termsCompositeAggregationSource, formatterResolver);
					break;
				case IDateHistogramCompositeAggregationSource dateHistogramCompositeAggregationSource:
					Serialize(ref writer, dateHistogramCompositeAggregationSource, formatterResolver);
					break;
				case IHistogramCompositeAggregationSource histogramCompositeAggregationSource:
					Serialize(ref writer, histogramCompositeAggregationSource, formatterResolver);
					break;
				case IGeoTileGridCompositeAggregationSource geoTileGridCompositeAggregationSource:
					Serialize(ref writer, geoTileGridCompositeAggregationSource, formatterResolver);
					break;
				default:
					Serialize(ref writer, value, formatterResolver);
					break;
			}

			writer.WriteEndObject();
			writer.WriteEndObject();
		}

		private static void Serialize<TCompositeAggregationSource>(ref JsonWriter writer, TCompositeAggregationSource value,
			IJsonFormatterResolver formatterResolver
		) where TCompositeAggregationSource : ICompositeAggregationSource
		{
			var formatter = DynamicObjectResolver.ExcludeNullCamelCase.GetFormatter<TCompositeAggregationSource>();
			formatter.Serialize(ref writer, value, formatterResolver);
		}

		private static readonly AutomataDictionary AggregationSource = new AutomataDictionary
		{
			{ "terms", 0 },
			{ "date_histogram", 1 },
			{ "histogram", 2 },
			{ "geotile_grid", 3 },
		};

		public ICompositeAggregationSource Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() != JsonToken.BeginObject)
				return null;

			reader.ReadIsBeginObjectWithVerify();
			var name = reader.ReadPropertyName();

			reader.ReadIsBeginObjectWithVerify(); // into source

			var sourcePropertyName = reader.ReadPropertyNameSegmentRaw();

			ICompositeAggregationSource compositeAggregationSource = null;

			if (AggregationSource.TryGetValue(sourcePropertyName, out var value))
			{
				switch (value)
				{
					case 0:
						compositeAggregationSource = formatterResolver.GetFormatter<TermsCompositeAggregationSource>()
							.Deserialize(ref reader, formatterResolver);
						break;
					case 1:
						compositeAggregationSource = formatterResolver.GetFormatter<DateHistogramCompositeAggregationSource>()
							.Deserialize(ref reader, formatterResolver);
						break;
					case 2:
						compositeAggregationSource = formatterResolver.GetFormatter<HistogramCompositeAggregationSource>()
							.Deserialize(ref reader, formatterResolver);
						break;
					case 3:
						compositeAggregationSource = formatterResolver.GetFormatter<GeoTileGridCompositeAggregationSource>()
							.Deserialize(ref reader, formatterResolver);
						break;
				}
			}
			else
				throw new Exception($"Unknown {nameof(ICompositeAggregationSource)}: {sourcePropertyName.Utf8String()}");

			reader.ReadIsEndObjectWithVerify();
			reader.ReadIsEndObjectWithVerify();

			compositeAggregationSource.Name = name;
			return compositeAggregationSource;
		}
	}

}
