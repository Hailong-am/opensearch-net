/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Deserializes a JSON number OR string into a <see cref="string"/>, and serializes a string back
	/// as a JSON number. Mirrors the historical (Utf8Json) <c>IntStringFormatter</c>: some OpenSearch
	/// responses return integer-valued identifiers (e.g. <c>shard_id</c>) as bare numbers even though
	/// the client models them as strings.
	/// </summary>
	internal sealed class IntStringConverter : JsonConverter<string>
	{
		public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.Number:
					return reader.GetInt64().ToString(CultureInfo.InvariantCulture);
				case JsonTokenType.String:
					return reader.GetString();
				default:
					throw new JsonException($"expected string or int but found {reader.TokenType}");
			}
		}

		public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
				writer.WriteNumberValue(i);
			else
				throw new InvalidOperationException($"expected a int string value, but found {value}");
		}
	}
}
