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
using System.Text.Json.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Extensions;


namespace OpenSearch.Client
{
	[InterfaceDataContract]
	[JsonFormatter(typeof(GeoBoundingBoxQueryFormatter))]
	public interface IGeoBoundingBoxQuery : IFieldNameQuery
	{
		IBoundingBox BoundingBox { get; set; }

		GeoExecution? Type { get; set; }

		GeoValidationMethod? ValidationMethod { get; set; }
	}

	public class GeoBoundingBoxQuery : FieldNameQueryBase, IGeoBoundingBoxQuery
	{
		public IBoundingBox BoundingBox { get; set; }
		public GeoExecution? Type { get; set; }

		public GeoValidationMethod? ValidationMethod { get; set; }
		protected override bool Conditionless => IsConditionless(this);

		internal override void InternalWrapInContainer(IQueryContainer c) => c.GeoBoundingBox = this;

		internal static bool IsConditionless(IGeoBoundingBoxQuery q) =>
			q.Field.IsConditionless() || q.BoundingBox?.BottomRight == null && q.BoundingBox?.TopLeft == null && q.BoundingBox?.WellKnownText == null;
	}

	public class GeoBoundingBoxQueryDescriptor<T>
		: FieldNameQueryDescriptorBase<GeoBoundingBoxQueryDescriptor<T>, IGeoBoundingBoxQuery, T>
			, IGeoBoundingBoxQuery where T : class
	{
		protected override bool Conditionless => GeoBoundingBoxQuery.IsConditionless(this);
		IBoundingBox IGeoBoundingBoxQuery.BoundingBox { get; set; }
		GeoExecution? IGeoBoundingBoxQuery.Type { get; set; }
		GeoValidationMethod? IGeoBoundingBoxQuery.ValidationMethod { get; set; }

		public GeoBoundingBoxQueryDescriptor<T> BoundingBox(double topLeftLat, double topLeftLon, double bottomRightLat, double bottomRightLon) =>
			BoundingBox(f => f.TopLeft(topLeftLat, topLeftLon).BottomRight(bottomRightLat, bottomRightLon));

		public GeoBoundingBoxQueryDescriptor<T> BoundingBox(GeoLocation topLeft, GeoLocation bottomRight) =>
			BoundingBox(f => f.TopLeft(topLeft).BottomRight(bottomRight));

		public GeoBoundingBoxQueryDescriptor<T> BoundingBox(string wkt) =>
			BoundingBox(f => f.WellKnownText(wkt));

		public GeoBoundingBoxQueryDescriptor<T> BoundingBox(Func<BoundingBoxDescriptor, IBoundingBox> boundingBoxSelector) =>
			Assign(boundingBoxSelector, (a, v) => a.BoundingBox = v?.Invoke(new BoundingBoxDescriptor()));

		public GeoBoundingBoxQueryDescriptor<T> Type(GeoExecution? type) => Assign(type, (a, v) => a.Type = v);

		public GeoBoundingBoxQueryDescriptor<T> ValidationMethod(GeoValidationMethod? validation) => Assign(validation, (a, v) => a.ValidationMethod = v);
	}

}
