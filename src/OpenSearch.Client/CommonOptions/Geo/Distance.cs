/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
/*
* Modifications Copyright OpenSearch Contributors. See
* GitHub history for details.
*
*  Licensed to Elasticsearch B.V. under one or more contributor
*  license agreements. See the NOTICE file distributed with
*  this work for additional information regarding copyright
*  ownership. Elasticsearch B.V. licenses this file to you under
*  the Apache License, Version 2.0 (the "License"); you may
*  not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
* 	http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing,
*  software distributed under the License is distributed on an
*  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
*  KIND, either express or implied.  See the License for the
*  specific language governing permissions and limitations
*  under the License.
*/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net.Utf8Json;
using System.Text.RegularExpressions;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>Serializes <see cref="Distance"/> as its string form (e.g. <c>"200m"</c>) and parses it back.</summary>
	internal sealed class DistanceConverter : JsonConverter<Distance>
	{
		public override Distance Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.String:
					var s = reader.GetString();
					return s == null ? null : new Distance(s);
				case JsonTokenType.Number:
					return new Distance(reader.GetDouble());
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, Distance value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				writer.WriteStringValue(value.ToString());
		}
	}

	[JsonFormatter(typeof(DistanceFormatter))]
	public class Distance
	{
		private static readonly Regex DistanceUnitRegex =
			new Regex(@"^(?<precision>\d+(?:\.\d+)?)(?<unit>\D+)?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

		public Distance(double distance) : this(distance, DistanceUnit.Meters) { }

		public Distance(double distance, DistanceUnit unit)
		{
			Precision = distance;
			Unit = unit;
		}

		public Distance(string distanceUnit)
		{
			distanceUnit.ThrowIfNullOrEmpty(nameof(distanceUnit));
			var match = DistanceUnitRegex.Match(distanceUnit);

			if (!match.Success)
				throw new ArgumentException("must be a valid distance unit", nameof(distanceUnit));

			var precision = double.Parse(match.Groups["precision"].Value, NumberStyles.Any, CultureInfo.InvariantCulture);
			var unit = match.Groups["unit"].Value;

			Precision = precision;

			if (string.IsNullOrEmpty(unit))
			{
				Unit = DistanceUnit.Meters;
				return;
			}

			var unitMeasure = unit.ToEnum<DistanceUnit>();
			if (unitMeasure == null) throw new InvalidCastException($"cannot parse {typeof(DistanceUnit).Name} from string '{unit}'");

			Unit = unitMeasure.Value;
		}

		public double Precision { get; private set; }
		public DistanceUnit Unit { get; private set; }

		public static Distance Inches(double inches) => new Distance(inches, DistanceUnit.Inch);

		public static Distance Yards(double yards) => new Distance(yards, DistanceUnit.Yards);

		public static Distance Miles(double miles) => new Distance(miles, DistanceUnit.Miles);

		public static Distance Kilometers(double kilometers) => new Distance(kilometers, DistanceUnit.Kilometers);

		public static Distance Meters(double meters) => new Distance(meters, DistanceUnit.Meters);

		public static Distance Centimeters(double centimeters) => new Distance(centimeters, DistanceUnit.Centimeters);

		public static Distance Millimeters(double millimeter) => new Distance(millimeter, DistanceUnit.Millimeters);

		public static Distance NauticalMiles(double nauticalMiles) => new Distance(nauticalMiles, DistanceUnit.NauticalMiles);

		public static implicit operator Distance(string distanceUnit) => new Distance(distanceUnit);

		public override string ToString() => Precision.ToString(CultureInfo.InvariantCulture) + Unit.GetStringValue();
	}
}
