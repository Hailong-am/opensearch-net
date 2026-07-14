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
	internal sealed class TermsExcludeConverter : JsonConverter<TermsExclude>
	{
		public override TermsExclude Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					reader.Skip();
					return null;
				case JsonTokenType.StartArray:
					var values = JsonSerializer.Deserialize<IEnumerable<string>>(ref reader, options);
					return new TermsExclude(values);
				case JsonTokenType.String:
					return new TermsExclude(reader.GetString());
				default:
					throw new JsonException($"Unexpected token {reader.TokenType} when deserializing {nameof(TermsExclude)}");
			}
		}

		public override void Write(Utf8JsonWriter writer, TermsExclude value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else if (value.Values != null)
				JsonSerializer.Serialize(writer, value.Values, options);
			else
				writer.WriteStringValue(value.Pattern);
		}
	}
}
