/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes <see cref="GeoLocation"/> as either a <c>{ "lat": .., "lon": .. }</c> object
	/// (<see cref="GeoFormat.GeoJson"/>) or a Well-Known-Text <c>"POINT (lon lat)"</c> string
	/// (<see cref="GeoFormat.WellKnownText"/>), and deserializes either form. Replaces the Utf8Json
	/// <c>GeoLocationFormatter</c>.
	/// </summary>
	internal sealed class GeoLocationConverter : JsonConverter<GeoLocation>
	{
		public override GeoLocation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
				{
					var wkt = reader.GetString();
					if (string.IsNullOrEmpty(wkt))
						return null;

					// Parse the WKT point via the shared reader and project onto a GeoLocation.
					if (GeoWKTReader.Read(wkt) is IPointGeoShape point && point.Coordinates != null)
						return new GeoLocation(point.Coordinates.Latitude, point.Coordinates.Longitude)
						{
							Format = GeoFormat.WellKnownText
						};
					return null;
				}
				case JsonTokenType.StartObject:
				{
					double lat = 0, lon = 0;
					while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
					{
						if (reader.TokenType != JsonTokenType.PropertyName)
							continue;

						var propertyName = reader.GetString();
						reader.Read();
						switch (propertyName)
						{
							case "lat":
								lat = reader.GetDouble();
								break;
							case "lon":
								lon = reader.GetDouble();
								break;
							default:
								reader.Skip();
								break;
						}
					}
					return new GeoLocation(lat, lon) { Format = GeoFormat.GeoJson };
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, GeoLocation value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value.Format)
			{
				case GeoFormat.WellKnownText:
					var lon = value.Longitude.ToString(CultureInfo.InvariantCulture);
					var lat = value.Latitude.ToString(CultureInfo.InvariantCulture);
					writer.WriteStringValue($"POINT ({lon} {lat})");
					break;
				default:
					// Route lat/lon through the registered double converter so integral values keep
					// their trailing ".0" (e.g. 34.0), matching the base serializer's number formatting.
					var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));
					writer.WriteStartObject();
					writer.WritePropertyName("lat");
					doubleConverter.Write(writer, value.Latitude, options);
					writer.WritePropertyName("lon");
					doubleConverter.Write(writer, value.Longitude, options);
					writer.WriteEndObject();
					break;
			}
		}
	}
}
