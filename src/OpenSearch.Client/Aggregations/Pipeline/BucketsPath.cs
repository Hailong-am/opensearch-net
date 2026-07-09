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

namespace OpenSearch.Client
{
	public interface IBucketsPath { }

	public class SingleBucketsPath : IBucketsPath
	{
		public SingleBucketsPath(string bucketsPath) => BucketsPath = bucketsPath;

		public string BucketsPath { get; }

		public static implicit operator SingleBucketsPath(string bucketsPath) => new SingleBucketsPath(bucketsPath);
	}

	public interface IMultiBucketsPath : IIsADictionary<string, string>, IBucketsPath { }

	public class MultiBucketsPath : IsADictionaryBase<string, string>, IMultiBucketsPath
	{
		public MultiBucketsPath() { }

		public MultiBucketsPath(IDictionary<string, string> container) : base(container) { }

		public MultiBucketsPath(Dictionary<string, string> container) : base(container) { }

		public void Add(string name, string bucketsPath) => BackingDictionary.Add(name, bucketsPath);

		public static implicit operator MultiBucketsPath(Dictionary<string, string> bucketsPath) => new MultiBucketsPath(bucketsPath);
	}

	public class MultiBucketsPathDescriptor
		: IsADictionaryDescriptorBase<MultiBucketsPathDescriptor, IMultiBucketsPath, string, string>
	{
		public MultiBucketsPathDescriptor() : base(new MultiBucketsPath()) { }

		public MultiBucketsPathDescriptor Add(string name, string bucketsPath) => Assign(name, bucketsPath);
	}

}
