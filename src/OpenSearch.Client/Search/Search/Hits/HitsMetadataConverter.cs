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
	/// A <see cref="JsonConverterFactory"/> that creates converters for
	/// <see cref="IHitsMetadata{T}"/> and <see cref="HitsMetadata{T}"/>.
	/// Handles the hits envelope containing total, max_score, and the hits array.
	/// </summary>
	internal sealed class HitsMetadataConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType) return false;

			var genericDef = typeToConvert.GetGenericTypeDefinition();
			return genericDef == typeof(IHitsMetadata<>) || genericDef == typeof(HitsMetadata<>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var docType = type.IsGenericType
					? type.GetGenericArguments()[0]
					: typeof(object);

				var converterType = typeof(HitsMetadataConverter<>).MakeGenericType(docType);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IHitsMetadata{TDocument}"/>
	/// that reads the hits envelope: <c>{"total": ..., "max_score": ..., "hits": [...]}</c>.
	/// </summary>
	internal sealed class HitsMetadataConverter<TDocument> : JsonConverter<IHitsMetadata<TDocument>>
		where TDocument : class
	{
		private static readonly JsonEncodedText TotalProp = JsonEncodedText.Encode("total");
		private static readonly JsonEncodedText MaxScoreProp = JsonEncodedText.Encode("max_score");
		private static readonly JsonEncodedText HitsProp = JsonEncodedText.Encode("hits");

		public override IHitsMetadata<TDocument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var metadata = new HitsMetadata<TDocument>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals(TotalProp.EncodedUtf8Bytes))
				{
					reader.Read();
					metadata.Total = JsonSerializer.Deserialize<TotalHits>(ref reader, options);
				}
				else if (reader.ValueTextEquals(MaxScoreProp.EncodedUtf8Bytes))
				{
					reader.Read();
					if (reader.TokenType == JsonTokenType.Null)
						metadata.MaxScore = null;
					else if (reader.TokenType == JsonTokenType.Number)
						metadata.MaxScore = reader.GetDouble();
					else
					{
						reader.Skip();
						metadata.MaxScore = null;
					}
				}
				else if (reader.ValueTextEquals(HitsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					metadata.Hits = ReadHitsArray(ref reader, options);
				}
				else
				{
					reader.Read();
					reader.Skip();
				}
			}

			return metadata;
		}

		public override void Write(Utf8JsonWriter writer, IHitsMetadata<TDocument> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (value.Total != null)
			{
				writer.WritePropertyName(TotalProp);
				JsonSerializer.Serialize(writer, value.Total, options);
			}

			if (value.MaxScore.HasValue)
			{
				writer.WritePropertyName(MaxScoreProp);
				writer.WriteNumberValue(value.MaxScore.Value);
			}

			writer.WritePropertyName(HitsProp);
			writer.WriteStartArray();
			if (value.Hits != null)
			{
				foreach (var hit in value.Hits)
					JsonSerializer.Serialize(writer, hit, options);
			}
			writer.WriteEndArray();

			writer.WriteEndObject();
		}

		private static IReadOnlyCollection<IHit<TDocument>> ReadHitsArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return EmptyReadOnly<IHit<TDocument>>.Collection;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return EmptyReadOnly<IHit<TDocument>>.Collection;
			}

			var hits = new List<IHit<TDocument>>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				var hit = JsonSerializer.Deserialize<IHit<TDocument>>(ref reader, options);
				if (hit != null)
					hits.Add(hit);
			}

			return hits;
		}
	}
}
