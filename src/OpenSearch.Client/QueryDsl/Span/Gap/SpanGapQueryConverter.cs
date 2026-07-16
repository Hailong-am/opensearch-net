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
	/// A <see cref="JsonConverter{T}"/> for <see cref="ISpanGapQuery"/> that handles
	/// the simple single-key object pattern where the key is the field name and the
	/// value is the gap width.
	/// <para>
	/// Example JSON shape:
	/// <code>
	/// { "field_name": 3 }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class SpanGapQueryConverter : JsonConverter<ISpanGapQuery>
	{
		public override ISpanGapQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new SpanGapQuery();

			if (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				query.Field = reader.GetString();
				reader.Read(); // Move to value
				query.Width = reader.GetInt32();
			}

			// Read to end of object
			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, ISpanGapQuery value, JsonSerializerOptions options)
		{
			if (value == null || SpanGapQuery.IsConditionless(value))
			{
				writer.WriteNullValue();
				return;
			}

			var fieldName = value.Field?.ToString();
			if (string.IsNullOrEmpty(fieldName))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(fieldName);
			writer.WriteNumberValue(value.Width.Value);
			writer.WriteEndObject();
		}
	}
}
