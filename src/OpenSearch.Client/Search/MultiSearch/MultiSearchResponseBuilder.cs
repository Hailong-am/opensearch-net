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
	/// Builds a <see cref="MultiSearchResponse"/> from the <c>{ "took": ..., "responses": [ ... ] }</c>
	/// envelope. The responses array is in the same order as the request operations, so each element is
	/// deserialized as a <see cref="SearchResponse{T}"/> using the CLR type of the corresponding operation
	/// and stored under the operation's key (matching the pre-STJ stateful <c>MultiSearchResponseFormatter</c>).
	/// </summary>
	internal class MultiSearchResponseBuilder : CustomResponseBuilderBase
	{
		private static readonly ConcurrentDictionary<Type, MethodInfo> DeserializeResponseMethods = new();

		private static readonly MethodInfo DeserializeResponseGenericMethod =
			typeof(MultiSearchResponseBuilder).GetMethod(nameof(DeserializeSearchResponse), BindingFlags.Static | BindingFlags.NonPublic);

		public MultiSearchResponseBuilder(IRequest request) => Request = request;

		private IRequest Request { get; }

		public override object DeserializeResponse(IOpenSearchSerializer builtInSerializer, IApiCallDetails response, Stream stream)
		{
			if (!response.Success)
				return new MultiSearchResponse();

			if (builtInSerializer is IInternalSerializer internalSerializer && internalSerializer.TryGetJsonFormatter(out var formatterResolver))
				return builtInSerializer.CreateStateful(new MultiSearchResponseFormatter(Request)).Deserialize<MultiSearchResponse>(stream);

			if (IsEmpty(stream))
				return new MultiSearchResponse();

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
				return new MultiSearchResponse();

			if (builtInSerializer is IInternalSerializer internalSerializer && internalSerializer.TryGetJsonFormatter(out var formatterResolver))
				return await builtInSerializer.CreateStateful(new MultiSearchResponseFormatter(Request))
					.DeserializeAsync<MultiSearchResponse>(stream, ctx)
					.ConfigureAwait(false);

			if (IsEmpty(stream))
				return new MultiSearchResponse();

			using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx).ConfigureAwait(false);
			return Build(builtInSerializer, doc);
		}

		// Mocked/unit-test responses (URL and HTTP-method assertions) provide an empty response stream;
		// JsonDocument.Parse throws on empty input, so short-circuit to an empty response.
		private static bool IsEmpty(Stream stream) =>
			stream == null || (stream.CanSeek && stream.Length == 0);

		private MultiSearchResponse Build(IOpenSearchSerializer builtInSerializer, JsonDocument doc)
		{
			var response = new MultiSearchResponse();

			// The order and key of each operation is required to reconstruct the keyed responses.
			IEnumerable<KeyValuePair<string, ITypedSearchRequest>> operations;
			switch (Request)
			{
				case IMultiSearchRequest multiSearch when multiSearch.Operations != null:
					operations = Project(multiSearch.Operations);
					break;
				case IMultiSearchTemplateRequest multiSearchTemplate when multiSearchTemplate.Operations != null:
					operations = Project(multiSearchTemplate.Operations);
					break;
				default:
					return response;
			}

			if (doc.RootElement.ValueKind != JsonValueKind.Object)
				return response;

			if (doc.RootElement.TryGetProperty("took", out var took) && took.ValueKind == JsonValueKind.Number)
				response.Took = took.GetInt64();

			if (!doc.RootElement.TryGetProperty("responses", out var responses) || responses.ValueKind != JsonValueKind.Array)
				return response;

			if (builtInSerializer is not IInternalSerializer internalSerializer
				|| !internalSerializer.TryGetJsonSerializerOptions(out var options))
				return response;

			using var operationEnumerator = operations.GetEnumerator();
			foreach (var responseElement in responses.EnumerateArray())
			{
				if (!operationEnumerator.MoveNext())
					break;

				var operation = operationEnumerator.Current;
				var clrType = operation.Value.ClrType ?? typeof(object);
				var method = DeserializeResponseMethods.GetOrAdd(clrType, static t => DeserializeResponseGenericMethod.MakeGenericMethod(t));
				var searchResponse = (IResponse)method.Invoke(null, new object[] { responseElement, options });
				if (searchResponse != null)
					response.Responses[operation.Key] = searchResponse;
			}

			return response;
		}

		private static IEnumerable<KeyValuePair<string, ITypedSearchRequest>> Project<TValue>(IDictionary<string, TValue> operations)
			where TValue : ITypedSearchRequest
		{
			foreach (var kv in operations)
				yield return new KeyValuePair<string, ITypedSearchRequest>(kv.Key, kv.Value);
		}

		private static IResponse DeserializeSearchResponse<T>(JsonElement element, JsonSerializerOptions options)
			where T : class =>
			element.Deserialize<SearchResponse<T>>(options);
	}
}
