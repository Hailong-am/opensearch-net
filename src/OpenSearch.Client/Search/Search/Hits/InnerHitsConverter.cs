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
	/// A <see cref="JsonConverter{T}"/> for <see cref="InnerHitsMetadata"/> that handles the
	/// inner hits metadata envelope containing total, max_score, and hits array.
	/// Inner hits use <see cref="ILazyDocument"/> for the document type since the concrete
	/// type is not known at deserialization time.
	/// Replaces the Utf8Json-based <c>InnerHitsFormatter</c>.
	/// </summary>
	internal sealed class InnerHitsMetadataConverter : JsonConverter<InnerHitsMetadata>
	{
		private static readonly JsonEncodedText TotalProp = JsonEncodedText.Encode("total");
		private static readonly JsonEncodedText MaxScoreProp = JsonEncodedText.Encode("max_score");
		private static readonly JsonEncodedText HitsProp = JsonEncodedText.Encode("hits");

		public override InnerHitsMetadata Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var metadata = new InnerHitsMetadata();

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

		public override void Write(Utf8JsonWriter writer, InnerHitsMetadata value, JsonSerializerOptions options)
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

		private static List<IHit<ILazyDocument>> ReadHitsArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return new List<IHit<ILazyDocument>>();

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return new List<IHit<ILazyDocument>>();
			}

			var hits = new List<IHit<ILazyDocument>>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				var hit = JsonSerializer.Deserialize<IHit<ILazyDocument>>(ref reader, options);
				if (hit != null)
					hits.Add(hit);
			}

			return hits;
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="InnerHitsResult"/> that reads/writes
	/// the wrapper object containing the inner hits metadata.
	/// </summary>
	internal sealed class InnerHitsResultConverter : JsonConverter<InnerHitsResult>
	{
		private static readonly JsonEncodedText HitsProp = JsonEncodedText.Encode("hits");

		public override InnerHitsResult Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var result = new InnerHitsResult();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals(HitsProp.EncodedUtf8Bytes))
				{
					reader.Read();
					result.Hits = JsonSerializer.Deserialize<InnerHitsMetadata>(ref reader, options);
				}
				else
				{
					reader.Read();
					reader.Skip();
				}
			}

			return result;
		}

		public override void Write(Utf8JsonWriter writer, InnerHitsResult value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (value.Hits != null)
			{
				writer.WritePropertyName(HitsProp);
				JsonSerializer.Serialize(writer, value.Hits, options);
			}

			writer.WriteEndObject();
		}
	}
}
