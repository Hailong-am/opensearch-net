/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="AggregationContainer"/> that handles
	/// the polymorphic aggregation dispatch pattern used by OpenSearch.
	/// Each aggregation container holds one aggregation type property (e.g. "terms", "avg"),
	/// optionally "meta" metadata, and optionally nested "aggs" sub-aggregations.
	/// </summary>
	internal sealed class AggregationContainerConverter : JsonConverter<AggregationContainer>
	{
		// Pre-computed property names for aggregation types
		private static readonly JsonEncodedText AggsProp = JsonEncodedText.Encode("aggs");
		private static readonly JsonEncodedText AggregationsProp = JsonEncodedText.Encode("aggregations");
		private static readonly JsonEncodedText MetaProp = JsonEncodedText.Encode("meta");
		private static readonly JsonEncodedText AdjacencyMatrixProp = JsonEncodedText.Encode("adjacency_matrix");
		private static readonly JsonEncodedText AvgProp = JsonEncodedText.Encode("avg");
		private static readonly JsonEncodedText AvgBucketProp = JsonEncodedText.Encode("avg_bucket");
		private static readonly JsonEncodedText BucketScriptProp = JsonEncodedText.Encode("bucket_script");
		private static readonly JsonEncodedText BucketSelectorProp = JsonEncodedText.Encode("bucket_selector");
		private static readonly JsonEncodedText BucketSortProp = JsonEncodedText.Encode("bucket_sort");
		private static readonly JsonEncodedText CardinalityProp = JsonEncodedText.Encode("cardinality");
		private static readonly JsonEncodedText ChildrenProp = JsonEncodedText.Encode("children");
		private static readonly JsonEncodedText CompositeProp = JsonEncodedText.Encode("composite");
		private static readonly JsonEncodedText CumulativeSumProp = JsonEncodedText.Encode("cumulative_sum");
		private static readonly JsonEncodedText DateHistogramProp = JsonEncodedText.Encode("date_histogram");
		private static readonly JsonEncodedText AutoDateHistogramProp = JsonEncodedText.Encode("auto_date_histogram");
		private static readonly JsonEncodedText DateRangeProp = JsonEncodedText.Encode("date_range");
		private static readonly JsonEncodedText DerivativeProp = JsonEncodedText.Encode("derivative");
		private static readonly JsonEncodedText DiversifiedSamplerProp = JsonEncodedText.Encode("diversified_sampler");
		private static readonly JsonEncodedText ExtendedStatsProp = JsonEncodedText.Encode("extended_stats");
		private static readonly JsonEncodedText ExtendedStatsBucketProp = JsonEncodedText.Encode("extended_stats_bucket");
		private static readonly JsonEncodedText FilterProp = JsonEncodedText.Encode("filter");
		private static readonly JsonEncodedText FiltersProp = JsonEncodedText.Encode("filters");
		private static readonly JsonEncodedText GeoBoundsProp = JsonEncodedText.Encode("geo_bounds");
		private static readonly JsonEncodedText GeoCentroidProp = JsonEncodedText.Encode("geo_centroid");
		private static readonly JsonEncodedText GeoDistanceProp = JsonEncodedText.Encode("geo_distance");
		private static readonly JsonEncodedText GeoHashGridProp = JsonEncodedText.Encode("geohash_grid");
		private static readonly JsonEncodedText GeoLineProp = JsonEncodedText.Encode("geo_line");
		private static readonly JsonEncodedText GeoTileGridProp = JsonEncodedText.Encode("geotile_grid");
		private static readonly JsonEncodedText GlobalProp = JsonEncodedText.Encode("global");
		private static readonly JsonEncodedText HistogramProp = JsonEncodedText.Encode("histogram");
		private static readonly JsonEncodedText IpRangeProp = JsonEncodedText.Encode("ip_range");
		private static readonly JsonEncodedText MatrixStatsProp = JsonEncodedText.Encode("matrix_stats");
		private static readonly JsonEncodedText MaxProp = JsonEncodedText.Encode("max");
		private static readonly JsonEncodedText MaxBucketProp = JsonEncodedText.Encode("max_bucket");
		private static readonly JsonEncodedText MinProp = JsonEncodedText.Encode("min");
		private static readonly JsonEncodedText MinBucketProp = JsonEncodedText.Encode("min_bucket");
		private static readonly JsonEncodedText MissingProp = JsonEncodedText.Encode("missing");
		private static readonly JsonEncodedText MovingAvgProp = JsonEncodedText.Encode("moving_avg");
		private static readonly JsonEncodedText MovingFnProp = JsonEncodedText.Encode("moving_fn");
		private static readonly JsonEncodedText NestedProp = JsonEncodedText.Encode("nested");
		private static readonly JsonEncodedText ParentProp = JsonEncodedText.Encode("parent");
		private static readonly JsonEncodedText PercentileRanksProp = JsonEncodedText.Encode("percentile_ranks");
		private static readonly JsonEncodedText PercentilesProp = JsonEncodedText.Encode("percentiles");
		private static readonly JsonEncodedText PercentilesBucketProp = JsonEncodedText.Encode("percentiles_bucket");
		private static readonly JsonEncodedText RangeProp = JsonEncodedText.Encode("range");
		private static readonly JsonEncodedText RareTermsProp = JsonEncodedText.Encode("rare_terms");
		private static readonly JsonEncodedText ReverseNestedProp = JsonEncodedText.Encode("reverse_nested");
		private static readonly JsonEncodedText SamplerProp = JsonEncodedText.Encode("sampler");
		private static readonly JsonEncodedText ScriptedMetricProp = JsonEncodedText.Encode("scripted_metric");
		private static readonly JsonEncodedText SerialDiffProp = JsonEncodedText.Encode("serial_diff");
		private static readonly JsonEncodedText SignificantTermsProp = JsonEncodedText.Encode("significant_terms");
		private static readonly JsonEncodedText SignificantTextProp = JsonEncodedText.Encode("significant_text");
		private static readonly JsonEncodedText StatsProp = JsonEncodedText.Encode("stats");
		private static readonly JsonEncodedText StatsBucketProp = JsonEncodedText.Encode("stats_bucket");
		private static readonly JsonEncodedText SumProp = JsonEncodedText.Encode("sum");
		private static readonly JsonEncodedText SumBucketProp = JsonEncodedText.Encode("sum_bucket");
		private static readonly JsonEncodedText TermsProp = JsonEncodedText.Encode("terms");
		private static readonly JsonEncodedText TopHitsProp = JsonEncodedText.Encode("top_hits");
		private static readonly JsonEncodedText ValueCountProp = JsonEncodedText.Encode("value_count");
		private static readonly JsonEncodedText WeightedAvgProp = JsonEncodedText.Encode("weighted_avg");
		private static readonly JsonEncodedText MedianAbsoluteDeviationProp = JsonEncodedText.Encode("median_absolute_deviation");
		private static readonly JsonEncodedText MultiTermsProp = JsonEncodedText.Encode("multi_terms");
		private static readonly JsonEncodedText VariableWidthHistogramProp = JsonEncodedText.Encode("variable_width_histogram");

		public override AggregationContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var container = new AggregationContainer();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to the value

				ReadAggregationProperty(container, propertyName, ref reader, options);
			}

			return container;
		}

		private static void ReadAggregationProperty(
			AggregationContainer container,
			string propertyName,
			ref Utf8JsonReader reader,
			JsonSerializerOptions options)
		{
			switch (propertyName)
			{
				case "aggs":
				case "aggregations":
					container.Aggregations = JsonSerializer.Deserialize<AggregationDictionary>(ref reader, options);
					break;
				case "meta":
					container.Meta = JsonSerializer.Deserialize<IDictionary<string, object>>(ref reader, options);
					break;
				case "adjacency_matrix":
					container.AdjacencyMatrix = JsonSerializer.Deserialize<IAdjacencyMatrixAggregation>(ref reader, options);
					break;
				case "avg":
					container.Average = JsonSerializer.Deserialize<IAverageAggregation>(ref reader, options);
					break;
				case "avg_bucket":
					container.AverageBucket = JsonSerializer.Deserialize<IAverageBucketAggregation>(ref reader, options);
					break;
				case "bucket_script":
					container.BucketScript = JsonSerializer.Deserialize<IBucketScriptAggregation>(ref reader, options);
					break;
				case "bucket_selector":
					container.BucketSelector = JsonSerializer.Deserialize<IBucketSelectorAggregation>(ref reader, options);
					break;
				case "bucket_sort":
					container.BucketSort = JsonSerializer.Deserialize<IBucketSortAggregation>(ref reader, options);
					break;
				case "cardinality":
					container.Cardinality = JsonSerializer.Deserialize<ICardinalityAggregation>(ref reader, options);
					break;
				case "children":
					container.Children = JsonSerializer.Deserialize<IChildrenAggregation>(ref reader, options);
					break;
				case "composite":
					container.Composite = JsonSerializer.Deserialize<ICompositeAggregation>(ref reader, options);
					break;
				case "cumulative_sum":
					container.CumulativeSum = JsonSerializer.Deserialize<ICumulativeSumAggregation>(ref reader, options);
					break;
				case "date_histogram":
					container.DateHistogram = JsonSerializer.Deserialize<IDateHistogramAggregation>(ref reader, options);
					break;
				case "auto_date_histogram":
					container.AutoDateHistogram = JsonSerializer.Deserialize<IAutoDateHistogramAggregation>(ref reader, options);
					break;
				case "date_range":
					container.DateRange = JsonSerializer.Deserialize<IDateRangeAggregation>(ref reader, options);
					break;
				case "derivative":
					container.Derivative = JsonSerializer.Deserialize<IDerivativeAggregation>(ref reader, options);
					break;
				case "diversified_sampler":
					container.DiversifiedSampler = JsonSerializer.Deserialize<IDiversifiedSamplerAggregation>(ref reader, options);
					break;
				case "extended_stats":
					container.ExtendedStats = JsonSerializer.Deserialize<IExtendedStatsAggregation>(ref reader, options);
					break;
				case "extended_stats_bucket":
					container.ExtendedStatsBucket = JsonSerializer.Deserialize<IExtendedStatsBucketAggregation>(ref reader, options);
					break;
				case "filter":
					container.Filter = JsonSerializer.Deserialize<IFilterAggregation>(ref reader, options);
					break;
				case "filters":
					container.Filters = JsonSerializer.Deserialize<IFiltersAggregation>(ref reader, options);
					break;
				case "geo_bounds":
					container.GeoBounds = JsonSerializer.Deserialize<IGeoBoundsAggregation>(ref reader, options);
					break;
				case "geo_centroid":
					container.GeoCentroid = JsonSerializer.Deserialize<IGeoCentroidAggregation>(ref reader, options);
					break;
				case "geo_distance":
					container.GeoDistance = JsonSerializer.Deserialize<IGeoDistanceAggregation>(ref reader, options);
					break;
				case "geohash_grid":
					container.GeoHash = JsonSerializer.Deserialize<IGeoHashGridAggregation>(ref reader, options);
					break;
				case "geo_line":
					container.GeoLine = JsonSerializer.Deserialize<IGeoLineAggregation>(ref reader, options);
					break;
				case "geotile_grid":
					container.GeoTile = JsonSerializer.Deserialize<IGeoTileGridAggregation>(ref reader, options);
					break;
				case "global":
					container.Global = JsonSerializer.Deserialize<IGlobalAggregation>(ref reader, options);
					break;
				case "histogram":
					container.Histogram = JsonSerializer.Deserialize<IHistogramAggregation>(ref reader, options);
					break;
				case "ip_range":
					container.IpRange = JsonSerializer.Deserialize<IIpRangeAggregation>(ref reader, options);
					break;
				case "matrix_stats":
					container.MatrixStats = JsonSerializer.Deserialize<IMatrixStatsAggregation>(ref reader, options);
					break;
				case "max":
					container.Max = JsonSerializer.Deserialize<IMaxAggregation>(ref reader, options);
					break;
				case "max_bucket":
					container.MaxBucket = JsonSerializer.Deserialize<IMaxBucketAggregation>(ref reader, options);
					break;
				case "min":
					container.Min = JsonSerializer.Deserialize<IMinAggregation>(ref reader, options);
					break;
				case "min_bucket":
					container.MinBucket = JsonSerializer.Deserialize<IMinBucketAggregation>(ref reader, options);
					break;
				case "missing":
					container.Missing = JsonSerializer.Deserialize<IMissingAggregation>(ref reader, options);
					break;
				case "moving_avg":
					container.MovingAverage = JsonSerializer.Deserialize<IMovingAverageAggregation>(ref reader, options);
					break;
				case "moving_fn":
					container.MovingFunction = JsonSerializer.Deserialize<IMovingFunctionAggregation>(ref reader, options);
					break;
				case "nested":
					container.Nested = JsonSerializer.Deserialize<INestedAggregation>(ref reader, options);
					break;
				case "parent":
					container.Parent = JsonSerializer.Deserialize<IParentAggregation>(ref reader, options);
					break;
				case "percentile_ranks":
					container.PercentileRanks = JsonSerializer.Deserialize<IPercentileRanksAggregation>(ref reader, options);
					break;
				case "percentiles":
					container.Percentiles = JsonSerializer.Deserialize<IPercentilesAggregation>(ref reader, options);
					break;
				case "percentiles_bucket":
					container.PercentilesBucket = JsonSerializer.Deserialize<IPercentilesBucketAggregation>(ref reader, options);
					break;
				case "range":
					container.Range = JsonSerializer.Deserialize<IRangeAggregation>(ref reader, options);
					break;
				case "rare_terms":
					container.RareTerms = JsonSerializer.Deserialize<IRareTermsAggregation>(ref reader, options);
					break;
				case "reverse_nested":
					container.ReverseNested = JsonSerializer.Deserialize<IReverseNestedAggregation>(ref reader, options);
					break;
				case "sampler":
					container.Sampler = JsonSerializer.Deserialize<ISamplerAggregation>(ref reader, options);
					break;
				case "scripted_metric":
					container.ScriptedMetric = JsonSerializer.Deserialize<IScriptedMetricAggregation>(ref reader, options);
					break;
				case "serial_diff":
					container.SerialDifferencing = JsonSerializer.Deserialize<ISerialDifferencingAggregation>(ref reader, options);
					break;
				case "significant_terms":
					container.SignificantTerms = JsonSerializer.Deserialize<ISignificantTermsAggregation>(ref reader, options);
					break;
				case "significant_text":
					container.SignificantText = JsonSerializer.Deserialize<ISignificantTextAggregation>(ref reader, options);
					break;
				case "stats":
					container.Stats = JsonSerializer.Deserialize<IStatsAggregation>(ref reader, options);
					break;
				case "stats_bucket":
					container.StatsBucket = JsonSerializer.Deserialize<IStatsBucketAggregation>(ref reader, options);
					break;
				case "sum":
					container.Sum = JsonSerializer.Deserialize<ISumAggregation>(ref reader, options);
					break;
				case "sum_bucket":
					container.SumBucket = JsonSerializer.Deserialize<ISumBucketAggregation>(ref reader, options);
					break;
				case "terms":
					container.Terms = JsonSerializer.Deserialize<ITermsAggregation>(ref reader, options);
					break;
				case "top_hits":
					container.TopHits = JsonSerializer.Deserialize<ITopHitsAggregation>(ref reader, options);
					break;
				case "value_count":
					container.ValueCount = JsonSerializer.Deserialize<IValueCountAggregation>(ref reader, options);
					break;
				case "weighted_avg":
					container.WeightedAverage = JsonSerializer.Deserialize<IWeightedAverageAggregation>(ref reader, options);
					break;
				case "median_absolute_deviation":
					container.MedianAbsoluteDeviation = JsonSerializer.Deserialize<IMedianAbsoluteDeviationAggregation>(ref reader, options);
					break;
				case "multi_terms":
					container.MultiTerms = JsonSerializer.Deserialize<IMultiTermsAggregation>(ref reader, options);
					break;
				case "variable_width_histogram":
					container.VariableWidthHistogram = JsonSerializer.Deserialize<IVariableWidthHistogramAggregation>(ref reader, options);
					break;
				default:
					// Unknown aggregation type — skip the value to maintain forward compatibility
					reader.Skip();
					break;
			}
		}

		public override void Write(Utf8JsonWriter writer, AggregationContainer value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			// Write the aggregation type property (only one should be non-null)
			WriteAggregationType(writer, value, options);

			// Write meta if present
			if (value.Meta != null && value.Meta.Count > 0)
			{
				writer.WritePropertyName(MetaProp);
				JsonSerializer.Serialize(writer, value.Meta, options);
			}

			// Write nested aggregations if present
			if (value.Aggregations != null && ((IDictionary<string, IAggregationContainer>)value.Aggregations).Count > 0)
			{
				writer.WritePropertyName(AggsProp);
				JsonSerializer.Serialize(writer, value.Aggregations, options);
			}

			writer.WriteEndObject();
		}

		private static void WriteAggregationType(Utf8JsonWriter writer, AggregationContainer value, JsonSerializerOptions options)
		{
			if (value.AdjacencyMatrix != null)
			{
				writer.WritePropertyName(AdjacencyMatrixProp);
				JsonSerializer.Serialize(writer, value.AdjacencyMatrix, options);
			}
			else if (value.Average != null)
			{
				writer.WritePropertyName(AvgProp);
				JsonSerializer.Serialize(writer, value.Average, options);
			}
			else if (value.AverageBucket != null)
			{
				writer.WritePropertyName(AvgBucketProp);
				JsonSerializer.Serialize(writer, value.AverageBucket, options);
			}
			else if (value.BucketScript != null)
			{
				writer.WritePropertyName(BucketScriptProp);
				JsonSerializer.Serialize(writer, value.BucketScript, options);
			}
			else if (value.BucketSelector != null)
			{
				writer.WritePropertyName(BucketSelectorProp);
				JsonSerializer.Serialize(writer, value.BucketSelector, options);
			}
			else if (value.BucketSort != null)
			{
				writer.WritePropertyName(BucketSortProp);
				JsonSerializer.Serialize(writer, value.BucketSort, options);
			}
			else if (value.Cardinality != null)
			{
				writer.WritePropertyName(CardinalityProp);
				JsonSerializer.Serialize(writer, value.Cardinality, options);
			}
			else if (value.Children != null)
			{
				writer.WritePropertyName(ChildrenProp);
				JsonSerializer.Serialize(writer, value.Children, options);
			}
			else if (value.Composite != null)
			{
				writer.WritePropertyName(CompositeProp);
				JsonSerializer.Serialize(writer, value.Composite, options);
			}
			else if (value.CumulativeSum != null)
			{
				writer.WritePropertyName(CumulativeSumProp);
				JsonSerializer.Serialize(writer, value.CumulativeSum, options);
			}
			else if (value.DateHistogram != null)
			{
				writer.WritePropertyName(DateHistogramProp);
				JsonSerializer.Serialize(writer, value.DateHistogram, options);
			}
			else if (value.AutoDateHistogram != null)
			{
				writer.WritePropertyName(AutoDateHistogramProp);
				JsonSerializer.Serialize(writer, value.AutoDateHistogram, options);
			}
			else if (value.DateRange != null)
			{
				writer.WritePropertyName(DateRangeProp);
				JsonSerializer.Serialize(writer, value.DateRange, options);
			}
			else if (value.Derivative != null)
			{
				writer.WritePropertyName(DerivativeProp);
				JsonSerializer.Serialize(writer, value.Derivative, options);
			}
			else if (value.DiversifiedSampler != null)
			{
				writer.WritePropertyName(DiversifiedSamplerProp);
				JsonSerializer.Serialize(writer, value.DiversifiedSampler, options);
			}
			else if (value.ExtendedStats != null)
			{
				writer.WritePropertyName(ExtendedStatsProp);
				JsonSerializer.Serialize(writer, value.ExtendedStats, options);
			}
			else if (value.ExtendedStatsBucket != null)
			{
				writer.WritePropertyName(ExtendedStatsBucketProp);
				JsonSerializer.Serialize(writer, value.ExtendedStatsBucket, options);
			}
			else if (value.Filter != null)
			{
				writer.WritePropertyName(FilterProp);
				JsonSerializer.Serialize(writer, value.Filter, options);
			}
			else if (value.Filters != null)
			{
				writer.WritePropertyName(FiltersProp);
				JsonSerializer.Serialize(writer, value.Filters, options);
			}
			else if (value.GeoBounds != null)
			{
				writer.WritePropertyName(GeoBoundsProp);
				JsonSerializer.Serialize(writer, value.GeoBounds, options);
			}
			else if (value.GeoCentroid != null)
			{
				writer.WritePropertyName(GeoCentroidProp);
				JsonSerializer.Serialize(writer, value.GeoCentroid, options);
			}
			else if (value.GeoDistance != null)
			{
				writer.WritePropertyName(GeoDistanceProp);
				JsonSerializer.Serialize(writer, value.GeoDistance, options);
			}
			else if (value.GeoHash != null)
			{
				writer.WritePropertyName(GeoHashGridProp);
				JsonSerializer.Serialize(writer, value.GeoHash, options);
			}
			else if (value.GeoLine != null)
			{
				writer.WritePropertyName(GeoLineProp);
				JsonSerializer.Serialize(writer, value.GeoLine, options);
			}
			else if (value.GeoTile != null)
			{
				writer.WritePropertyName(GeoTileGridProp);
				JsonSerializer.Serialize(writer, value.GeoTile, options);
			}
			else if (value.Global != null)
			{
				writer.WritePropertyName(GlobalProp);
				JsonSerializer.Serialize(writer, value.Global, options);
			}
			else if (value.Histogram != null)
			{
				writer.WritePropertyName(HistogramProp);
				JsonSerializer.Serialize(writer, value.Histogram, options);
			}
			else if (value.IpRange != null)
			{
				writer.WritePropertyName(IpRangeProp);
				JsonSerializer.Serialize(writer, value.IpRange, options);
			}
			else if (value.MatrixStats != null)
			{
				writer.WritePropertyName(MatrixStatsProp);
				JsonSerializer.Serialize(writer, value.MatrixStats, options);
			}
			else if (value.Max != null)
			{
				writer.WritePropertyName(MaxProp);
				JsonSerializer.Serialize(writer, value.Max, options);
			}
			else if (value.MaxBucket != null)
			{
				writer.WritePropertyName(MaxBucketProp);
				JsonSerializer.Serialize(writer, value.MaxBucket, options);
			}
			else if (value.Min != null)
			{
				writer.WritePropertyName(MinProp);
				JsonSerializer.Serialize(writer, value.Min, options);
			}
			else if (value.MinBucket != null)
			{
				writer.WritePropertyName(MinBucketProp);
				JsonSerializer.Serialize(writer, value.MinBucket, options);
			}
			else if (value.Missing != null)
			{
				writer.WritePropertyName(MissingProp);
				JsonSerializer.Serialize(writer, value.Missing, options);
			}
			else if (value.MovingAverage != null)
			{
				writer.WritePropertyName(MovingAvgProp);
				JsonSerializer.Serialize(writer, value.MovingAverage, options);
			}
			else if (value.MovingFunction != null)
			{
				writer.WritePropertyName(MovingFnProp);
				JsonSerializer.Serialize(writer, value.MovingFunction, options);
			}
			else if (value.Nested != null)
			{
				writer.WritePropertyName(NestedProp);
				JsonSerializer.Serialize(writer, value.Nested, options);
			}
			else if (value.Parent != null)
			{
				writer.WritePropertyName(ParentProp);
				JsonSerializer.Serialize(writer, value.Parent, options);
			}
			else if (value.PercentileRanks != null)
			{
				writer.WritePropertyName(PercentileRanksProp);
				JsonSerializer.Serialize(writer, value.PercentileRanks, options);
			}
			else if (value.Percentiles != null)
			{
				writer.WritePropertyName(PercentilesProp);
				JsonSerializer.Serialize(writer, value.Percentiles, options);
			}
			else if (value.PercentilesBucket != null)
			{
				writer.WritePropertyName(PercentilesBucketProp);
				JsonSerializer.Serialize(writer, value.PercentilesBucket, options);
			}
			else if (value.Range != null)
			{
				writer.WritePropertyName(RangeProp);
				JsonSerializer.Serialize(writer, value.Range, options);
			}
			else if (value.RareTerms != null)
			{
				writer.WritePropertyName(RareTermsProp);
				JsonSerializer.Serialize(writer, value.RareTerms, options);
			}
			else if (value.ReverseNested != null)
			{
				writer.WritePropertyName(ReverseNestedProp);
				JsonSerializer.Serialize(writer, value.ReverseNested, options);
			}
			else if (value.Sampler != null)
			{
				writer.WritePropertyName(SamplerProp);
				JsonSerializer.Serialize(writer, value.Sampler, options);
			}
			else if (value.ScriptedMetric != null)
			{
				writer.WritePropertyName(ScriptedMetricProp);
				JsonSerializer.Serialize(writer, value.ScriptedMetric, options);
			}
			else if (value.SerialDifferencing != null)
			{
				writer.WritePropertyName(SerialDiffProp);
				JsonSerializer.Serialize(writer, value.SerialDifferencing, options);
			}
			else if (value.SignificantTerms != null)
			{
				writer.WritePropertyName(SignificantTermsProp);
				JsonSerializer.Serialize(writer, value.SignificantTerms, options);
			}
			else if (value.SignificantText != null)
			{
				writer.WritePropertyName(SignificantTextProp);
				JsonSerializer.Serialize(writer, value.SignificantText, options);
			}
			else if (value.Stats != null)
			{
				writer.WritePropertyName(StatsProp);
				JsonSerializer.Serialize(writer, value.Stats, options);
			}
			else if (value.StatsBucket != null)
			{
				writer.WritePropertyName(StatsBucketProp);
				JsonSerializer.Serialize(writer, value.StatsBucket, options);
			}
			else if (value.Sum != null)
			{
				writer.WritePropertyName(SumProp);
				JsonSerializer.Serialize(writer, value.Sum, options);
			}
			else if (value.SumBucket != null)
			{
				writer.WritePropertyName(SumBucketProp);
				JsonSerializer.Serialize(writer, value.SumBucket, options);
			}
			else if (value.Terms != null)
			{
				writer.WritePropertyName(TermsProp);
				JsonSerializer.Serialize(writer, value.Terms, options);
			}
			else if (value.TopHits != null)
			{
				writer.WritePropertyName(TopHitsProp);
				JsonSerializer.Serialize(writer, value.TopHits, options);
			}
			else if (value.ValueCount != null)
			{
				writer.WritePropertyName(ValueCountProp);
				JsonSerializer.Serialize(writer, value.ValueCount, options);
			}
			else if (value.WeightedAverage != null)
			{
				writer.WritePropertyName(WeightedAvgProp);
				JsonSerializer.Serialize(writer, value.WeightedAverage, options);
			}
			else if (value.MedianAbsoluteDeviation != null)
			{
				writer.WritePropertyName(MedianAbsoluteDeviationProp);
				JsonSerializer.Serialize(writer, value.MedianAbsoluteDeviation, options);
			}
			else if (value.MultiTerms != null)
			{
				writer.WritePropertyName(MultiTermsProp);
				JsonSerializer.Serialize(writer, value.MultiTerms, options);
			}
			else if (value.VariableWidthHistogram != null)
			{
				writer.WritePropertyName(VariableWidthHistogramProp);
				JsonSerializer.Serialize(writer, value.VariableWidthHistogram, options);
			}
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IAggregationContainer"/> that delegates
	/// to <see cref="AggregationContainerConverter"/> for interface-based serialization.
	/// </summary>
	internal sealed class AggregationContainerInterfaceConverter : JsonConverter<IAggregationContainer>
	{
		private static readonly AggregationContainerConverter ConcreteConverter = new();

		public override IAggregationContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			ConcreteConverter.Read(ref reader, typeof(AggregationContainer), options);

		public override void Write(Utf8JsonWriter writer, IAggregationContainer value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Cast to concrete type for serialization
			if (value is AggregationContainer concrete)
				ConcreteConverter.Write(writer, concrete, options);
			else
			{
				// Fallback: create an AggregationContainer from the interface
				var container = new AggregationContainer
				{
					AdjacencyMatrix = value.AdjacencyMatrix,
					Aggregations = value.Aggregations,
					Average = value.Average,
					AverageBucket = value.AverageBucket,
					BucketScript = value.BucketScript,
					BucketSelector = value.BucketSelector,
					BucketSort = value.BucketSort,
					Cardinality = value.Cardinality,
					Children = value.Children,
					Composite = value.Composite,
					CumulativeSum = value.CumulativeSum,
					DateHistogram = value.DateHistogram,
					AutoDateHistogram = value.AutoDateHistogram,
					DateRange = value.DateRange,
					Derivative = value.Derivative,
					DiversifiedSampler = value.DiversifiedSampler,
					ExtendedStats = value.ExtendedStats,
					ExtendedStatsBucket = value.ExtendedStatsBucket,
					Filter = value.Filter,
					Filters = value.Filters,
					GeoBounds = value.GeoBounds,
					GeoCentroid = value.GeoCentroid,
					GeoDistance = value.GeoDistance,
					GeoHash = value.GeoHash,
					GeoLine = value.GeoLine,
					GeoTile = value.GeoTile,
					Global = value.Global,
					Histogram = value.Histogram,
					IpRange = value.IpRange,
					MatrixStats = value.MatrixStats,
					Max = value.Max,
					MaxBucket = value.MaxBucket,
					Meta = value.Meta,
					Min = value.Min,
					MinBucket = value.MinBucket,
					Missing = value.Missing,
					MovingAverage = value.MovingAverage,
					MovingFunction = value.MovingFunction,
					Nested = value.Nested,
					Parent = value.Parent,
					PercentileRanks = value.PercentileRanks,
					Percentiles = value.Percentiles,
					PercentilesBucket = value.PercentilesBucket,
					Range = value.Range,
					RareTerms = value.RareTerms,
					ReverseNested = value.ReverseNested,
					Sampler = value.Sampler,
					ScriptedMetric = value.ScriptedMetric,
					SerialDifferencing = value.SerialDifferencing,
					SignificantTerms = value.SignificantTerms,
					SignificantText = value.SignificantText,
					Stats = value.Stats,
					StatsBucket = value.StatsBucket,
					Sum = value.Sum,
					SumBucket = value.SumBucket,
					Terms = value.Terms,
					TopHits = value.TopHits,
					ValueCount = value.ValueCount,
					WeightedAverage = value.WeightedAverage,
					MedianAbsoluteDeviation = value.MedianAbsoluteDeviation,
					MultiTerms = value.MultiTerms,
					VariableWidthHistogram = value.VariableWidthHistogram
				};
				ConcreteConverter.Write(writer, container, options);
			}
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="AggregationDictionary"/> that handles
	/// it as a dictionary of string keys to <see cref="IAggregationContainer"/> values.
	/// </summary>
	internal sealed class AggregationDictionaryConverter : JsonConverter<AggregationDictionary>
	{
		public override AggregationDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var dictionary = new Dictionary<string, IAggregationContainer>();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var key = reader.GetString();
				reader.Read();

				var container = JsonSerializer.Deserialize<AggregationContainer>(ref reader, options);
				if (container != null)
					dictionary[key] = container;
			}

			return new AggregationDictionary(dictionary);
		}

		public override void Write(Utf8JsonWriter writer, AggregationDictionary value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var backing = (IDictionary<string, IAggregationContainer>)value;

			writer.WriteStartObject();

			foreach (var kvp in backing)
			{
				if (kvp.Value == null)
					continue;

				writer.WritePropertyName(kvp.Key);
				JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
			}

			writer.WriteEndObject();
		}
	}
}
