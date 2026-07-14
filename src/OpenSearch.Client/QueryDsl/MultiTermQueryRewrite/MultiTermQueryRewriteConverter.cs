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
	/// A <see cref="JsonConverter{T}"/> for <see cref="MultiTermQueryRewrite"/> that handles
	/// string-based serialization/deserialization of rewrite parameter values.
	/// <para>
	/// Example JSON values: <c>"constant_score"</c>, <c>"scoring_boolean"</c>,
	/// <c>"top_terms_boost_N"</c>, <c>"top_terms_N"</c>
	/// </para>
	/// </summary>
	internal sealed class MultiTermQueryRewriteConverter : JsonConverter<MultiTermQueryRewrite>
	{
		public override MultiTermQueryRewrite Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.String)
			{
				reader.Skip();
				return null;
			}

			var value = reader.GetString();
			return MultiTermQueryRewrite.Create(value);
		}

		public override void Write(Utf8JsonWriter writer, MultiTermQueryRewrite value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				writer.WriteStringValue(value.ToString());
		}
	}
}
