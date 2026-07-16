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
	/// A <see cref="JsonConverter{T}"/> for weakly-typed (<c>object</c>) query/document values that
	/// must be written through the configured source serializer when the value is a user type
	/// (e.g. an enum, POCO), but through the built-in high-level converters when the value is an
	/// OpenSearch.Client type.
	/// <para>
	/// This is the System.Text.Json equivalent of the Utf8Json <c>SourceWriteFormatter&lt;T&gt;</c>.
	/// It is applied via the <see cref="JsonConverterAttribute"/> attribute channel so it takes effect
	/// even when the value sits on a contract rebuilt by <see cref="InterfaceDataContractModifier"/> or
	/// nested inside another converter, where options-registered factories are skipped.
	/// </para>
	/// <para>
	/// Serialization dispatches on the runtime type: the <see cref="SourceConverterFactory"/> registered
	/// on the main options already routes non-OpenSearch types to the source serializer, so serializing
	/// with the runtime type and the ambient options produces the correct wire format for both the
	/// default and a custom source serializer.
	/// </para>
	/// </summary>
	internal sealed class SourceValueWriteConverter : JsonConverter<object>
	{
		public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Read into a loosely-typed representation; term/prefix/etc. values are scalars or arrays.
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return reader.GetString();
				case JsonTokenType.Number:
					// Note: avoid the ternary `ok ? l : GetDouble()` — C# unifies both branches to
					// double, silently boxing integral values as double.
					if (reader.TryGetInt64(out var l))
						return l;
					return reader.GetDouble();
				case JsonTokenType.True:
					return true;
				case JsonTokenType.False:
					return false;
				default:
					using (var doc = JsonDocument.ParseValue(ref reader))
						return doc.RootElement.Clone();
			}
		}

		public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
