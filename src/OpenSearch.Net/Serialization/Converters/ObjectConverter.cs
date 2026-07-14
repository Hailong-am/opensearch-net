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

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="object"/> that deserializes JSON values
	/// into .NET primitive types (bool, long, double, string) instead of <see cref="JsonElement"/>.
	/// This ensures that values stored in <c>IDictionary&lt;string, object&gt;</c> and similar
	/// collections are compatible with <see cref="Convert"/> and <see cref="IConvertible"/>.
	/// </summary>
	internal class ObjectConverter : JsonConverter<object>
	{
		public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return DynamicValueConverter.ReadValue(ref reader, options);
		}

		public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			DynamicValueConverter.WriteValue(writer, value, options);
		}

		/// <summary>
		/// Supports <c>object</c>-keyed dictionaries (e.g. <c>IDictionary&lt;object, object&gt;</c>).
		/// STJ has no built-in property-name converter for <see cref="object"/>, so a dictionary with
		/// object keys would otherwise throw <see cref="NotSupportedException"/>. Writes the key's
		/// verbatim string form (matching the legacy Utf8Json behavior for weakly-typed dictionary keys).
		/// </summary>
		public override void WriteAsPropertyName(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
		{
			if (value == null)
				throw new ArgumentNullException(nameof(value), "Dictionary key must not be null.");
			writer.WritePropertyName(value.ToString());
		}

		/// <inheritdoc cref="WriteAsPropertyName" />
		public override object ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			reader.GetString();
	}
}
