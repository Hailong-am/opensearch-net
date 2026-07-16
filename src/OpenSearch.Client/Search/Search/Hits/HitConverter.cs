/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> that creates <see cref="HitConverter{TDocument}"/>
	/// instances for any <see cref="IHit{TDocument}"/> type.
	/// Replaces the Utf8Json-based <c>HitFormatter</c>.
	/// </summary>
	internal sealed class HitConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType) return false;

			var genericDef = typeToConvert.GetGenericTypeDefinition();
			return genericDef == typeof(IHit<>) || genericDef == typeof(Hit<>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var docType = type.IsGenericType
					? type.GetGenericArguments()[0]
					: typeof(object);

				var converterType = typeof(HitConverter<>).MakeGenericType(docType);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IHit{TDocument}"/> / <see cref="Hit{TDocument}"/>
	/// that handles all hit properties including <c>_source</c> delegation to the SourceConverter,
	/// inner hits, highlights, fields, sorts, and matched queries.
	/// </summary>
	internal sealed class HitConverter<TDocument> : JsonConverter<IHit<TDocument>>
		where TDocument : class
	{
		private static readonly JsonEncodedText IdProp = JsonEncodedText.Encode("_id");
		private static readonly JsonEncodedText IndexProp = JsonEncodedText.Encode("_index");
		private static readonly JsonEncodedText TypeProp = JsonEncodedText.Encode("_type");
		private static readonly JsonEncodedText ScoreProp = JsonEncodedText.Encode("_score");
		private static readonly JsonEncodedText SourceProp = JsonEncodedText.Encode("_source");
		private static readonly JsonEncodedText VersionProp = JsonEncodedText.Encode("_version");
		private static readonly JsonEncodedText RoutingProp = JsonEncodedText.Encode("_routing");
		private static readonly JsonEncodedText PrimaryTermProp = JsonEncodedText.Encode("_primary_term");
		private static readonly JsonEncodedText SeqNoProp = JsonEncodedText.Encode("_seq_no");
		private static readonly JsonEncodedText ExplanationProp = JsonEncodedText.Encode("_explanation");
		private static readonly JsonEncodedText FieldsProp = JsonEncodedText.Encode("fields");
		private static readonly JsonEncodedText HighlightProp = JsonEncodedText.Encode("highlight");
		private static readonly JsonEncodedText InnerHitsProp = JsonEncodedText.Encode("inner_hits");
		private static readonly JsonEncodedText NestedProp = JsonEncodedText.Encode("_nested");
		private static readonly JsonEncodedText MatchedQueriesProp = JsonEncodedText.Encode("matched_queries");
		private static readonly JsonEncodedText SortProp = JsonEncodedText.Encode("sort");

		public override IHit<TDocument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var hit = new Hit<TDocument>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals(IdProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Id = reader.GetString();
				}
				else if (reader.ValueTextEquals(IndexProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Index = reader.GetString();
				}
				else if (reader.ValueTextEquals(TypeProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Type = reader.GetString();
				}
				else if (reader.ValueTextEquals(ScoreProp.EncodedUtf8Bytes))
				{
					reader.Read();
					if (reader.TokenType == JsonTokenType.Null)
						hit.Score = null;
					else
						hit.Score = reader.GetDouble();
				}
				else if (reader.ValueTextEquals(SourceProp.EncodedUtf8Bytes))
				{
					reader.Read();
					// Delegate _source deserialization to SourceConverter or STJ default for TDocument
					hit.Source = JsonSerializer.Deserialize<TDocument>(ref reader, options);
				}
				else if (reader.ValueTextEquals(VersionProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Version = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(RoutingProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Routing = reader.GetString();
				}
				else if (reader.ValueTextEquals(PrimaryTermProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.PrimaryTerm = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(SeqNoProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.SequenceNumber = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(ExplanationProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Explanation = JsonSerializer.Deserialize<Explanation>(ref reader, options);
				}
				else if (reader.ValueTextEquals(FieldsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Fields = JsonSerializer.Deserialize<FieldValues>(ref reader, options);
				}
				else if (reader.ValueTextEquals(HighlightProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Highlight = JsonSerializer.Deserialize<IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(ref reader, options);
				}
				else if (reader.ValueTextEquals(InnerHitsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.InnerHits = JsonSerializer.Deserialize<IReadOnlyDictionary<string, InnerHitsResult>>(ref reader, options);
				}
				else if (reader.ValueTextEquals(NestedProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Nested = JsonSerializer.Deserialize<NestedIdentity>(ref reader, options);
				}
				else if (reader.ValueTextEquals(MatchedQueriesProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.MatchedQueries = JsonSerializer.Deserialize<IReadOnlyCollection<string>>(ref reader, options);
				}
				else if (reader.ValueTextEquals(SortProp.EncodedUtf8Bytes))
				{
					reader.Read();
					hit.Sorts = ReadSortValues(ref reader);
				}
				else
				{
					// Skip unknown properties
					reader.Read();
					reader.Skip();
				}
			}

			return hit;
		}

		public override void Write(Utf8JsonWriter writer, IHit<TDocument> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (value.Index != null)
			{
				writer.WritePropertyName(IndexProp);
				writer.WriteStringValue(value.Index);
			}

			if (value.Type != null)
			{
				writer.WritePropertyName(TypeProp);
				writer.WriteStringValue(value.Type);
			}

			if (value.Id != null)
			{
				writer.WritePropertyName(IdProp);
				writer.WriteStringValue(value.Id);
			}

			if (value.Score.HasValue)
			{
				writer.WritePropertyName(ScoreProp);
				writer.WriteNumberValue(value.Score.Value);
			}

			if (value.Source != null)
			{
				writer.WritePropertyName(SourceProp);
				JsonSerializer.Serialize(writer, value.Source, options);
			}

			if (value.Version != 0)
			{
				writer.WritePropertyName(VersionProp);
				writer.WriteNumberValue(value.Version);
			}

			if (value.Routing != null)
			{
				writer.WritePropertyName(RoutingProp);
				writer.WriteStringValue(value.Routing);
			}

			if (value.PrimaryTerm.HasValue)
			{
				writer.WritePropertyName(PrimaryTermProp);
				writer.WriteNumberValue(value.PrimaryTerm.Value);
			}

			if (value.SequenceNumber.HasValue)
			{
				writer.WritePropertyName(SeqNoProp);
				writer.WriteNumberValue(value.SequenceNumber.Value);
			}

			if (value.Explanation != null)
			{
				writer.WritePropertyName(ExplanationProp);
				JsonSerializer.Serialize(writer, value.Explanation, options);
			}

			if (value.Fields != null)
			{
				writer.WritePropertyName(FieldsProp);
				JsonSerializer.Serialize(writer, value.Fields, options);
			}

			if (value.Highlight != null && value.Highlight.Count > 0)
			{
				writer.WritePropertyName(HighlightProp);
				JsonSerializer.Serialize(writer, value.Highlight, options);
			}

			if (value.InnerHits != null && value.InnerHits.Count > 0)
			{
				writer.WritePropertyName(InnerHitsProp);
				JsonSerializer.Serialize(writer, value.InnerHits, options);
			}

			if (value.Nested != null)
			{
				writer.WritePropertyName(NestedProp);
				JsonSerializer.Serialize(writer, value.Nested, options);
			}

			if (value.MatchedQueries != null && value.MatchedQueries.Count > 0)
			{
				writer.WritePropertyName(MatchedQueriesProp);
				JsonSerializer.Serialize(writer, value.MatchedQueries, options);
			}

			if (value.Sorts != null && value.Sorts.Count > 0)
			{
				writer.WritePropertyName(SortProp);
				WriteSortValues(writer, value.Sorts);
			}

			writer.WriteEndObject();
		}

		private static IReadOnlyCollection<object> ReadSortValues(ref Utf8JsonReader reader)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return EmptyReadOnly<object>.Collection;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return EmptyReadOnly<object>.Collection;
			}

			var sorts = new List<object>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				switch (reader.TokenType)
				{
					case JsonTokenType.String:
						sorts.Add(reader.GetString());
						break;
					case JsonTokenType.Number:
						if (reader.TryGetInt64(out var l))
							sorts.Add(l);
						else
							sorts.Add(reader.GetDouble());
						break;
					case JsonTokenType.True:
						sorts.Add(true);
						break;
					case JsonTokenType.False:
						sorts.Add(false);
						break;
					case JsonTokenType.Null:
						sorts.Add(null);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return sorts;
		}

		private static void WriteSortValues(Utf8JsonWriter writer, IReadOnlyCollection<object> sorts)
		{
			writer.WriteStartArray();
			foreach (var sort in sorts)
			{
				switch (sort)
				{
					case null:
						writer.WriteNullValue();
						break;
					case string s:
						writer.WriteStringValue(s);
						break;
					case int i:
						writer.WriteNumberValue(i);
						break;
					case long l:
						writer.WriteNumberValue(l);
						break;
					case double d:
						writer.WriteNumberValue(d);
						break;
					case float f:
						writer.WriteNumberValue(f);
						break;
					case bool b:
						writer.WriteBooleanValue(b);
						break;
					default:
						writer.WriteStringValue(sort.ToString());
						break;
				}
			}
			writer.WriteEndArray();
		}
	}
}
