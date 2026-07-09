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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>The built in internal serializer that the high level client OpenSearch.Client uses.</summary>
	internal class DefaultHighLevelSerializer : IOpenSearchSerializer, IInternalSerializer
	{
		private readonly OpenSearchClientSerializerOptions _serializerOptions;

		public DefaultHighLevelSerializer(IConnectionSettingsValues settings) =>
			_serializerOptions = new OpenSearchClientSerializerOptions(settings);

		bool IInternalSerializer.TryGetJsonSerializerOptions(out JsonSerializerOptions options)
		{
			options = _serializerOptions.Options;
			return true;
		}

		public T Deserialize<T>(Stream stream)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0) return default;
			return JsonSerializer.Deserialize<T>(stream, _serializerOptions.Options);
		}

		public object Deserialize(Type type, Stream stream)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0) return null;
			return JsonSerializer.Deserialize(stream, type, _serializerOptions.Options);
		}

		public Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0)
				return Task.FromResult(default(T));
			return JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions.Options, cancellationToken).AsTask();
		}

		public Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0)
				return Task.FromResult((object)null);
			return JsonSerializer.DeserializeAsync(stream, type, _serializerOptions.Options, cancellationToken).AsTask();
		}

		public virtual void Serialize<T>(T data, Stream writableStream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			JsonSerializer.Serialize(writableStream, data, options);
		}

		public Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default)
		{
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			return JsonSerializer.SerializeAsync(stream, data, options, cancellationToken);
		}
	}
}
