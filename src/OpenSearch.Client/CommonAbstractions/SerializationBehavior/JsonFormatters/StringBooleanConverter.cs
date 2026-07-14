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
	/// Deserializes a JSON boolean OR a JSON string (<c>"true"</c>/<c>"false"</c>) into a
	/// <see cref="Nullable{Boolean}"/>, and serializes it back as a JSON boolean.
	/// Mirrors the historical (Utf8Json) <c>NullableStringBooleanFormatter</c>: OpenSearch frequently
	/// returns repository settings booleans (e.g. <c>compress</c>) as strings.
	/// </summary>
	internal sealed class NullableStringBooleanConverter : JsonConverter<bool?>
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
					var s = reader.GetString();
					if (bool.TryParse(s, out var b))
						return b;
					throw new JsonException($"Cannot parse {typeof(bool).FullName} from: {s}");
				default:
					throw new JsonException($"Cannot parse {typeof(bool).FullName} from: {reader.TokenType}");
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
