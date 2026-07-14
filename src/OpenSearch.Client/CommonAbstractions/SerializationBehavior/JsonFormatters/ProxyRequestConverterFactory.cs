/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for request types implementing <see cref="IProxyRequest"/>
	/// (e.g. <see cref="IIndexRequest{TDocument}"/>, <see cref="ICreateRequest{TDocument}"/>).
	/// <para>
	/// These requests do not serialize themselves against their interface contract; instead the body
	/// is the document written by <see cref="IProxyRequest.WriteJson"/> using the configured
	/// <see cref="IConnectionSettingsValues.SourceSerializer"/>. This replaces the Utf8Json-based
	/// <c>ProxyRequestFormatterBase</c>.
	/// </para>
	/// </summary>
	internal sealed class ProxyRequestConverterFactory : JsonConverterFactory
	{
		// Instance-level cache: the converter captures this factory's settings, so it must not be
		// shared across factories built for different IConnectionSettingsValues (e.g. clients with a
		// custom source serializer). A static cache would leak the first settings to all clients.
		private readonly ConcurrentDictionary<Type, JsonConverter> _converterCache = new();

		private readonly IConnectionSettingsValues _settings;

		public ProxyRequestConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IProxyRequest).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			_converterCache.GetOrAdd(typeToConvert, type =>
			{
				var converterType = typeof(ProxyRequestConverter<>).MakeGenericType(type);
				return (JsonConverter)Activator.CreateInstance(converterType, _settings);
			});
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> that writes a proxy request's body by delegating to
	/// <see cref="IProxyRequest.WriteJson"/> using the source serializer.
	/// </summary>
	internal sealed class ProxyRequestConverter<T> : JsonConverter<T>
		where T : class, IProxyRequest
	{
		private readonly IConnectionSettingsValues _settings;

		public ProxyRequestConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Deserializing proxy requests is uncommon. Buffer the JSON, deserialize the document type
			// via the source serializer, and construct the request around it.
			var requestType = ResolveConcreteRequestType(typeToConvert);
			if (requestType == null || !requestType.IsGenericType)
			{
				reader.Skip();
				return null;
			}

			var documentType = requestType.GetGenericArguments()[0];

			using var doc = JsonDocument.ParseValue(ref reader);
			using var ms = _settings.MemoryStreamFactory.Create();
			using (var writer = new Utf8JsonWriter(ms))
			{
				doc.WriteTo(writer);
				writer.Flush();
			}
			ms.Position = 0;
			var document = _settings.SourceSerializer.Deserialize(documentType, ms);

			var request = requestType.IsGenericTypeDefinition
				? requestType.CreateGenericInstance(documentType, document, null, null)
				: requestType.CreateInstance(document, null, null);

			return (T)request;
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// The document body is always written compact, matching the Utf8Json ProxyRequestFormatter
			// which wrote it via WriteRaw with SerializationFormatting.None regardless of the request-level
			// formatting (PrettyJson only indents the enclosing request, never the source document).
			using var ms = _settings.MemoryStreamFactory.Create();
			value.WriteJson(_settings.SourceSerializer, ms, SerializationFormatting.None);
			ms.Position = 0;

			if (ms.Length == 0)
			{
				writer.WriteNullValue();
				return;
			}

			// Write the pre-serialized bytes verbatim so the compact document is not re-indented by an
			// indented writer (WriteTo would re-format using the writer's options).
			writer.WriteRawValue(ms.ToArray(), skipInputValidation: false);
		}

		private static Type ResolveConcreteRequestType(Type typeToConvert)
		{
			// If the target is an interface (e.g. IIndexRequest<TDocument>), map to the concrete
			// request type (IndexRequest<TDocument>) living in the same namespace.
			if (!typeToConvert.IsInterface)
				return typeToConvert;

			if (!typeToConvert.IsGenericType)
				return null;

			var ifaceName = typeToConvert.Name; // e.g. IIndexRequest`1
			if (ifaceName.StartsWith("I", StringComparison.Ordinal))
			{
				var concreteName = typeToConvert.Namespace + "." + ifaceName.Substring(1);
				var concrete = typeToConvert.Assembly.GetType(concreteName);
				if (concrete != null && concrete.IsGenericTypeDefinition)
					return concrete.MakeGenericType(typeToConvert.GetGenericArguments());
			}

			return null;
		}
	}
}
