/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// A lenient <see cref="JsonConverter{T}"/> for <see cref="bool"/> that accepts both
	/// native JSON booleans (<c>true</c>/<c>false</c>) and their string representations
	/// (<c>"true"</c>/<c>"false"</c>). OpenSearch may return boolean fields as strings
	/// in certain responses (e.g. <c>"expand": "true"</c>).
	/// </summary>
	internal sealed class LenientBoolConverter : JsonConverter<bool>
	{
		public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.True:
					return true;
				case JsonTokenType.False:
					return false;
				case JsonTokenType.String:
					var str = reader.GetString();
					if (bool.TryParse(str, out var result))
						return result;
					throw new JsonException($"Cannot convert string \"{str}\" to Boolean.");
				case JsonTokenType.Number:
					// Some APIs return 0/1 for booleans
					return reader.GetInt32() != 0;
				default:
					throw new JsonException($"Cannot convert token type {reader.TokenType} to Boolean.");
			}
		}

		public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
		{
			writer.WriteBooleanValue(value);
		}
	}

	/// <summary>
	/// A lenient <see cref="JsonConverter{T}"/> for <see cref="Nullable{Boolean}"/> that accepts
	/// native JSON booleans, null, and string representations (<c>"true"</c>/<c>"false"</c>).
	/// </summary>
	internal sealed class NullableLenientBoolConverter : JsonConverter<bool?>
	{
		public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.True:
					return true;
				case JsonTokenType.False:
					return false;
				case JsonTokenType.String:
					var str = reader.GetString();
					if (string.IsNullOrEmpty(str))
						return null;
					if (bool.TryParse(str, out var result))
						return result;
					throw new JsonException($"Cannot convert string \"{str}\" to Boolean.");
				case JsonTokenType.Number:
					return reader.GetInt32() != 0;
				default:
					throw new JsonException($"Cannot convert token type {reader.TokenType} to Boolean.");
			}
		}

		public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				writer.WriteBooleanValue(value.Value);
		}
	}
}
