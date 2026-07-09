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

using System.Text.Json.Serialization;
using OpenSearch.Net.Extensions;


namespace OpenSearch.Client
{
	public interface IGeoDistanceQuery : IFieldNameQuery
	{
		Distance Distance { get; set; }

		GeoDistanceType? DistanceType { get; set; }

		GeoLocation Location { get; set; }

		GeoValidationMethod? ValidationMethod { get; set; }
	}

	public class GeoDistanceQuery : FieldNameQueryBase, IGeoDistanceQuery
	{
		public Distance Distance { get; set; }
		public GeoDistanceType? DistanceType { get; set; }
		public GeoLocation Location { get; set; }
		public GeoValidationMethod? ValidationMethod { get; set; }
		protected override bool Conditionless => IsConditionless(this);

		internal override void InternalWrapInContainer(IQueryContainer c) => c.GeoDistance = this;

		internal static bool IsConditionless(IGeoDistanceQuery q) =>
			q.Location == null || q.Distance == null || q.Field.IsConditionless();
	}

	public class GeoDistanceQueryDescriptor<T>
		: FieldNameQueryDescriptorBase<GeoDistanceQueryDescriptor<T>, IGeoDistanceQuery, T>
			, IGeoDistanceQuery where T : class
	{
		protected override bool Conditionless => GeoDistanceQuery.IsConditionless(this);
		Distance IGeoDistanceQuery.Distance { get; set; }
		GeoDistanceType? IGeoDistanceQuery.DistanceType { get; set; }
		GeoLocation IGeoDistanceQuery.Location { get; set; }
		GeoValidationMethod? IGeoDistanceQuery.ValidationMethod { get; set; }

		public GeoDistanceQueryDescriptor<T> Location(GeoLocation location) => Assign(location, (a, v) => a.Location = v);

		public GeoDistanceQueryDescriptor<T> Location(double lat, double lon) => Assign(new GeoLocation(lat, lon), (a, v) => a.Location = v);

		public GeoDistanceQueryDescriptor<T> Distance(Distance distance) => Assign(distance, (a, v) => a.Distance = v);

		public GeoDistanceQueryDescriptor<T> Distance(double distance, DistanceUnit unit) => Assign(new Distance(distance, unit), (a, v) => a.Distance = v);

		public GeoDistanceQueryDescriptor<T> DistanceType(GeoDistanceType? type) => Assign(type, (a, v) => a.DistanceType = v);

		public GeoDistanceQueryDescriptor<T> ValidationMethod(GeoValidationMethod? validation) => Assign(validation, (a, v) => a.ValidationMethod = v);
	}

}
