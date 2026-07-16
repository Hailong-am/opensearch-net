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
	/// Polymorphic converter for <see cref="ISuggestContext"/> that discriminates
	/// on the "type" JSON field to deserialize as either <see cref="CategorySuggestContext"/>
	/// or <see cref="GeoSuggestContext"/>.
	/// </summary>
	internal sealed class ISuggestContextConverter : JsonConverter<ISuggestContext>
	{
		public override ISuggestContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// Clone the reader to peek at the "type" field without consuming the object
			var readerClone = reader;

			if (readerClone.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject token, got {readerClone.TokenType}");

			string type = null;
			while (readerClone.Read())
			{
				if (readerClone.TokenType == JsonTokenType.EndObject)
					break;

				if (readerClone.TokenType == JsonTokenType.PropertyName)
				{
					var propertyName = readerClone.GetString();
					readerClone.Read(); // advance to value

					if (string.Equals(propertyName, "type", StringComparison.OrdinalIgnoreCase))
					{
						type = readerClone.GetString();
						break;
					}

					// Skip the value if it's not the "type" field
					readerClone.Skip();
				}
			}

			switch (type)
			{
				case "category":
					return JsonSerializer.Deserialize<CategorySuggestContext>(ref reader, options);
				case "geo":
					return JsonSerializer.Deserialize<GeoSuggestContext>(ref reader, options);
				default:
					// Fallback: deserialize as CategorySuggestContext for unknown types
					return JsonSerializer.Deserialize<CategorySuggestContext>(ref reader, options);
			}
		}

		public override void Write(Utf8JsonWriter writer, ISuggestContext value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Serialize using the runtime type to include all properties
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
