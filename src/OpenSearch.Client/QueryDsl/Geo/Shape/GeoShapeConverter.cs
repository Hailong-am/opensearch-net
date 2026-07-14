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
	/// A polymorphic <see cref="JsonConverter{T}"/> for <see cref="IGeoShape"/> that emits/reads the
	/// GeoJSON <c>{ "type": ..., "coordinates": ... }</c> shape (or a WKT string when the shape's
	/// <see cref="GeoFormat"/> is <see cref="GeoFormat.WellKnownText"/>). Replaces the Utf8Json
	/// <c>GeoShapeFormatter</c>.
	/// </summary>
	internal sealed class GeoShapeConverter : JsonConverter<IGeoShape>
	{
		public override bool CanConvert(Type typeToConvert) => typeof(IGeoShape).IsAssignableFrom(typeToConvert);

		public override IGeoShape Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return GeoWKTReader.Read(reader.GetString());
				case JsonTokenType.StartObject:
					break;
				default:
					reader.Skip();
					return null;
			}

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
				return null;

			var typeName = typeElement.GetString()?.ToUpperInvariant();

			switch (typeName)
			{
				case GeoShapeType.Circle:
					return new CircleGeoShape
					{
						Coordinates = GetCoordinates<GeoCoordinate>(root, options),
						Radius = root.TryGetProperty("radius", out var r) ? r.GetString() : null
					};
				case GeoShapeType.Envelope:
					return new EnvelopeGeoShape { Coordinates = GetCoordinates<IEnumerable<GeoCoordinate>>(root, options) };
				case GeoShapeType.LineString:
					return new LineStringGeoShape { Coordinates = GetCoordinates<IEnumerable<GeoCoordinate>>(root, options) };
				case GeoShapeType.MultiLineString:
					return new MultiLineStringGeoShape { Coordinates = GetCoordinates<IEnumerable<IEnumerable<GeoCoordinate>>>(root, options) };
				case GeoShapeType.Point:
					return new PointGeoShape { Coordinates = GetCoordinates<GeoCoordinate>(root, options) };
				case GeoShapeType.MultiPoint:
					return new MultiPointGeoShape { Coordinates = GetCoordinates<IEnumerable<GeoCoordinate>>(root, options) };
				case GeoShapeType.Polygon:
					return new PolygonGeoShape { Coordinates = GetCoordinates<IEnumerable<IEnumerable<GeoCoordinate>>>(root, options) };
				case GeoShapeType.MultiPolygon:
					return new MultiPolygonGeoShape { Coordinates = GetCoordinates<IEnumerable<IEnumerable<IEnumerable<GeoCoordinate>>>>(root, options) };
				case GeoShapeType.GeometryCollection:
					return new GeometryCollection
					{
						Geometries = root.TryGetProperty("geometries", out var g)
							? g.Deserialize<IEnumerable<IGeoShape>>(options)
							: null
					};
				default:
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, IGeoShape value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value is GeoShapeBase shapeBase && shapeBase.Format == GeoFormat.WellKnownText)
			{
				writer.WriteStringValue(GeoWKTWriter.Write(shapeBase));
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("type");
			writer.WriteStringValue(value.Type);

			switch (value)
			{
				case IPointGeoShape point:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, point.Coordinates, options);
					break;
				case IMultiPointGeoShape multiPoint:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, multiPoint.Coordinates, options);
					break;
				case ILineStringGeoShape lineString:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, lineString.Coordinates, options);
					break;
				case IMultiLineStringGeoShape multiLineString:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, multiLineString.Coordinates, options);
					break;
				case IPolygonGeoShape polygon:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, polygon.Coordinates, options);
					break;
				case IMultiPolygonGeoShape multiPolygon:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, multiPolygon.Coordinates, options);
					break;
				case IEnvelopeGeoShape envelope:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, envelope.Coordinates, options);
					break;
				case ICircleGeoShape circle:
					writer.WritePropertyName("coordinates");
					JsonSerializer.Serialize(writer, circle.Coordinates, options);
					writer.WritePropertyName("radius");
					writer.WriteStringValue(circle.Radius);
					break;
				case IGeometryCollection collection:
					writer.WritePropertyName("geometries");
					JsonSerializer.Serialize(writer, collection.Geometries, options);
					break;
			}

			writer.WriteEndObject();
		}

		private static T GetCoordinates<T>(JsonElement root, JsonSerializerOptions options) =>
			root.TryGetProperty("coordinates", out var coords) ? coords.Deserialize<T>(options) : default;
	}
}
