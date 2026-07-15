/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IQueryContainer"/> that handles
	/// the single-key object pattern used by OpenSearch Query DSL.
	/// Each query is serialized as <c>{"query_type": { ... }}</c>.
	/// </summary>
	internal sealed class QueryContainerConverter : JsonConverter<IQueryContainer>
	{
		// Pre-computed property names for frequently used query types
		private static readonly JsonEncodedText BoolProp = JsonEncodedText.Encode("bool");
		private static readonly JsonEncodedText MatchProp = JsonEncodedText.Encode("match");
		private static readonly JsonEncodedText TermProp = JsonEncodedText.Encode("term");
		private static readonly JsonEncodedText TermsProp = JsonEncodedText.Encode("terms");
		private static readonly JsonEncodedText RangeProp = JsonEncodedText.Encode("range");
		private static readonly JsonEncodedText MatchAllProp = JsonEncodedText.Encode("match_all");
		private static readonly JsonEncodedText MatchNoneProp = JsonEncodedText.Encode("match_none");
		private static readonly JsonEncodedText ExistsProp = JsonEncodedText.Encode("exists");
		private static readonly JsonEncodedText NestedProp = JsonEncodedText.Encode("nested");
		private static readonly JsonEncodedText WildcardProp = JsonEncodedText.Encode("wildcard");
		private static readonly JsonEncodedText PrefixProp = JsonEncodedText.Encode("prefix");
		private static readonly JsonEncodedText QueryStringProp = JsonEncodedText.Encode("query_string");
		private static readonly JsonEncodedText FuzzyProp = JsonEncodedText.Encode("fuzzy");
		private static readonly JsonEncodedText RegexpProp = JsonEncodedText.Encode("regexp");
		private static readonly JsonEncodedText IdsProp = JsonEncodedText.Encode("ids");
		private static readonly JsonEncodedText BoostingProp = JsonEncodedText.Encode("boosting");
		private static readonly JsonEncodedText ConstantScoreProp = JsonEncodedText.Encode("constant_score");
		private static readonly JsonEncodedText DisMaxProp = JsonEncodedText.Encode("dis_max");
		private static readonly JsonEncodedText FunctionScoreProp = JsonEncodedText.Encode("function_score");
		private static readonly JsonEncodedText MultiMatchProp = JsonEncodedText.Encode("multi_match");
		private static readonly JsonEncodedText CombinedFieldsProp = JsonEncodedText.Encode("combined_fields");
		private static readonly JsonEncodedText MatchPhraseProp = JsonEncodedText.Encode("match_phrase");
		private static readonly JsonEncodedText MatchPhrasePrefixProp = JsonEncodedText.Encode("match_phrase_prefix");
		private static readonly JsonEncodedText MatchBoolPrefixProp = JsonEncodedText.Encode("match_bool_prefix");
		private static readonly JsonEncodedText MoreLikeThisProp = JsonEncodedText.Encode("more_like_this");
		private static readonly JsonEncodedText SimpleQueryStringProp = JsonEncodedText.Encode("simple_query_string");
		private static readonly JsonEncodedText GeoBoundingBoxProp = JsonEncodedText.Encode("geo_bounding_box");
		private static readonly JsonEncodedText GeoDistanceProp = JsonEncodedText.Encode("geo_distance");
		private static readonly JsonEncodedText GeoPolygonProp = JsonEncodedText.Encode("geo_polygon");
		private static readonly JsonEncodedText GeoShapeProp = JsonEncodedText.Encode("geo_shape");
		private static readonly JsonEncodedText ShapeProp = JsonEncodedText.Encode("shape");
		private static readonly JsonEncodedText HasChildProp = JsonEncodedText.Encode("has_child");
		private static readonly JsonEncodedText HasParentProp = JsonEncodedText.Encode("has_parent");
		private static readonly JsonEncodedText ParentIdProp = JsonEncodedText.Encode("parent_id");
		private static readonly JsonEncodedText PercolateProp = JsonEncodedText.Encode("percolate");
		private static readonly JsonEncodedText ScriptProp = JsonEncodedText.Encode("script");
		private static readonly JsonEncodedText ScriptScoreProp = JsonEncodedText.Encode("script_score");
		private static readonly JsonEncodedText SpanContainingProp = JsonEncodedText.Encode("span_containing");
		private static readonly JsonEncodedText FieldMaskingSpanProp = JsonEncodedText.Encode("field_masking_span");
		private static readonly JsonEncodedText SpanFirstProp = JsonEncodedText.Encode("span_first");
		private static readonly JsonEncodedText SpanMultiProp = JsonEncodedText.Encode("span_multi");
		private static readonly JsonEncodedText SpanNearProp = JsonEncodedText.Encode("span_near");
		private static readonly JsonEncodedText SpanNotProp = JsonEncodedText.Encode("span_not");
		private static readonly JsonEncodedText SpanOrProp = JsonEncodedText.Encode("span_or");
		private static readonly JsonEncodedText SpanTermProp = JsonEncodedText.Encode("span_term");
		private static readonly JsonEncodedText SpanWithinProp = JsonEncodedText.Encode("span_within");
		private static readonly JsonEncodedText TermsSetProp = JsonEncodedText.Encode("terms_set");
		private static readonly JsonEncodedText IntervalsProp = JsonEncodedText.Encode("intervals");
		private static readonly JsonEncodedText RankFeatureProp = JsonEncodedText.Encode("rank_feature");
		private static readonly JsonEncodedText DistanceFeatureProp = JsonEncodedText.Encode("distance_feature");
		private static readonly JsonEncodedText KnnProp = JsonEncodedText.Encode("knn");
		private static readonly JsonEncodedText NeuralProp = JsonEncodedText.Encode("neural");
		private static readonly JsonEncodedText HybridProp = JsonEncodedText.Encode("hybrid");

		public override IQueryContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType == JsonTokenType.String)
			{
				// Handle string-encoded query (raw JSON string containing a query object)
				var raw = reader.GetString();
				var innerReader = new Utf8JsonReader(Encoding.UTF8.GetBytes(raw));
				innerReader.Read();
				return ReadObject(ref innerReader, options);
			}

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			return ReadObject(ref reader, options);
		}

		private static IQueryContainer ReadObject(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var container = new QueryContainer();

			// Expect StartObject
			if (reader.TokenType != JsonTokenType.StartObject)
				return container;

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to the value

				ReadQueryProperty(container, propertyName, ref reader, options);
			}

			return container;
		}

		private static void ReadQueryProperty(QueryContainer container, string propertyName, ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			switch (propertyName)
			{
				case "bool":
					((IQueryContainer)container).Bool = JsonSerializer.Deserialize<IBoolQuery>(ref reader, options);
					break;
				case "boosting":
					((IQueryContainer)container).Boosting = JsonSerializer.Deserialize<IBoostingQuery>(ref reader, options);
					break;
				case "constant_score":
					((IQueryContainer)container).ConstantScore = JsonSerializer.Deserialize<IConstantScoreQuery>(ref reader, options);
					break;
				case "dis_max":
					((IQueryContainer)container).DisMax = JsonSerializer.Deserialize<IDisMaxQuery>(ref reader, options);
					break;
				case "exists":
					((IQueryContainer)container).Exists = JsonSerializer.Deserialize<IExistsQuery>(ref reader, options);
					break;
				case "function_score":
					((IQueryContainer)container).FunctionScore = JsonSerializer.Deserialize<IFunctionScoreQuery>(ref reader, options);
					break;
				case "fuzzy":
					((IQueryContainer)container).Fuzzy = JsonSerializer.Deserialize<IFuzzyQuery>(ref reader, options);
					break;
				case "geo_bounding_box":
					((IQueryContainer)container).GeoBoundingBox = JsonSerializer.Deserialize<IGeoBoundingBoxQuery>(ref reader, options);
					break;
				case "geo_distance":
					((IQueryContainer)container).GeoDistance = JsonSerializer.Deserialize<IGeoDistanceQuery>(ref reader, options);
					break;
				case "geo_polygon":
					((IQueryContainer)container).GeoPolygon = JsonSerializer.Deserialize<IGeoPolygonQuery>(ref reader, options);
					break;
				case "geo_shape":
					((IQueryContainer)container).GeoShape = JsonSerializer.Deserialize<IGeoShapeQuery>(ref reader, options);
					break;
				case "shape":
					((IQueryContainer)container).Shape = JsonSerializer.Deserialize<IShapeQuery>(ref reader, options);
					break;
				case "has_child":
					((IQueryContainer)container).HasChild = JsonSerializer.Deserialize<IHasChildQuery>(ref reader, options);
					break;
				case "has_parent":
					((IQueryContainer)container).HasParent = JsonSerializer.Deserialize<IHasParentQuery>(ref reader, options);
					break;
				case "ids":
					((IQueryContainer)container).Ids = JsonSerializer.Deserialize<IIdsQuery>(ref reader, options);
					break;
				case "intervals":
					((IQueryContainer)container).Intervals = JsonSerializer.Deserialize<IIntervalsQuery>(ref reader, options);
					break;
				case "match":
					((IQueryContainer)container).Match = JsonSerializer.Deserialize<IMatchQuery>(ref reader, options);
					break;
				case "match_all":
					((IQueryContainer)container).MatchAll = JsonSerializer.Deserialize<IMatchAllQuery>(ref reader, options);
					break;
				case "match_bool_prefix":
					((IQueryContainer)container).MatchBoolPrefix = JsonSerializer.Deserialize<IMatchBoolPrefixQuery>(ref reader, options);
					break;
				case "match_none":
					((IQueryContainer)container).MatchNone = JsonSerializer.Deserialize<IMatchNoneQuery>(ref reader, options);
					break;
				case "match_phrase":
					((IQueryContainer)container).MatchPhrase = JsonSerializer.Deserialize<IMatchPhraseQuery>(ref reader, options);
					break;
				case "match_phrase_prefix":
					((IQueryContainer)container).MatchPhrasePrefix = JsonSerializer.Deserialize<IMatchPhrasePrefixQuery>(ref reader, options);
					break;
				case "more_like_this":
					((IQueryContainer)container).MoreLikeThis = JsonSerializer.Deserialize<IMoreLikeThisQuery>(ref reader, options);
					break;
				case "multi_match":
					((IQueryContainer)container).MultiMatch = JsonSerializer.Deserialize<IMultiMatchQuery>(ref reader, options);
					break;
				case "combined_fields":
					((IQueryContainer)container).CombinedFields = JsonSerializer.Deserialize<ICombinedFieldsQuery>(ref reader, options);
					break;
				case "nested":
					((IQueryContainer)container).Nested = JsonSerializer.Deserialize<INestedQuery>(ref reader, options);
					break;
				case "parent_id":
					((IQueryContainer)container).ParentId = JsonSerializer.Deserialize<IParentIdQuery>(ref reader, options);
					break;
				case "percolate":
					((IQueryContainer)container).Percolate = JsonSerializer.Deserialize<IPercolateQuery>(ref reader, options);
					break;
				case "prefix":
					((IQueryContainer)container).Prefix = JsonSerializer.Deserialize<IPrefixQuery>(ref reader, options);
					break;
				case "query_string":
					((IQueryContainer)container).QueryString = JsonSerializer.Deserialize<IQueryStringQuery>(ref reader, options);
					break;
				case "range":
					((IQueryContainer)container).Range = JsonSerializer.Deserialize<IRangeQuery>(ref reader, options);
					break;
				case "regexp":
					((IQueryContainer)container).Regexp = JsonSerializer.Deserialize<IRegexpQuery>(ref reader, options);
					break;
				case "script":
					((IQueryContainer)container).Script = JsonSerializer.Deserialize<IScriptQuery>(ref reader, options);
					break;
				case "script_score":
					((IQueryContainer)container).ScriptScore = JsonSerializer.Deserialize<IScriptScoreQuery>(ref reader, options);
					break;
				case "simple_query_string":
					((IQueryContainer)container).SimpleQueryString = JsonSerializer.Deserialize<ISimpleQueryStringQuery>(ref reader, options);
					break;
				case "span_containing":
					((IQueryContainer)container).SpanContaining = JsonSerializer.Deserialize<ISpanContainingQuery>(ref reader, options);
					break;
				case "field_masking_span":
					((IQueryContainer)container).SpanFieldMasking = JsonSerializer.Deserialize<ISpanFieldMaskingQuery>(ref reader, options);
					break;
				case "span_first":
					((IQueryContainer)container).SpanFirst = JsonSerializer.Deserialize<ISpanFirstQuery>(ref reader, options);
					break;
				case "span_multi":
					((IQueryContainer)container).SpanMultiTerm = JsonSerializer.Deserialize<ISpanMultiTermQuery>(ref reader, options);
					break;
				case "span_near":
					((IQueryContainer)container).SpanNear = JsonSerializer.Deserialize<ISpanNearQuery>(ref reader, options);
					break;
				case "span_not":
					((IQueryContainer)container).SpanNot = JsonSerializer.Deserialize<ISpanNotQuery>(ref reader, options);
					break;
				case "span_or":
					((IQueryContainer)container).SpanOr = JsonSerializer.Deserialize<ISpanOrQuery>(ref reader, options);
					break;
				case "span_term":
					((IQueryContainer)container).SpanTerm = JsonSerializer.Deserialize<ISpanTermQuery>(ref reader, options);
					break;
				case "span_within":
					((IQueryContainer)container).SpanWithin = JsonSerializer.Deserialize<ISpanWithinQuery>(ref reader, options);
					break;
				case "term":
					((IQueryContainer)container).Term = JsonSerializer.Deserialize<ITermQuery>(ref reader, options);
					break;
				case "terms":
					((IQueryContainer)container).Terms = JsonSerializer.Deserialize<ITermsQuery>(ref reader, options);
					break;
				case "terms_set":
					((IQueryContainer)container).TermsSet = JsonSerializer.Deserialize<ITermsSetQuery>(ref reader, options);
					break;
				case "wildcard":
					((IQueryContainer)container).Wildcard = JsonSerializer.Deserialize<IWildcardQuery>(ref reader, options);
					break;
				case "rank_feature":
					((IQueryContainer)container).RankFeature = JsonSerializer.Deserialize<IRankFeatureQuery>(ref reader, options);
					break;
				case "distance_feature":
					((IQueryContainer)container).DistanceFeature = JsonSerializer.Deserialize<IDistanceFeatureQuery>(ref reader, options);
					break;
				case "knn":
					((IQueryContainer)container).Knn = JsonSerializer.Deserialize<IKnnQuery>(ref reader, options);
					break;
				case "neural":
					((IQueryContainer)container).Neural = JsonSerializer.Deserialize<INeuralQuery>(ref reader, options);
					break;
				case "hybrid":
					((IQueryContainer)container).Hybrid = JsonSerializer.Deserialize<IHybridQuery>(ref reader, options);
					break;
				default:
					// Unknown query type — skip the value to maintain forward compatibility
					reader.Skip();
					break;
			}
		}

		public override void Write(Utf8JsonWriter writer, IQueryContainer value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Handle raw query — write the raw JSON directly
			var rawQuery = value.RawQuery;
			if (rawQuery?.Raw != null && !rawQuery.Raw.IsNullOrEmpty() && value.IsWritable)
			{
				writer.WriteRawValue(rawQuery.Raw);
				return;
			}

			writer.WriteStartObject();

			// Write the single query property that is non-null.
			// The QueryContainer can only hold one query at a time.
			if (value.Bool != null)
			{
				writer.WritePropertyName(BoolProp);
				JsonSerializer.Serialize(writer, value.Bool, options);
			}
			else if (value.Boosting != null)
			{
				writer.WritePropertyName(BoostingProp);
				JsonSerializer.Serialize(writer, value.Boosting, options);
			}
			else if (value.ConstantScore != null)
			{
				writer.WritePropertyName(ConstantScoreProp);
				JsonSerializer.Serialize(writer, value.ConstantScore, options);
			}
			else if (value.DisMax != null)
			{
				writer.WritePropertyName(DisMaxProp);
				JsonSerializer.Serialize(writer, value.DisMax, options);
			}
			else if (value.Exists != null)
			{
				writer.WritePropertyName(ExistsProp);
				JsonSerializer.Serialize(writer, value.Exists, options);
			}
			else if (value.FunctionScore != null)
			{
				writer.WritePropertyName(FunctionScoreProp);
				JsonSerializer.Serialize(writer, value.FunctionScore, options);
			}
			else if (value.Fuzzy != null)
			{
				writer.WritePropertyName(FuzzyProp);
				JsonSerializer.Serialize(writer, value.Fuzzy, options);
			}
			else if (value.GeoBoundingBox != null)
			{
				writer.WritePropertyName(GeoBoundingBoxProp);
				JsonSerializer.Serialize(writer, value.GeoBoundingBox, options);
			}
			else if (value.GeoDistance != null)
			{
				writer.WritePropertyName(GeoDistanceProp);
				JsonSerializer.Serialize(writer, value.GeoDistance, options);
			}
			else if (value.GeoPolygon != null)
			{
				writer.WritePropertyName(GeoPolygonProp);
				JsonSerializer.Serialize(writer, value.GeoPolygon, options);
			}
			else if (value.GeoShape != null)
			{
				writer.WritePropertyName(GeoShapeProp);
				JsonSerializer.Serialize(writer, value.GeoShape, options);
			}
			else if (value.Shape != null)
			{
				writer.WritePropertyName(ShapeProp);
				JsonSerializer.Serialize(writer, value.Shape, options);
			}
			else if (value.HasChild != null)
			{
				writer.WritePropertyName(HasChildProp);
				JsonSerializer.Serialize(writer, value.HasChild, options);
			}
			else if (value.HasParent != null)
			{
				writer.WritePropertyName(HasParentProp);
				JsonSerializer.Serialize(writer, value.HasParent, options);
			}
			else if (value.Ids != null)
			{
				writer.WritePropertyName(IdsProp);
				JsonSerializer.Serialize(writer, value.Ids, options);
			}
			else if (value.Intervals != null)
			{
				writer.WritePropertyName(IntervalsProp);
				JsonSerializer.Serialize(writer, value.Intervals, options);
			}
			else if (value.Match != null)
			{
				writer.WritePropertyName(MatchProp);
				JsonSerializer.Serialize(writer, value.Match, options);
			}
			else if (value.MatchAll != null)
			{
				writer.WritePropertyName(MatchAllProp);
				JsonSerializer.Serialize(writer, value.MatchAll, options);
			}
			else if (value.MatchBoolPrefix != null)
			{
				writer.WritePropertyName(MatchBoolPrefixProp);
				JsonSerializer.Serialize(writer, value.MatchBoolPrefix, options);
			}
			else if (value.MatchNone != null)
			{
				writer.WritePropertyName(MatchNoneProp);
				JsonSerializer.Serialize(writer, value.MatchNone, options);
			}
			else if (value.MatchPhrase != null)
			{
				writer.WritePropertyName(MatchPhraseProp);
				JsonSerializer.Serialize(writer, value.MatchPhrase, options);
			}
			else if (value.MatchPhrasePrefix != null)
			{
				writer.WritePropertyName(MatchPhrasePrefixProp);
				JsonSerializer.Serialize(writer, value.MatchPhrasePrefix, options);
			}
			else if (value.MoreLikeThis != null)
			{
				writer.WritePropertyName(MoreLikeThisProp);
				JsonSerializer.Serialize(writer, value.MoreLikeThis, options);
			}
			else if (value.MultiMatch != null)
			{
				writer.WritePropertyName(MultiMatchProp);
				JsonSerializer.Serialize(writer, value.MultiMatch, options);
			}
			else if (value.CombinedFields != null)
			{
				writer.WritePropertyName(CombinedFieldsProp);
				JsonSerializer.Serialize(writer, value.CombinedFields, options);
			}
			else if (value.Nested != null)
			{
				writer.WritePropertyName(NestedProp);
				JsonSerializer.Serialize(writer, value.Nested, options);
			}
			else if (value.ParentId != null)
			{
				writer.WritePropertyName(ParentIdProp);
				JsonSerializer.Serialize(writer, value.ParentId, options);
			}
			else if (value.Percolate != null)
			{
				writer.WritePropertyName(PercolateProp);
				JsonSerializer.Serialize(writer, value.Percolate, options);
			}
			else if (value.Prefix != null)
			{
				writer.WritePropertyName(PrefixProp);
				JsonSerializer.Serialize(writer, value.Prefix, options);
			}
			else if (value.QueryString != null)
			{
				writer.WritePropertyName(QueryStringProp);
				JsonSerializer.Serialize(writer, value.QueryString, options);
			}
			else if (value.Range != null)
			{
				writer.WritePropertyName(RangeProp);
				JsonSerializer.Serialize(writer, value.Range, options);
			}
			else if (value.Regexp != null)
			{
				writer.WritePropertyName(RegexpProp);
				JsonSerializer.Serialize(writer, value.Regexp, options);
			}
			else if (value.Script != null)
			{
				writer.WritePropertyName(ScriptProp);
				JsonSerializer.Serialize(writer, value.Script, options);
			}
			else if (value.ScriptScore != null)
			{
				writer.WritePropertyName(ScriptScoreProp);
				JsonSerializer.Serialize(writer, value.ScriptScore, options);
			}
			else if (value.SimpleQueryString != null)
			{
				writer.WritePropertyName(SimpleQueryStringProp);
				JsonSerializer.Serialize(writer, value.SimpleQueryString, options);
			}
			else if (value.SpanContaining != null)
			{
				writer.WritePropertyName(SpanContainingProp);
				JsonSerializer.Serialize(writer, value.SpanContaining, options);
			}
			else if (value.SpanFieldMasking != null)
			{
				writer.WritePropertyName(FieldMaskingSpanProp);
				JsonSerializer.Serialize(writer, value.SpanFieldMasking, options);
			}
			else if (value.SpanFirst != null)
			{
				writer.WritePropertyName(SpanFirstProp);
				JsonSerializer.Serialize(writer, value.SpanFirst, options);
			}
			else if (value.SpanMultiTerm != null)
			{
				writer.WritePropertyName(SpanMultiProp);
				JsonSerializer.Serialize(writer, value.SpanMultiTerm, options);
			}
			else if (value.SpanNear != null)
			{
				writer.WritePropertyName(SpanNearProp);
				JsonSerializer.Serialize(writer, value.SpanNear, options);
			}
			else if (value.SpanNot != null)
			{
				writer.WritePropertyName(SpanNotProp);
				JsonSerializer.Serialize(writer, value.SpanNot, options);
			}
			else if (value.SpanOr != null)
			{
				writer.WritePropertyName(SpanOrProp);
				JsonSerializer.Serialize(writer, value.SpanOr, options);
			}
			else if (value.SpanTerm != null)
			{
				writer.WritePropertyName(SpanTermProp);
				JsonSerializer.Serialize(writer, value.SpanTerm, options);
			}
			else if (value.SpanWithin != null)
			{
				writer.WritePropertyName(SpanWithinProp);
				JsonSerializer.Serialize(writer, value.SpanWithin, options);
			}
			else if (value.Term != null)
			{
				writer.WritePropertyName(TermProp);
				JsonSerializer.Serialize(writer, value.Term, options);
			}
			else if (value.Terms != null)
			{
				writer.WritePropertyName(TermsProp);
				JsonSerializer.Serialize(writer, value.Terms, options);
			}
			else if (value.TermsSet != null)
			{
				writer.WritePropertyName(TermsSetProp);
				JsonSerializer.Serialize(writer, value.TermsSet, options);
			}
			else if (value.Wildcard != null)
			{
				writer.WritePropertyName(WildcardProp);
				JsonSerializer.Serialize(writer, value.Wildcard, options);
			}
			else if (value.RankFeature != null)
			{
				writer.WritePropertyName(RankFeatureProp);
				JsonSerializer.Serialize(writer, value.RankFeature, options);
			}
			else if (value.DistanceFeature != null)
			{
				writer.WritePropertyName(DistanceFeatureProp);
				JsonSerializer.Serialize(writer, value.DistanceFeature, options);
			}
			else if (value.Knn != null)
			{
				writer.WritePropertyName(KnnProp);
				JsonSerializer.Serialize(writer, value.Knn, options);
			}
			else if (value.Neural != null)
			{
				writer.WritePropertyName(NeuralProp);
				JsonSerializer.Serialize(writer, value.Neural, options);
			}
			else if (value.Hybrid != null)
			{
				writer.WritePropertyName(HybridProp);
				JsonSerializer.Serialize(writer, value.Hybrid, options);
			}

			writer.WriteEndObject();
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="QueryContainer"/> that delegates
	/// to the <see cref="QueryContainerConverter"/> for interface-based serialization
	/// and handles string-encoded queries during deserialization.
	/// </summary>
	internal sealed class QueryContainerConcretConverter : JsonConverter<QueryContainer>
	{
		private static readonly QueryContainerConverter InterfaceConverter = new();

		public override QueryContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var result = InterfaceConverter.Read(ref reader, typeof(IQueryContainer), options);
			return result as QueryContainer;
		}

		public override void Write(Utf8JsonWriter writer, QueryContainer value, JsonSerializerOptions options)
		{
			if (value == null || !value.IsWritable)
			{
				writer.WriteNullValue();
				return;
			}

			InterfaceConverter.Write(writer, value, options);
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for collections of <see cref="QueryContainer"/>
	/// that handles both array and single-object input forms, and filters out non-writable
	/// containers during serialization.
	/// </summary>
	internal sealed class QueryContainerCollectionConverter : JsonConverter<IEnumerable<QueryContainer>>
	{
		private static readonly QueryContainerConverter InterfaceConverter = new();

		public override IEnumerable<QueryContainer> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType == JsonTokenType.StartArray)
			{
				var list = new List<QueryContainer>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					var container = InterfaceConverter.Read(ref reader, typeof(IQueryContainer), options) as QueryContainer;
					if (container != null)
						list.Add(container);
				}
				return list;
			}

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				// Single object form — wrap in a list
				var container = InterfaceConverter.Read(ref reader, typeof(IQueryContainer), options) as QueryContainer;
				return container != null ? new List<QueryContainer> { container } : new List<QueryContainer>();
			}

			reader.Skip();
			return null;
		}

		public override void Write(Utf8JsonWriter writer, IEnumerable<QueryContainer> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();
			foreach (var container in value)
			{
				if (container != null && container.IsWritable)
					InterfaceConverter.Write(writer, container, options);
			}
			writer.WriteEndArray();
		}
	}
}
