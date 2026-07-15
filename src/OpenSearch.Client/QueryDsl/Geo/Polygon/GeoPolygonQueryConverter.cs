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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IGeoPolygonQuery"/> that handles
	/// the field-name-wrapping pattern with polygon points.
	/// <para>
	/// The OpenSearch geo_polygon query JSON shape is:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "validation_method": "...",
	///   "field_name": { "points": [ {...}, {...} ] }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class GeoPolygonQueryConverter : JsonConverter<IGeoPolygonQuery>
	{
		private readonly IConnectionSettingsValues _settings;

		public GeoPolygonQueryConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IGeoPolygonQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new GeoPolygonQuery();

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
					default:
						// This is the field name — its value is the points object
						query.Field = propertyName;
						ReadPointsBody(ref reader, query, options);
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IGeoPolygonQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var fieldName = _settings.Inferrer.Field(value.Field);
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

			writer.WritePropertyName(fieldName);
			writer.WriteStartObject();
			writer.WritePropertyName("points");
			JsonSerializer.Serialize(writer, value.Points, options);
			writer.WriteEndObject();

			writer.WriteEndObject();
		}

		private static void ReadPointsBody(ref Utf8JsonReader reader, GeoPolygonQuery query, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return;
			}

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var prop = reader.GetString();
				reader.Read();

				switch (prop)
				{
					case "points":
						query.Points = JsonSerializer.Deserialize<IEnumerable<GeoLocation>>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}
		}
	}
}
