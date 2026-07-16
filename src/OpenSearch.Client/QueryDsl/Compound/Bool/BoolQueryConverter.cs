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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IBoolQuery"/> that handles
	/// the bool query structure with must, must_not, should, filter clauses and
	/// minimum_should_match.
	/// <para>
	/// Example JSON shape:
	/// <code>
	/// {
	///   "_name": "named_query",
	///   "boost": 1.1,
	///   "must": [ { ... } ],
	///   "must_not": [ { ... } ],
	///   "should": [ { ... } ],
	///   "filter": [ { ... } ],
	///   "minimum_should_match": 1
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class BoolQueryConverter : JsonConverter<IBoolQuery>
	{
		private static readonly JsonEncodedText MustProp = JsonEncodedText.Encode("must");
		private static readonly JsonEncodedText MustNotProp = JsonEncodedText.Encode("must_not");
		private static readonly JsonEncodedText ShouldProp = JsonEncodedText.Encode("should");
		private static readonly JsonEncodedText FilterProp = JsonEncodedText.Encode("filter");
		private static readonly JsonEncodedText MinimumShouldMatchProp = JsonEncodedText.Encode("minimum_should_match");
		private static readonly JsonEncodedText BoostProp = JsonEncodedText.Encode("boost");
		private static readonly JsonEncodedText NameProp = JsonEncodedText.Encode("_name");

		public override IBoolQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new BoolQuery();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to value

				switch (propertyName)
				{
					case "_name":
						query.Name = reader.GetString();
						break;
					case "boost":
						query.Boost = reader.GetDouble();
						break;
					case "must":
						query.Must = JsonSerializer.Deserialize<IEnumerable<QueryContainer>>(ref reader, options);
						break;
					case "must_not":
						query.MustNot = JsonSerializer.Deserialize<IEnumerable<QueryContainer>>(ref reader, options);
						break;
					case "should":
						query.Should = JsonSerializer.Deserialize<IEnumerable<QueryContainer>>(ref reader, options);
						break;
					case "filter":
						query.Filter = JsonSerializer.Deserialize<IEnumerable<QueryContainer>>(ref reader, options);
						break;
					case "minimum_should_match":
						query.MinimumShouldMatch = JsonSerializer.Deserialize<MinimumShouldMatch>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IBoolQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName(NameProp);
				writer.WriteStringValue(value.Name);
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName(BoostProp);
				JsonSerializer.Serialize(writer, value.Boost.Value, options);
			}

			if (value.ShouldSerializeMust())
			{
				writer.WritePropertyName(MustProp);
				JsonSerializer.Serialize(writer, value.Must, options);
			}

			if (value.ShouldSerializeMustNot())
			{
				writer.WritePropertyName(MustNotProp);
				JsonSerializer.Serialize(writer, value.MustNot, options);
			}

			if (value.ShouldSerializeShould())
			{
				writer.WritePropertyName(ShouldProp);
				JsonSerializer.Serialize(writer, value.Should, options);
			}

			if (value.ShouldSerializeFilter())
			{
				writer.WritePropertyName(FilterProp);
				JsonSerializer.Serialize(writer, value.Filter, options);
			}

			if (value.MinimumShouldMatch != null)
			{
				writer.WritePropertyName(MinimumShouldMatchProp);
				JsonSerializer.Serialize(writer, value.MinimumShouldMatch, options);
			}

			writer.WriteEndObject();
		}
	}
}
