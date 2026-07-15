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
using IJsonFormatterResolver = OpenSearch.Net.Utf8Json.IJsonFormatterResolver;
using Utf8JsonSerializer = OpenSearch.Net.Utf8Json.JsonSerializer;

namespace OpenSearch.Client
{
	/// <summary>The built in internal serializer that the high level client OpenSearch.Client uses.</summary>
	internal class DefaultHighLevelSerializer : IOpenSearchSerializer, IInternalSerializer
	{
		private readonly OpenSearchClientSerializerOptions _serializerOptions;
		private readonly IConnectionSettingsValues _settings;

		// Non-null only on the legacy Utf8Json path. When set, all (de)serialization is routed through the
		// vendored Utf8Json engine using this resolver and the System.Text.Json options are never built.
		private readonly IJsonFormatterResolver _formatterResolver;

		public DefaultHighLevelSerializer(IConnectionSettingsValues settings)
		{
			_settings = settings;
			// Select the engine once, at construction, from the resolved setting (which already folds in the
			// OSC_USE_UTF8JSON environment default). STJ is the default; Utf8Json is the rollback path.
			if (settings != null && settings.UseUtf8Json)
				_formatterResolver = new OpenSearchClientFormatterResolver(settings);
			else
				_serializerOptions = new OpenSearchClientSerializerOptions(settings);
		}

		// Legacy Utf8Json entry point retained so stateful serializers (see StatefulSerializerExtensions) and
		// other resolver-based callers can construct a serializer directly from a formatter resolver.
		public DefaultHighLevelSerializer(IJsonFormatterResolver formatterResolver)
		{
			_formatterResolver = formatterResolver;
			_settings = (formatterResolver as IJsonFormatterResolverWithSettings)?.Settings;
		}

		private bool UsesUtf8Json => _formatterResolver != null;

		bool IInternalSerializer.TryGetJsonFormatter(out IJsonFormatterResolver formatterResolver)
		{
			formatterResolver = _formatterResolver;
			return UsesUtf8Json;
		}

		bool IInternalSerializer.TryGetJsonSerializerOptions(out JsonSerializerOptions options)
		{
			if (UsesUtf8Json)
			{
				options = null;
				return false;
			}
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
			if (IsNullOrEmpty(ref stream)) return default;
			if (UsesUtf8Json) return Utf8JsonSerializer.Deserialize<T>(stream, _formatterResolver);
			return JsonSerializer.Deserialize<T>(stream, OptionsForRoot(typeof(T), _serializerOptions.Options));
		}

		public object Deserialize(Type type, Stream stream)
		{
			if (IsNullOrEmpty(ref stream)) return type is { IsValueType: true } ? Activator.CreateInstance(type) : null;
			if (UsesUtf8Json) return Utf8JsonSerializer.NonGeneric.Deserialize(type, stream, _formatterResolver);
			return JsonSerializer.Deserialize(stream, type, OptionsForRoot(type, _serializerOptions.Options));
		}

		public async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		{
			var empty = await IsNullOrEmptyAsync(stream, cancellationToken).ConfigureAwait(false);
			if (empty.IsEmpty) return default;
			if (UsesUtf8Json) return await Utf8JsonSerializer.DeserializeAsync<T>(empty.Stream, _formatterResolver).ConfigureAwait(false);
			return await JsonSerializer.DeserializeAsync<T>(empty.Stream, OptionsForRoot(typeof(T), _serializerOptions.Options), cancellationToken).ConfigureAwait(false);
		}

		public async Task<object> DeserializeAsync(Type type, Stream stream, CancellationToken cancellationToken = default)
		{
			var empty = await IsNullOrEmptyAsync(stream, cancellationToken).ConfigureAwait(false);
			if (empty.IsEmpty) return type is { IsValueType: true } ? Activator.CreateInstance(type) : null;
			if (UsesUtf8Json) return await Utf8JsonSerializer.NonGeneric.DeserializeAsync(type, empty.Stream, _formatterResolver).ConfigureAwait(false);
			return await JsonSerializer.DeserializeAsync(empty.Stream, type, OptionsForRoot(type, _serializerOptions.Options), cancellationToken).ConfigureAwait(false);
		}

		// A response with no body (e.g. a HEAD request such as Ping, or a 200 with an empty payload) can
		// arrive as an empty, possibly non-seekable, network stream. STJ throws "The input does not contain
		// any JSON tokens" on such input, so short-circuit to default. For a seekable stream we can check the
		// length directly; for a non-seekable stream we peek a single byte and, if present, splice it back in
		// front of the remaining data so deserialization sees the full body.
		private static bool IsNullOrEmpty(ref Stream stream)
		{
			if (stream == null) return true;
			if (stream.CanSeek) return stream.Length == 0 || stream.Position >= stream.Length;

			var first = stream.ReadByte();
			if (first == -1) return true;
			stream = new PrependByteStream((byte)first, stream);
			return false;
		}

		private static async Task<(bool IsEmpty, Stream Stream)> IsNullOrEmptyAsync(Stream stream, CancellationToken cancellationToken)
		{
			if (stream == null) return (true, null);
			if (stream.CanSeek) return (stream.Length == 0 || stream.Position >= stream.Length, stream);

			var buffer = new byte[1];
			var read = await stream.ReadAsync(buffer, 0, 1, cancellationToken).ConfigureAwait(false);
			if (read == 0) return (true, stream);
			return (false, new PrependByteStream(buffer[0], stream));
		}

		public virtual void Serialize<T>(T data, Stream writableStream, SerializationFormatting formatting = SerializationFormatting.None)
		{
			if (UsesUtf8Json)
			{
				Utf8JsonSerializer.Serialize(writableStream, data, _formatterResolver);
				return;
			}
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			JsonSerializer.Serialize(writableStream, data, OptionsForRoot(typeof(T), options));
		}

		public Task SerializeAsync<T>(T data, Stream stream, SerializationFormatting formatting = SerializationFormatting.None,
			CancellationToken cancellationToken = default)
		{
			if (UsesUtf8Json)
				return Utf8JsonSerializer.SerializeAsync(stream, data, _formatterResolver);
			var options = formatting == SerializationFormatting.Indented
				? _serializerOptions.Indented
				: _serializerOptions.Options;
			return JsonSerializer.SerializeAsync(stream, data, OptionsForRoot(typeof(T), options), cancellationToken);
		}

		/// <summary>
		/// A read-only forward-only stream that yields a single already-read "peek" byte before delegating
		/// to the underlying stream. Used to non-destructively test a non-seekable response stream for
		/// emptiness while still allowing the full body to be deserialized.
		/// </summary>
		private sealed class PrependByteStream : Stream
		{
			private readonly byte _first;
			private readonly Stream _inner;
			private bool _firstConsumed;

			public PrependByteStream(byte first, Stream inner)
			{
				_first = first;
				_inner = inner;
			}

			public override bool CanRead => true;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (count <= 0) return 0;
				if (!_firstConsumed)
				{
					_firstConsumed = true;
					buffer[offset] = _first;
					return 1;
				}
				return _inner.Read(buffer, offset, count);
			}

			public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				if (count <= 0) return 0;
				if (!_firstConsumed)
				{
					_firstConsumed = true;
					buffer[offset] = _first;
					return 1;
				}
				return await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
			}

			public override void Flush() => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			protected override void Dispose(bool disposing)
			{
				if (disposing) _inner.Dispose();
				base.Dispose(disposing);
			}
		}
	}
}
