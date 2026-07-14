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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenSearch.Net.Utf8Json;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace OpenSearch.Net
{
	public class LowLevelRequestResponseSerializer : IOpenSearchSerializer, IInternalSerializer
	{
		public static readonly LowLevelRequestResponseSerializer Instance = new LowLevelRequestResponseSerializer();

		public object Deserialize(Type type, Stream stream)
		{
			if (IsEmptyStream(stream)) return type.IsValueType ? Activator.CreateInstance(type) : null;
			return StjSerializer.Deserialize(stream, type, OpenSearchNetSerializerOptions.Instance);
		}

		public T Deserialize<T>(Stream stream)
		{
			if (IsEmptyStream(stream)) return default;
			return StjSerializer.Deserialize<T>(stream, OpenSearchNetSerializerOptions.Instance);
		}

		public async Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			if (IsEmptyStream(stream)) return type.IsValueType ? Activator.CreateInstance(type) : null;
			return await StjSerializer.DeserializeAsync(stream, type, OpenSearchNetSerializerOptions.Instance, cancellationToken).ConfigureAwait(false);
		}

		public async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			if (IsEmptyStream(stream)) return default;
			return await StjSerializer.DeserializeAsync<T>(stream, OpenSearchNetSerializerOptions.Instance, cancellationToken).ConfigureAwait(false);
		}

		public void Serialize<T>(T data, Stream writableStream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			var options = formatting == SerializationFormatting.Indented
				? OpenSearchNetSerializerOptions.Indented
				: OpenSearchNetSerializerOptions.Instance;
			StjSerializer.Serialize(writableStream, data, options);
		}

		public Task SerializeAsync<T>(T data, Stream writableStream, SerializationFormatting formatting,
			CancellationToken cancellationToken = default
		)
		{
			var options = formatting == SerializationFormatting.Indented
				? OpenSearchNetSerializerOptions.Indented
				: OpenSearchNetSerializerOptions.Instance;
			return StjSerializer.SerializeAsync(writableStream, data, options, cancellationToken);
		}

		bool IInternalSerializer.TryGetJsonSerializerOptions(out System.Text.Json.JsonSerializerOptions options)
		{
			options = OpenSearchNetSerializerOptions.Instance;
			return true;
		}

		// The low-level serializer always uses System.Text.Json (it serializes OpenSearch.Net primitives, never
		// user domain types), so it never yields a Utf8Json formatter resolver.
		bool IInternalSerializer.TryGetFormatterResolver(out OpenSearch.Net.Utf8Json.IJsonFormatterResolver formatterResolver)
		{
			formatterResolver = null;
			return false;
		}

		private static bool IsEmptyStream(Stream stream)
		{
			if (stream == null) return true;
			if (!stream.CanSeek) return false;
			if (stream.Length == 0) return true;
			if (stream.Position >= stream.Length) return true;
			return false;
		}
	}
}
