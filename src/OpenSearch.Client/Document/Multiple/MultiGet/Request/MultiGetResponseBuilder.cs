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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Builds a <see cref="MultiGetResponse"/> from the <c>{ "docs": [ ... ] }</c> envelope.
	/// The order of the returned documents mirrors the order of the operations in the request, so each
	/// hit is deserialized as a <see cref="MultiGetHit{T}"/> using the CLR type of the corresponding
	/// operation.
	/// </summary>
	/// <remarks>
	/// Dual-engine: when the built-in serializer exposes a Utf8Json formatter resolver (the current
	/// default), delegates to the stateful <see cref="MultiGetResponseFormatter"/> — the same path
	/// <c>main</c> uses. Only when the serializer exposes STJ options (once the default engine switches
	/// in a later PR of #388) does this builder parse and dispatch the STJ way below.
	/// </remarks>
	internal class MultiGetResponseBuilder : CustomResponseBuilderBase
	{
		private static readonly ConcurrentDictionary<Type, MethodInfo> DeserializeHitMethods = new();

		private static readonly MethodInfo DeserializeHitGenericMethod =
			typeof(MultiGetResponseBuilder).GetMethod(nameof(DeserializeHit), BindingFlags.Static | BindingFlags.NonPublic);

		public MultiGetResponseBuilder(IMultiGetRequest request) => Request = request;

		private IMultiGetRequest Request { get; }

		public override object DeserializeResponse(IOpenSearchSerializer builtInSerializer, IApiCallDetails response, Stream stream)
		{
			if (!response.Success)
				return new MultiGetResponse();

			if (builtInSerializer is IInternalSerializer internalSerializer && internalSerializer.TryGetJsonFormatter(out var formatterResolver))
				return builtInSerializer.CreateStateful(new MultiGetResponseFormatter(Request)).Deserialize<MultiGetResponse>(stream);

			if (IsEmpty(stream))
				return new MultiGetResponse();

			using var doc = JsonDocument.Parse(stream);
			return Build(builtInSerializer, doc);
		}

		public override async Task<object> DeserializeResponseAsync(
			IOpenSearchSerializer builtInSerializer,
			IApiCallDetails response,
			Stream stream,
			CancellationToken ctx = default
		)
		{
			if (!response.Success)
				return new MultiGetResponse();

			if (builtInSerializer is IInternalSerializer internalSerializer && internalSerializer.TryGetJsonFormatter(out var formatterResolver))
				return await builtInSerializer.CreateStateful(new MultiGetResponseFormatter(Request))
					.DeserializeAsync<MultiGetResponse>(stream, ctx)
					.ConfigureAwait(false);

			if (IsEmpty(stream))
				return new MultiGetResponse();

			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx).ConfigureAwait(false);
			return Build(builtInSerializer, doc);
		}

		// Mocked/unit-test responses (URL and HTTP-method assertions) provide an empty response stream;
		// JsonDocument.Parse throws on empty input, so short-circuit to an empty response.
		private static bool IsEmpty(Stream stream) =>
			stream == null || (stream.CanSeek && stream.Length == 0);

		private MultiGetResponse Build(IOpenSearchSerializer builtInSerializer, JsonDocument doc)
		{
			var response = new MultiGetResponse();

			if (Request?.Documents == null)
				return response;

			if (doc.RootElement.ValueKind != JsonValueKind.Object
				|| !doc.RootElement.TryGetProperty("docs", out var docs)
				|| docs.ValueKind != JsonValueKind.Array)
				return response;

			// The built-in high level serializer exposes the STJ options used for domain deserialization.
			if (builtInSerializer is not IInternalSerializer internalSerializer
				|| !internalSerializer.TryGetJsonSerializerOptions(out var options))
				return response;

			using var operationEnumerator = Request.Documents.GetEnumerator();
			foreach (var hitElement in docs.EnumerateArray())
			{
				if (!operationEnumerator.MoveNext())
					break;

				var clrType = operationEnumerator.Current.ClrType ?? typeof(object);
				var method = DeserializeHitMethods.GetOrAdd(clrType, static t => DeserializeHitGenericMethod.MakeGenericMethod(t));
				var hit = (IMultiGetHit<object>)method.Invoke(null, new object[] { hitElement, options });
				if (hit != null)
					response.InternalHits.Add(hit);
			}

			return response;
		}

		private static IMultiGetHit<object> DeserializeHit<T>(JsonElement element, JsonSerializerOptions options)
			where T : class =>
			element.Deserialize<MultiGetHit<T>>(options);
	}
}
