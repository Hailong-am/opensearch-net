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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IDistanceFeatureQuery"/> that handles
	/// the distance_feature query with its field, origin, and pivot parameters.
	/// <para>
	/// Example JSON shape:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "field": "production_date",
	///   "origin": "now",
	///   "pivot": "7d"
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class DistanceFeatureQueryConverter : JsonConverter<IDistanceFeatureQuery>
	{
		public override IDistanceFeatureQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new DistanceFeatureQuery();

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
					case "field":
						query.Field = JsonSerializer.Deserialize<Field>(ref reader, options);
						break;
					case "origin":
						query.Origin = JsonSerializer.Deserialize<Union<GeoCoordinate, DateMath>>(ref reader, options);
						break;
					case "pivot":
						query.Pivot = JsonSerializer.Deserialize<Union<Distance, Time>>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IDistanceFeatureQuery value, JsonSerializerOptions options)
		{
			if (value == null)
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

			if (value.Field != null)
			{
				writer.WritePropertyName("field");
				JsonSerializer.Serialize(writer, value.Field, options);
			}

			if (value.Origin != null)
			{
				writer.WritePropertyName("origin");
				JsonSerializer.Serialize(writer, value.Origin, options);
			}

			if (value.Pivot != null)
			{
				writer.WritePropertyName("pivot");
				JsonSerializer.Serialize(writer, value.Pivot, options);
			}

			writer.WriteEndObject();
		}
	}
}
