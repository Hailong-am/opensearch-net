/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	internal sealed class MultiGetRequestConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public MultiGetRequestConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert == typeof(IMultiGetRequest)
			|| typeToConvert == typeof(MultiGetRequest)
			|| typeToConvert == typeof(MultiGetDescriptor);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			(JsonConverter)Activator.CreateInstance(
				typeof(MultiGetRequestConverter<>).MakeGenericType(typeToConvert), _settings);
	}

	internal sealed class MultiGetRequestConverter<T> : JsonConverter<T>
		where T : IMultiGetRequest
	{
		private readonly IConnectionSettingsValues _settings;

		public MultiGetRequestConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			var request = (IMultiGetRequest)value;
			if (request?.Documents == null || !request.Documents.Any())
			{
				writer.WriteEndObject();
				return;
			}

			List<IMultiGetOperation> docs;

			// If an index is specified at the request level and a document has the same index,
			// clear the document-level index so it is not emitted redundantly.
			if (request.Index != null)
			{
				var resolvedIndex = request.Index.GetString(_settings);
				docs = request.Documents.Select(d =>
					{
						if (d.Index == null)
							return d;

						var docIndex = d.Index.GetString(_settings);
						if (string.Equals(resolvedIndex, docIndex))
							d.Index = null;
						return d;
					})
					.ToList();
			}
			else
				docs = request.Documents.ToList();

			var flatten = docs.All(p => p.CanBeFlattened);

			writer.WritePropertyName(flatten ? "ids" : "docs");
			writer.WriteStartArray();
			foreach (var doc in docs)
			{
				if (flatten)
					JsonSerializer.Serialize(writer, doc.Id, options);
				else
					JsonSerializer.Serialize(writer, doc, options);
			}
			writer.WriteEndArray();

			writer.WriteEndObject();
		}
	}
}
