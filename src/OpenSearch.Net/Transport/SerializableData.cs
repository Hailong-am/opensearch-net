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

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static OpenSearch.Net.SerializationFormatting;

namespace OpenSearch.Net
{
	public class SerializableData<T> : PostData, IPostData<T>
	{
		private readonly T _serializable;

		public SerializableData(T item)
		{
			Type = PostType.Serializable;
			_serializable = item;
		}

		public static implicit operator SerializableData<T>(T serializableData) => new SerializableData<T>(serializableData);

		public override void Write(Stream writableStream, IConnectionConfigurationValues settings)
		{
			MemoryStream buffer = null;
			var stream = writableStream;
			BufferIfNeeded(settings, ref buffer, ref stream);

			// Request bodies are always written compact. The original Utf8Json request/response
			// serializer ignored the formatting parameter entirely, so PrettyJson never indented
			// request bodies (it only affects response debug rendering). The STJ serializer honours
			// SerializationFormatting.Indented, so we must explicitly request None here to preserve
			// wire parity (see GitHubIssue4573 and DebugInformation docs which assert compact bodies).
			settings.RequestResponseSerializer.Serialize(_serializable, stream, None);

			FinishStream(writableStream, buffer, settings);
		}

		public override async Task WriteAsync(Stream writableStream, IConnectionConfigurationValues settings, CancellationToken cancellationToken)
		{
			MemoryStream buffer = null;
            var stream = writableStream;
            BufferIfNeeded(settings, ref buffer, ref stream);

            // See Write: request bodies are always compact to preserve parity with the Utf8Json serializer.
			await settings.RequestResponseSerializer.SerializeAsync(_serializable, stream, None, cancellationToken)
				.ConfigureAwait(false);

			await FinishStreamAsync(writableStream, buffer, settings, cancellationToken).ConfigureAwait(false);
		}
	}
}
