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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IGeoDistanceQuery"/> that handles
	/// the field-name-wrapping pattern with distance and location parameters.
	/// <para>
	/// The OpenSearch geo_distance query JSON shape is:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "validation_method": "...",
	///   "distance": "12km",
	///   "distance_type": "arc",
	///   "field_name": { "lat": 40, "lon": -70 }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class GeoDistanceQueryConverter : JsonConverter<IGeoDistanceQuery>
	{
		public override IGeoDistanceQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new GeoDistanceQuery();

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
					case "distance":
						query.Distance = JsonSerializer.Deserialize<Distance>(ref reader, options);
						break;
					case "distance_type":
						query.DistanceType = JsonSerializer.Deserialize<GeoDistanceType>(ref reader, options);
						break;
					default:
						// This is the field name — its value is the geo location
						query.Field = propertyName;
						query.Location = JsonSerializer.Deserialize<GeoLocation>(ref reader, options);
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IGeoDistanceQuery value, JsonSerializerOptions options)
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

			if (value.Distance != null)
			{
				writer.WritePropertyName("distance");
				JsonSerializer.Serialize(writer, value.Distance, options);
			}

			if (value.DistanceType.HasValue)
			{
				writer.WritePropertyName("distance_type");
				JsonSerializer.Serialize(writer, value.DistanceType.Value, options);
			}

			writer.WritePropertyName(fieldName);
			JsonSerializer.Serialize(writer, value.Location, options);

			writer.WriteEndObject();
		}
	}
}
