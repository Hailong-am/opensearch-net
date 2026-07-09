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

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using OpenSearch.Net.Extensions;

namespace OpenSearch.Client
{
	[InterfaceDataContract]
	[System.Text.Json.Serialization.JsonConverter(typeof(GeoShapeConverter))]
	public interface IGeoShape
	{
		/// <summary>
		/// The type of geo shape
		/// </summary>
		[DataMember(Name = "type")]
		string Type { get; }
	}

	internal enum GeoFormat
	{
		GeoJson,
		WellKnownText
	}

	internal static class GeoShapeType
	{
		// WKT uses BBOX for envelope geo shape
		public const string BoundingBox = "BBOX";
		public const string Circle = "CIRCLE";
		public const string Envelope = "ENVELOPE";
		public const string GeometryCollection = "GEOMETRYCOLLECTION";
		public const string LineString = "LINESTRING";
		public const string MultiLineString = "MULTILINESTRING";
		public const string MultiPoint = "MULTIPOINT";
		public const string MultiPolygon = "MULTIPOLYGON";
		public const string Point = "POINT";
		public const string Polygon = "POLYGON";
	}

	/// <summary>
	/// Base type for geo shapes
	/// </summary>
	public abstract class GeoShapeBase : IGeoShape
	{
		protected GeoShapeBase(string type) => Type = type;

		/// <inheritdoc />
		public string Type { get; protected set; }

		internal GeoFormat Format { get; set; }
	}


}
