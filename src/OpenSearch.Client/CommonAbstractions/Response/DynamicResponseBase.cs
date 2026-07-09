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
using System.Runtime.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	public interface IDynamicResponse : IResponse
	{
		DynamicDictionary BackingDictionary { get; set; }
	}

	public abstract class DynamicResponseBase : ResponseBase, IDynamicResponse
	{
		[IgnoreDataMember]
		protected IDynamicResponse Self => this;

		/// <summary>
		/// Helper to to easily traverse the data using a path notation
		/// </summary>
		/// <param name="path">path into the stored object, keys are seperated with a dot and the last key is returned as T</param>
		/// <typeparam name="T"></typeparam>
		/// <returns>T or default</returns>
		public T Get<T>(string path) => Self.BackingDictionary.Get<T>(path);

		DynamicDictionary IDynamicResponse.BackingDictionary { get; set; } = new DynamicDictionary();
	}


}
