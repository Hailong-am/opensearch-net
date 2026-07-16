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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	public class SourceRequestResponseBuilder<TDocument> : CustomResponseBuilderBase
	{
		public static SourceRequestResponseBuilder<TDocument> Instance { get; } = new SourceRequestResponseBuilder<TDocument>();

		public override object DeserializeResponse(IOpenSearchSerializer builtInSerializer, IApiCallDetails response, Stream stream) =>
			response.Success
				? new SourceResponse<TDocument> { Body = ResolveSourceSerializer(builtInSerializer).Deserialize<TDocument>(stream) }
				: new SourceResponse<TDocument>();

		public override async Task<object> DeserializeResponseAsync(IOpenSearchSerializer builtInSerializer, IApiCallDetails response, Stream stream, CancellationToken ctx = default) =>
			response.Success
				? new SourceResponse<TDocument>
				{
					Body = await ResolveSourceSerializer(builtInSerializer).DeserializeAsync<TDocument>(stream, ctx).ConfigureAwait(false)
				}
				: new SourceResponse<TDocument>();

		// A _source response body IS the raw user document, so it must be deserialized by the configured
		// SourceSerializer — when a distinct source serializer is set (e.g. the JSON.NET serializer under
		// source_serializer=true) the built-in high-level serializer would not honor the source serializer's
		// converters. Resolve the SourceSerializer from whichever engine backs the built-in serializer: on
		// Utf8Json via the formatter resolver's connection settings, on STJ via the registered
		// SourceConverterFactory's bound settings. Falls back to the built-in serializer when unavailable —
		// that path already routes a bare document root through the inference-aware terminal source options.
		private static IOpenSearchSerializer ResolveSourceSerializer(IOpenSearchSerializer builtInSerializer)
		{
			if (builtInSerializer is not IInternalSerializer internalSerializer)
				return builtInSerializer;

			if (internalSerializer.TryGetJsonFormatter(out var formatter))
				return formatter.GetConnectionSettings().SourceSerializer;

			if (internalSerializer.TryGetJsonSerializerOptions(out var options))
			{
				foreach (var converter in options.Converters)
				{
					if (converter is SourceConverterFactory factory)
						return factory.Settings.SourceSerializer;
				}
			}

			return builtInSerializer;
		}
	}
}
