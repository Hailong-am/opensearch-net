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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IFieldMapping"/> that delegates
	/// to <see cref="IProperty"/> deserialization since all field mappings are properties.
	/// </summary>
	internal sealed class FieldMappingConverter : JsonConverter<IFieldMapping>
	{
		public override IFieldMapping Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// IProperty extends IFieldMapping, and PropertyConverter handles the type discrimination.
			// Deserialize as IProperty which will trigger PropertyConverter.
			return JsonSerializer.Deserialize<IProperty>(ref reader, options);
		}

		public override void Write(Utf8JsonWriter writer, IFieldMapping value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// All IFieldMapping implementations are IProperty, delegate to PropertyConverter
			if (value is IProperty property)
			{
				JsonSerializer.Serialize(writer, property, options);
			}
			else
			{
				// Fallback: serialize using the actual runtime type
				JsonSerializer.Serialize(writer, value, value.GetType(), options);
			}
		}
	}
}
