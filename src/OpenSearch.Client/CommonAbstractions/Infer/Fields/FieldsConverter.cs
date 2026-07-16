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
	/// <summary>
	/// Serializes <see cref="Fields"/> as a JSON array of field strings, resolving each element
	/// through the settings-aware <see cref="Field"/> converter. Replaces the Utf8Json
	/// <c>FieldsFormatter</c>.
	/// </summary>
	internal sealed class FieldsConverter : JsonConverter<Fields>
	{
		public override Fields Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return null;
			}

			var fieldConverter = (JsonConverter<Field>)options.GetConverter(typeof(Field));
			var fields = new List<Field>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				var field = fieldConverter.Read(ref reader, typeof(Field), options);
				if (field != null)
					fields.Add(field);
			}

			return new Fields(fields);
		}

		public override void Write(Utf8JsonWriter writer, Fields value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var fieldConverter = (JsonConverter<Field>)options.GetConverter(typeof(Field));

			writer.WriteStartArray();
			foreach (var field in value.ListOfFields)
				fieldConverter.Write(writer, field, options);
			writer.WriteEndArray();
		}
	}
}
