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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IGeoBoundingBoxQuery"/> that handles
	/// the field-name-wrapping pattern with bounding box coordinates.
	/// <para>
	/// The OpenSearch geo_bounding_box query JSON shape is:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "validation_method": "...",
	///   "type": "...",
	///   "field_name": { "top_left": {...}, "bottom_right": {...} }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class GeoBoundingBoxQueryConverter : JsonConverter<IGeoBoundingBoxQuery>
	{
		public override IGeoBoundingBoxQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new GeoBoundingBoxQuery();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to value

				switch (propertyName)
				{
					case "_name":
						query.Name = reader.GetString();
						break;
					case "boost":
						query.Boost = reader.GetDouble();
						break;
					case "validation_method":
						query.ValidationMethod = JsonSerializer.Deserialize<GeoValidationMethod>(ref reader, options);
						break;
					case "type":
						query.Type = JsonSerializer.Deserialize<GeoExecution>(ref reader, options);
						break;
					default:
						// This is the field name — its value is the bounding box object
						query.Field = propertyName;
						query.BoundingBox = JsonSerializer.Deserialize<IBoundingBox>(ref reader, options);
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IGeoBoundingBoxQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var fieldName = value.Field?.ToString();
			if (string.IsNullOrEmpty(fieldName))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName("_name");
				writer.WriteStringValue(value.Name);
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName("boost");
				writer.WriteNumberValue(value.Boost.Value);
			}

			if (value.ValidationMethod.HasValue)
			{
				writer.WritePropertyName("validation_method");
				JsonSerializer.Serialize(writer, value.ValidationMethod.Value, options);
			}

			if (value.Type.HasValue)
			{
				writer.WritePropertyName("type");
				JsonSerializer.Serialize(writer, value.Type.Value, options);
			}

			writer.WritePropertyName(fieldName);
			JsonSerializer.Serialize(writer, value.BoundingBox, options);

			writer.WriteEndObject();
		}
	}
}
