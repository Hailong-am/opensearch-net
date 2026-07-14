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
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	/// <summary>
	/// A source repository enables you to create minimal, source-only snapshots that take up to 50% less space on disk.
	/// Source only snapshots contain stored fields and index metadata. They do not include index or doc values structures
	/// and are not searchable when restored. After restoring a source-only snapshot, you must reindex the data into a new index.
	/// </summary>
	[JsonConverter(typeof(SourceOnlyRepositoryConverter))]
	[JsonFormatter(typeof(SourceOnlyRepositoryFormatter))]
	public interface ISourceOnlyRepository : IRepositoryWithSettings
	{
		/// <summary>
		/// The type of snapshot repository to delegate to for storage
		/// </summary>
		[IgnoreDataMember]
		string DelegateType { get; }
	}

	/// <inheritdoc />
	public class SourceOnlyRepository : ISourceOnlyRepository
	{
		private readonly object _delegateSettings;
		private readonly string _delegateType;

		internal SourceOnlyRepository() { }

		internal SourceOnlyRepository(string delegateType, object settings)
		{
			_delegateType = delegateType;
			_delegateSettings = settings;
		}

		public SourceOnlyRepository(IRepositoryWithSettings repositoryToDelegateTo)
		{
			if (repositoryToDelegateTo == null) throw new ArgumentNullException(nameof(repositoryToDelegateTo));

			_delegateType = repositoryToDelegateTo.Type;
			_delegateSettings = repositoryToDelegateTo.DelegateSettings;
		}

		object IRepositoryWithSettings.DelegateSettings => _delegateSettings;
		string ISourceOnlyRepository.DelegateType => _delegateType;
		string ISnapshotRepository.Type { get; } = "source";
	}

	/// <inheritdoc cref="ISourceOnlyRepository" />
	public class SourceOnlyRepositoryDescriptor
		: DescriptorBase<SourceOnlyRepositoryDescriptor, ISourceOnlyRepository>, ISourceOnlyRepository
	{
		private object _delegateSettings;
		private string _delegateType;

		object IRepositoryWithSettings.DelegateSettings => _delegateSettings;
		string ISourceOnlyRepository.DelegateType => _delegateType;
		string ISnapshotRepository.Type { get; } = "source";

		private SourceOnlyRepositoryDescriptor DelegateTo<TDescriptor>(Func<TDescriptor, IRepositoryWithSettings> selector)
			where TDescriptor : IRepositoryWithSettings, new() => Custom(selector?.Invoke(new TDescriptor()));

		/// <inheritdoc cref="CreateRepositoryDescriptor.FileSystem" />
		public SourceOnlyRepositoryDescriptor FileSystem(Func<FileSystemRepositoryDescriptor, IFileSystemRepository> selector) =>
			DelegateTo(selector);

		/// <inheritdoc cref="CreateRepositoryDescriptor.ReadOnlyUrl" />
		public SourceOnlyRepositoryDescriptor ReadOnlyUrl(Func<ReadOnlyUrlRepositoryDescriptor, IReadOnlyUrlRepository> selector) =>
			DelegateTo(selector);

		/// <inheritdoc cref="CreateRepositoryDescriptor.Azure" />
		public SourceOnlyRepositoryDescriptor Azure(Func<AzureRepositoryDescriptor, IAzureRepository> selector = null) =>
			DelegateTo(selector);

		/// <inheritdoc cref="CreateRepositoryDescriptor.Hdfs" />
		public SourceOnlyRepositoryDescriptor Hdfs(Func<HdfsRepositoryDescriptor, IHdfsRepository> selector) =>
			DelegateTo(selector);

		/// <inheritdoc cref="CreateRepositoryDescriptor.S3" />
		public SourceOnlyRepositoryDescriptor S3(Func<S3RepositoryDescriptor, IS3Repository> selector) =>
			DelegateTo(selector);

		/// <inheritdoc cref="CreateRepositoryDescriptor.Custom" />
		public SourceOnlyRepositoryDescriptor Custom(IRepositoryWithSettings repository)
		{
			_delegateType = repository?.Type;
			_delegateSettings = repository?.DelegateSettings;
			return this;
		}
	}

}
