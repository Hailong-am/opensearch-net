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
	}
}
