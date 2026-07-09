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
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Extensions;


namespace OpenSearch.Client
{
	/// <summary>
	/// A values source for <see cref="ICompositeAggregation" />
	/// </summary>
	[InterfaceDataContract]
	[JsonConverter(typeof(CompositeAggregationSourceConverter))]
	public interface ICompositeAggregationSource
	{
		/// <summary>
		/// The field from which to extract value
		/// </summary>
		[DataMember(Name = "field")]
		Field Field { get; set; }

		/// <summary>
		/// By default documents without a value for a given source are ignored. It is possible to include
		/// them in the response as null by setting this to true
		/// </summary>
		[DataMember(Name = "missing_bucket")]
		bool? MissingBucket { get; set; }

		/// <summary>
		/// The name of the source
		/// </summary>
		[IgnoreDataMember]
		string Name { get; set; }

		/// <summary>
		/// Defines the direction of sorting for each
		/// value source. Defaults to <see cref="SortOrder.Ascending" />
		/// </summary>
		[DataMember(Name = "order")]
		SortOrder? Order { get; set; }

		/// <summary>
		/// The type of the source
		/// </summary>
		[IgnoreDataMember]
		string SourceType { get; }
	}

	/// <inheritdoc />
	public abstract class CompositeAggregationSourceBase : ICompositeAggregationSource
	{
		internal CompositeAggregationSourceBase() { }

		protected CompositeAggregationSourceBase(string name) =>
			((ICompositeAggregationSource)this).Name = name;

		/// <inheritdoc />
		public Field Field { get; set; }

		/// <inheritdoc />
		public bool? MissingBucket { get; set; }

		/// <inheritdoc />
		public SortOrder? Order { get; set; }

		/// <inheritdoc cref="ICompositeAggregationSource.SourceType" />
		protected abstract string SourceType { get; }

		/// <inheritdoc />
		string ICompositeAggregationSource.Name { get; set; }

		string ICompositeAggregationSource.SourceType => SourceType;
	}

	/// <inheritdoc cref="ICompositeAggregationSource" />
	public class CompositeAggregationSourcesDescriptor<T>
		: DescriptorPromiseBase<CompositeAggregationSourcesDescriptor<T>, IList<ICompositeAggregationSource>>
		where T : class
	{
		public CompositeAggregationSourcesDescriptor() : base(new List<ICompositeAggregationSource>()) { }

		/// <inheritdoc cref="ITermsCompositeAggregationSource" />
		public CompositeAggregationSourcesDescriptor<T> Terms(string name,
			Func<TermsCompositeAggregationSourceDescriptor<T>, ITermsCompositeAggregationSource> selector
		) =>
			Assign(selector?.Invoke(new TermsCompositeAggregationSourceDescriptor<T>(name)), (a, v) => a.Add(v));

		/// <inheritdoc cref="IHistogramCompositeAggregationSource" />
		public CompositeAggregationSourcesDescriptor<T> Histogram(string name,
			Func<HistogramCompositeAggregationSourceDescriptor<T>, IHistogramCompositeAggregationSource> selector
		) =>
			Assign(selector?.Invoke(new HistogramCompositeAggregationSourceDescriptor<T>(name)), (a, v) => a.Add(v));

		/// <inheritdoc cref="IDateHistogramCompositeAggregationSource" />
		public CompositeAggregationSourcesDescriptor<T> DateHistogram(string name,
			Func<DateHistogramCompositeAggregationSourceDescriptor<T>, IDateHistogramCompositeAggregationSource> selector
		) =>
			Assign(selector?.Invoke(new DateHistogramCompositeAggregationSourceDescriptor<T>(name)), (a, v) => a.Add(v));

		/// <inheritdoc cref="IGeoTileGridCompositeAggregationSource" />
		public CompositeAggregationSourcesDescriptor<T> GeoTileGrid(string name,
			Func<GeoTileGridCompositeAggregationSourceDescriptor<T>, IGeoTileGridCompositeAggregationSource> selector
		) =>
			Assign(selector?.Invoke(new GeoTileGridCompositeAggregationSourceDescriptor<T>(name)), (a, v) => a.Add(v));
	}

	/// <inheritdoc cref="ICompositeAggregationSource" />
	public abstract class CompositeAggregationSourceDescriptorBase<TDescriptor, TInterface, T>
		: DescriptorBase<TDescriptor, TInterface>, ICompositeAggregationSource
		where TDescriptor : CompositeAggregationSourceDescriptorBase<TDescriptor, TInterface, T>, TInterface
		where TInterface : class, ICompositeAggregationSource
	{
		private readonly string _sourceType;

		protected CompositeAggregationSourceDescriptorBase(string name, string sourceType)
		{
			_sourceType = sourceType;
			Self.Name = name;
		}

		Field ICompositeAggregationSource.Field { get; set; }
		bool? ICompositeAggregationSource.MissingBucket { get; set; }

		string ICompositeAggregationSource.Name { get; set; }
		SortOrder? ICompositeAggregationSource.Order { get; set; }
		string ICompositeAggregationSource.SourceType => _sourceType;

		/// <inheritdoc cref="ICompositeAggregationSource.Field" />
		public TDescriptor Field(Field field) => Assign(field, (a, v) => a.Field = v);

		/// <inheritdoc cref="ICompositeAggregationSource.Field" />
		public TDescriptor Field<TValue>(Expression<Func<T, TValue>> objectPath) => Assign(objectPath, (a, v) => a.Field = v);

		/// <inheritdoc cref="ICompositeAggregationSource.Order" />
		public TDescriptor Order(SortOrder? order) => Assign(order, (a, v) => a.Order = v);

		/// <inheritdoc cref="ICompositeAggregationSource.MissingBucket" />
		public TDescriptor MissingBucket(bool? includeMissing = true) => Assign(includeMissing, (a, v) => a.MissingBucket = v);
	}

}
