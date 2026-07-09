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
		private readonly IConnectionSettingsValues _settings;

		public DefaultHighLevelSerializer(IConnectionSettingsValues settings)
		{
			_settings = settings;
			_serializerOptions = new OpenSearchClientSerializerOptions(settings);
		}

		bool IInternalSerializer.TryGetJsonSerializerOptions(out JsonSerializerOptions options)
		{
			options = _serializerOptions.Options;
			return true;
		}

		// A bare user document serialized/deserialized at the JSON root (the root type IS the document,
		// not a document nested inside an OpenSearch envelope such as a bulk body or a hit's _source)
		// follows the high-level naming path. The pre-STJ Utf8Json client attached its SourceFormatter
		// only to explicitly attributed _source members, never as a top-level catch-all, so a document
		// at the root honored OSC field-name inference (DefaultFieldNameInferrer, PropertyName/DataMember,
		// DefaultMappingFor) rather than a distinct source serializer's own naming. Route those through
		// the inference-aware terminal source options.
		//
		// Only types the domain options would themselves hand to SourceConverter qualify: if a
		// higher-precedence domain converter claims the root type (e.g. IsADictionaryConverterFactory for
		// IIsADictionary types), the domain options are used unchanged so that converter still runs.
		// Embedded document bodies are likewise unaffected: index/bulk request bodies serialize via the
		// configured source serializer directly through IProxyRequest.WriteJson, and _source members are
		// (de)serialized by nested converters — neither reaches this entry point with a bare-document
		// root type. In the default case (no distinct source serializer) the domain options would anyway
		// delegate the root document to SourceConverter, which already uses these same terminal options,
		// so the produced JSON is unchanged.
		private JsonSerializerOptions OptionsForRoot(Type rootType, JsonSerializerOptions domainOptions)
		{
			if (!SourceConverterFactory.IsSourceDocumentType(rootType))
				return domainOptions;
			// Confirm the domain options resolve the root type to the SourceConverter and not some
			// higher-precedence domain converter; only then does the inference-aware terminal path match
			// the domain behavior for the document itself.
			return domainOptions.GetConverter(rootType) is ISourceConverter
				? SourceConverterHelper.GetDefaultSourceOptions(_settings)
				: domainOptions;
		}

		public T Deserialize<T>(Stream stream)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0) return default;
			return JsonSerializer.Deserialize<T>(stream, OptionsForRoot(typeof(T), _serializerOptions.Options));
		}

		public object Deserialize(Type type, Stream stream)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0) return null;
			return JsonSerializer.Deserialize(stream, type, OptionsForRoot(type, _serializerOptions.Options));
		}

		public Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0)
				return Task.FromResult(default(T));
			return JsonSerializer.DeserializeAsync<T>(stream, OptionsForRoot(typeof(T), _serializerOptions.Options), cancellationToken).AsTask();
		}

		public Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			if (stream == null || stream.CanSeek && stream.Length == 0)
				return Task.FromResult((object)null);
			return JsonSerializer.DeserializeAsync(stream, type, OptionsForRoot(type, _serializerOptions.Options), cancellationToken).AsTask();
		}

		public virtual void Serialize<T>(T data, Stream writableStream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			JsonSerializer.Serialize(writableStream, data, OptionsForRoot(typeof(T), options));
		}

		public Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default)
		{
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			return JsonSerializer.SerializeAsync(stream, data, OptionsForRoot(typeof(T), options), cancellationToken);
		}
	}
}
