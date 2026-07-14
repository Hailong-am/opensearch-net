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
	/// Serializes <see cref="Indices"/> as a JSON array of index names (e.g. <c>["a", "b"]</c>),
	/// as opposed to the default comma-separated multi-syntax string form. Applied via
	/// <c>[JsonConverter]</c> on properties that expect the array shape (alias add/remove operations).
	/// </summary>
	internal sealed class IndicesArrayConverter : JsonConverter<Indices>
	{
		public override Indices Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return reader.GetString();
				case JsonTokenType.StartArray:
				{
					var indices = new List<IndexName>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						if (reader.TokenType == JsonTokenType.String)
							indices.Add(reader.GetString());
						else
							reader.Skip();
					}
					return new Indices(indices);
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, Indices value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			value.Match(
				all =>
				{
					writer.WriteStartArray();
					writer.WriteStringValue("_all");
					writer.WriteEndArray();
				},
				many =>
				{
					writer.WriteStartArray();
					foreach (var index in many.Indices)
						JsonSerializer.Serialize(writer, index, typeof(IndexName), options);
					writer.WriteEndArray();
				});
		}
	}
}
