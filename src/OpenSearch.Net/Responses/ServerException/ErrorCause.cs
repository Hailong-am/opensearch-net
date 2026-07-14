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
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;
namespace OpenSearch.Net
{
	[JsonConverter(typeof(ErrorCauseConverter))]
	[DataContract]
	[JsonFormatter(typeof(ErrorCauseFormatter))]
	public class ErrorCause
	{
		private static readonly IReadOnlyCollection<string> DefaultCollection =
			new ReadOnlyCollection<string>(new string[0]);

		private static readonly IReadOnlyDictionary<string, object> DefaultDictionary =
			new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

		private static readonly IReadOnlyCollection<ShardFailure> DefaultFailedShards =
			new ReadOnlyCollection<ShardFailure>(new ShardFailure[0]);

		/// <summary>
		/// Additional properties related to the error cause. Contains properties that
		/// are not explicitly mapped on <see cref="ErrorCause" />
		/// </summary>
		public IReadOnlyDictionary<string, object> AdditionalProperties { get; internal set; } = DefaultDictionary;

		public long? BytesLimit { get; internal set; }

		public long? BytesWanted { get; internal set; }

		public ErrorCause CausedBy { get; internal set; }

		public int? Column { get; internal set; }

		public IReadOnlyCollection<ShardFailure> FailedShards { get; internal set; } = DefaultFailedShards;

		public bool? Grouped { get; internal set; }

		public string Index { get; internal set; }

		public string IndexUUID { get; internal set; }

		public string Language { get; internal set; }

		public int? Line { get; internal set; }

		public string Phase { get; internal set; }

		public string Reason { get; internal set; }

		public IReadOnlyCollection<string> ResourceId { get; internal set; } = DefaultCollection;

		public string ResourceType { get; internal set; }

		public string Script { get; internal set; }

		public IReadOnlyCollection<string> ScriptStack { get; internal set; } = DefaultCollection;

		public int? Shard { get; internal set; }

		public string StackTrace { get; internal set; }

		public string Type { get; internal set; }

		public override string ToString() => CausedBy == null
			? $"Type: {Type} Reason: \"{Reason}\""
			: $"Type: {Type} Reason: \"{Reason}\" CausedBy: \"{CausedBy}\"";
	}
}
