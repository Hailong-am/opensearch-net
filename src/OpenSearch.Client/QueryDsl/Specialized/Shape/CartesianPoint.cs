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
using System.IO;
using System.Text;
using OpenSearch.Client;
using OpenSearch.Net.Extensions;

namespace OpenSearch.Client
{
	internal enum ShapeFormat
	{
		Object,
		Array,
		WellKnownText,
		String,
	}

	/// <summary>
	/// Represents a point in the cartesian plane.
	/// </summary>
	public class CartesianPoint : IEquatable<CartesianPoint>
	{
		internal ShapeFormat Format = ShapeFormat.Object;

		public float X { get; set; }
		public float Y { get; set; }

		public CartesianPoint()
		{
		}

		public CartesianPoint(float x, float y)
		{
			X = x;
			Y = y;
		}

		public bool Equals(CartesianPoint other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;

			return X.Equals(other.X) && Y.Equals(other.Y);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != GetType()) return false;

			return Equals((CartesianPoint)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (X.GetHashCode() * 397) ^ Y.GetHashCode();
			}
		}

		public static CartesianPoint FromCoordinates(string coordinates)
		{
			var values = coordinates.Split(',');
			if (values.Length > 3 || values.Length < 2)
				throw new InvalidOperationException(
					$"failed to parse {coordinates}, expected 2 or 3 coordinates but found: {values.Length}");

			var s = values[0].Trim();
			if (!float.TryParse(s, out var x))
				throw new InvalidOperationException($"failed to parse float for x from {s}");

			s = values[1].Trim();
			if (!float.TryParse(s, out var y))
				throw new InvalidOperationException($"failed to parse float for y from {s}");

			if (values.Length > 2)
			{
				s = values[2].Trim();
				if (!float.TryParse(s, out var _))
					throw new InvalidOperationException($"failed to parse float for z from {s}");
			}

			return new CartesianPoint(x, y) { Format = ShapeFormat.String };
		}

		public static CartesianPoint FromWellKnownText(string wkt)
		{
			using var tokenizer = new WellKnownTextTokenizer(new StringReader(wkt));
			var token = tokenizer.NextToken();

			if (token != TokenType.Word)
				throw new GeoWKTException(
					$"Expected word but found {tokenizer.TokenString()}", tokenizer.LineNumber, tokenizer.Position);

			var type = tokenizer.TokenValue.ToUpperInvariant();
			if (type != GeoShapeType.Point)
				throw new GeoWKTException(
					$"Expected {GeoShapeType.Point} but found {type}", tokenizer.LineNumber, tokenizer.Position);

			if (GeoWKTReader.NextEmptyOrOpen(tokenizer) == TokenType.Word)
				return null;

			var x = Convert.ToSingle(GeoWKTReader.NextNumber(tokenizer));
			var y = Convert.ToSingle(GeoWKTReader.NextNumber(tokenizer));

			// ignore any z value for now
			if (GeoWKTReader.IsNumberNext(tokenizer))
				GeoWKTReader.NextNumber(tokenizer);

			var point = new CartesianPoint(x, y) { Format = ShapeFormat.WellKnownText };
			GeoWKTReader.NextCloser(tokenizer);

			return point;
		}

		public static implicit operator CartesianPoint(string value)
		{
			try
			{
				return value.IndexOf(",", StringComparison.InvariantCultureIgnoreCase) > -1
					? FromCoordinates(value)
					: FromWellKnownText(value);
			}
			catch
			{
				// implicit conversions should never fail
				return null;
			}
		}

		public static bool operator ==(CartesianPoint left, CartesianPoint right) => Equals(left, right);

		public static bool operator !=(CartesianPoint left, CartesianPoint right) => !Equals(left, right);
	}

}
