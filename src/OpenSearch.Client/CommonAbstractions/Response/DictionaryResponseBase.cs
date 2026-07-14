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
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	public interface IDictionaryResponse<TKey, TValue> : IResponse
	{
		IReadOnlyDictionary<TKey, TValue> BackingDictionary { get; set; }
	}

	public abstract class DictionaryResponseBase<TKey, TValue> : ResponseBase, IDictionaryResponse<TKey, TValue>
	{
		[IgnoreDataMember]
		protected IDictionaryResponse<TKey, TValue> Self => this;

		IReadOnlyDictionary<TKey, TValue> IDictionaryResponse<TKey, TValue>.BackingDictionary { get; set; } =
			EmptyReadOnly<TKey, TValue>.Dictionary;
	}

	internal class ResponseFormatterHelpers
	{
		internal static readonly Dictionary<string, int> ServerErrorFields = new Dictionary<string, int>
		{
			{ "error", 0 },
			{ "status", 1 }
		};

		// Byte-segment keyed variant used by the restored Utf8Json response formatters, which match raw
		// property-name segments (ArraySegment&lt;byte&gt;) rather than decoded strings. Only used on the
		// legacy Utf8Json serialization path.
		internal static readonly OpenSearch.Net.Utf8Json.Internal.AutomataDictionary ServerErrorFieldsAutomata =
			new OpenSearch.Net.Utf8Json.Internal.AutomataDictionary
			{
				{ "error", 0 },
				{ "status", 1 }
			};
	}

}
