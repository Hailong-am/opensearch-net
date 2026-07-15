/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for <see cref="SearchResponse{TDocument}"/> and
	/// <see cref="ISearchResponse{TDocument}"/>.
	/// Delegates to the standard STJ deserialization with DataMember support but ensures
	/// the generic type argument is properly resolved for hits and suggest handling.
	/// </summary>
	internal sealed class SearchResponseConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType) return false;

			var genericDef = typeToConvert.GetGenericTypeDefinition();
			return genericDef == typeof(SearchResponse<>) || genericDef == typeof(ISearchResponse<>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var docType = type.IsGenericType
					? type.GetGenericArguments()[0]
					: typeof(object);

				var converterType = typeof(SearchResponseConverter<>).MakeGenericType(docType);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="SearchResponse{TDocument}"/>
	/// that handles all search response properties including hits metadata, aggregations,
	/// suggest results, and other metadata fields.
	/// Replaces the Utf8Json-based <c>SearchResponseFormatter</c>.
	/// </summary>
	internal sealed class SearchResponseConverter<TDocument> : JsonConverter<SearchResponse<TDocument>>
		where TDocument : class
	{
		private static readonly JsonEncodedText TookProp = JsonEncodedText.Encode("took");
		private static readonly JsonEncodedText TimedOutProp = JsonEncodedText.Encode("timed_out");
		private static readonly JsonEncodedText TerminatedEarlyProp = JsonEncodedText.Encode("terminated_early");
		private static readonly JsonEncodedText ShardsProp = JsonEncodedText.Encode("_shards");
		private static readonly JsonEncodedText HitsProp = JsonEncodedText.Encode("hits");
		private static readonly JsonEncodedText AggregationsProp = JsonEncodedText.Encode("aggregations");
		private static readonly JsonEncodedText SuggestProp = JsonEncodedText.Encode("suggest");
		private static readonly JsonEncodedText ProfileProp = JsonEncodedText.Encode("profile");
		private static readonly JsonEncodedText ScrollIdProp = JsonEncodedText.Encode("_scroll_id");
		private static readonly JsonEncodedText ClustersProp = JsonEncodedText.Encode("_clusters");
		private static readonly JsonEncodedText NumReducePhasesProp = JsonEncodedText.Encode("num_reduce_phases");
		private static readonly JsonEncodedText ErrorProp = JsonEncodedText.Encode("error");
		private static readonly JsonEncodedText StatusProp = JsonEncodedText.Encode("status");

		public override SearchResponse<TDocument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var response = new SearchResponse<TDocument>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals(TookProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Took = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(TimedOutProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.TimedOut = reader.GetBoolean();
				}
				else if (reader.ValueTextEquals(TerminatedEarlyProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.TerminatedEarly = reader.GetBoolean();
				}
				else if (reader.ValueTextEquals(ShardsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Shards = JsonSerializer.Deserialize<ShardStatistics>(ref reader, options);
				}
				else if (reader.ValueTextEquals(HitsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.HitsMetadata = JsonSerializer.Deserialize<IHitsMetadata<TDocument>>(ref reader, options);
				}
				else if (reader.ValueTextEquals(AggregationsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Aggregations = JsonSerializer.Deserialize<AggregateDictionary>(ref reader, options);
				}
				else if (reader.ValueTextEquals(SuggestProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Suggest = JsonSerializer.Deserialize<ISuggestDictionary<TDocument>>(ref reader, options);
				}
				else if (reader.ValueTextEquals(ProfileProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Profile = JsonSerializer.Deserialize<Profile>(ref reader, options);
				}
				else if (reader.ValueTextEquals(ScrollIdProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.ScrollId = reader.GetString();
				}
				else if (reader.ValueTextEquals(ClustersProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Clusters = JsonSerializer.Deserialize<ClusterStatistics>(ref reader, options);
				}
				else if (reader.ValueTextEquals(NumReducePhasesProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.NumberOfReducePhases = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(ErrorProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.Error = JsonSerializer.Deserialize<OpenSearch.Net.Error>(ref reader, options);
				}
				else if (reader.ValueTextEquals(StatusProp.EncodedUtf8Bytes))
				{
					reader.Read();
					response.StatusCode = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
				}
				else
				{
					// Skip unknown properties
					reader.Read();
					reader.Skip();
				}
			}

			return response;
		}

		public override void Write(Utf8JsonWriter writer, SearchResponse<TDocument> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			writer.WritePropertyName(TookProp);
			writer.WriteNumberValue(value.Took);

			writer.WritePropertyName(TimedOutProp);
			writer.WriteBooleanValue(value.TimedOut);

			if (value.TerminatedEarly)
			{
				writer.WritePropertyName(TerminatedEarlyProp);
				writer.WriteBooleanValue(value.TerminatedEarly);
			}

			if (value.Shards != null)
			{
				writer.WritePropertyName(ShardsProp);
				JsonSerializer.Serialize(writer, value.Shards, options);
			}

			if (value.HitsMetadata != null)
			{
				writer.WritePropertyName(HitsProp);
				JsonSerializer.Serialize(writer, value.HitsMetadata, options);
			}

			if (value.Aggregations != null && value.Aggregations != AggregateDictionary.Default)
			{
				writer.WritePropertyName(AggregationsProp);
				JsonSerializer.Serialize(writer, value.Aggregations, options);
			}

			if (value.Suggest != null && value.Suggest != SuggestDictionary<TDocument>.Default)
			{
				writer.WritePropertyName(SuggestProp);
				JsonSerializer.Serialize(writer, value.Suggest, options);
			}

			if (value.Profile != null)
			{
				writer.WritePropertyName(ProfileProp);
				JsonSerializer.Serialize(writer, value.Profile, options);
			}

			if (value.ScrollId != null)
			{
				writer.WritePropertyName(ScrollIdProp);
				writer.WriteStringValue(value.ScrollId);
			}

			if (value.Clusters != null)
			{
				writer.WritePropertyName(ClustersProp);
				JsonSerializer.Serialize(writer, value.Clusters, options);
			}

			if (value.NumberOfReducePhases != 0)
			{
				writer.WritePropertyName(NumReducePhasesProp);
				writer.WriteNumberValue(value.NumberOfReducePhases);
			}

			writer.WriteEndObject();
		}
	}
}
