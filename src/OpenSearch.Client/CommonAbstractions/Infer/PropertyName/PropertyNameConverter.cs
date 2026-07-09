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
	internal sealed class PropertyNameConverter : JsonConverter<PropertyName>
	{
		public override PropertyName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var name = reader.GetString();
			return name == null ? null : new PropertyName(name);
		}

		public override void Write(Utf8JsonWriter writer, PropertyName value, JsonSerializerOptions options)
		{
			if (value == null || value.IsConditionless())
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStringValue(value.Name);
		}

		public override PropertyName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var name = reader.GetString();
			return name == null ? null : new PropertyName(name);
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, PropertyName value, JsonSerializerOptions options)
		{
			if (value == null || value.IsConditionless())
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			writer.WritePropertyName(value.Name);
		}
	}
}
