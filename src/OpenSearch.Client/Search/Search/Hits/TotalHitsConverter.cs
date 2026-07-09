/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="TotalHits"/> that handles two formats:
	/// <list type="bullet">
	/// <item>Legacy format: a simple number (e.g. <c>42</c>)</item>
	/// <item>Object format: <c>{"value": 42, "relation": "eq"}</c></item>
	/// </list>
	/// Replaces the Utf8Json-based <c>TotalHitsFormatter</c>.
	/// </summary>
	internal sealed class TotalHitsConverter : JsonConverter<TotalHits>
	{
		private static readonly JsonEncodedText ValueProp = JsonEncodedText.Encode("value");
		private static readonly JsonEncodedText RelationProp = JsonEncodedText.Encode("relation");

		public override TotalHits Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
			{
				reader.Read();
				return null;
			}

			if (reader.TokenType == JsonTokenType.Number)
			{
				// Legacy format: just a number
				return new TotalHits { Value = reader.GetInt64() };
			}

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			long value = -1;
			TotalHitsRelation? relation = null;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName) continue;

				if (reader.ValueTextEquals(ValueProp.EncodedUtf8Bytes))
				{
					reader.Read();
					value = reader.GetInt64();
				}
				else if (reader.ValueTextEquals(RelationProp.EncodedUtf8Bytes))
				{
					reader.Read();
					relation = JsonSerializer.Deserialize<TotalHitsRelation?>(ref reader, options);
				}
				else
				{
					reader.Read();
					reader.Skip();
				}
			}

			return new TotalHits { Value = value, Relation = relation };
		}

		public override void Write(Utf8JsonWriter writer, TotalHits value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.Relation.HasValue)
			{
				writer.WriteStartObject();
				writer.WriteNumber(ValueProp, value.Value);
				writer.WritePropertyName(RelationProp);
				JsonSerializer.Serialize(writer, value.Relation.Value, options);
				writer.WriteEndObject();
			}
			else
			{
				writer.WriteNumberValue(value.Value);
			}
		}
	}
}
