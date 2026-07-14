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
	/// Serializes <see cref="GeoCoordinate"/> as a coordinate array <c>[lon, lat, [z]]</c> and
	/// deserializes the same, matching the historical (Utf8Json) <c>GeoCoordinateFormatter</c>.
	/// This differs from the base <see cref="GeoLocation"/> which serializes as <c>{ "lat": .., "lon": .. }</c>.
	/// </summary>
	internal sealed class GeoCoordinateConverter : JsonConverter<GeoCoordinate>
	{
		public override GeoCoordinate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return null;
			}

			var doubles = new List<double>(3);
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				if (reader.TokenType == JsonTokenType.Number)
					doubles.Add(reader.GetDouble());
			}

			switch (doubles.Count)
			{
				case 2:
					return new GeoCoordinate(doubles[1], doubles[0]);
				case 3:
					return new GeoCoordinate(doubles[1], doubles[0], doubles[2]);
				default:
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, GeoCoordinate value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();
			WriteDouble(writer, value.Longitude);
			WriteDouble(writer, value.Latitude);
			if (value.Z.HasValue)
				WriteDouble(writer, value.Z.Value);
			writer.WriteEndArray();
		}

		// Emits integral values with a trailing ".0" (e.g. 10.0 rather than 10), matching the
		// client's historical wire format. The options-registered DoubleConverter is bypassed for
		// values written from within a converter, so the same formatting is applied here directly.
		private static void WriteDouble(Utf8JsonWriter writer, double value)
		{
			if (!double.IsNaN(value) && !double.IsInfinity(value)
				&& Math.Floor(value) == value && Math.Abs(value) < 1e16)
			{
				writer.WriteRawValue(
					value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
					skipInputValidation: true);
				return;
			}

			writer.WriteNumberValue(value);
		}
	}
}
