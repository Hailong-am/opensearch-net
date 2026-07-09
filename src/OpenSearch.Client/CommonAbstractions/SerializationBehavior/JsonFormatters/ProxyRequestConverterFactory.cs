/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for request types that delegate their body serialization
	/// to the configured source serializer via <see cref="IProxyRequest.WriteJson"/> (e.g.
	/// <see cref="IndexRequest{TDocument}"/>, <see cref="CreateRequest{TDocument}"/> and their descriptors).
	/// This is the System.Text.Json equivalent of the Utf8Json <c>ProxyRequestFormatterBase</c>.
	/// </summary>
	internal sealed class ProxyRequestConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		private readonly IConnectionSettingsValues _settings;

		public ProxyRequestConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IProxyRequest).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var converterType = typeof(ProxyRequestConverter<>).MakeGenericType(type);
				return (JsonConverter)Activator.CreateInstance(converterType, _settings);
			});
	}

	/// <summary>
	/// Serializes a proxy request by delegating to its <see cref="IProxyRequest.WriteJson"/> method,
	/// which writes the request's document/body via the configured source serializer.
	/// </summary>
	internal sealed class ProxyRequestConverter<T> : JsonConverter<T>
		where T : IProxyRequest
	{
		private readonly IConnectionSettingsValues _settings;

		public ProxyRequestConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Deserializing proxy requests is uncommon; buffer the document and hand it to the
			// source serializer, then construct the request from the resulting document.
			using var doc = JsonDocument.ParseValue(ref reader);

			var genericArgs = typeToConvert.GetGenericArguments();
			if (genericArgs.Length == 0)
				return default;

			var documentType = genericArgs[0];

			// The requested type may be the interface (e.g. IIndexRequest<T>) which has no
			// constructor — resolve the concrete request implementation (IndexRequest<T>).
			var concreteType = ResolveConcreteType(typeToConvert);
			if (concreteType == null)
				return default;

			using var ms = _settings.MemoryStreamFactory.Create();
			using (var writer = new Utf8JsonWriter(ms))
			{
				doc.WriteTo(writer);
				writer.Flush();
			}
			ms.Position = 0;

			var document = _settings.SourceSerializer.Deserialize(documentType, ms);

			var request = (T)concreteType.CreateInstance(document, null, null);

			return request;
		}

		private static Type ResolveConcreteType(Type typeToConvert)
		{
			if (!typeToConvert.IsInterface)
				return typeToConvert;

			// Convention: strip the leading 'I' from the interface name to find the concrete type
			// in the same namespace/assembly (IIndexRequest`1 -> IndexRequest`1).
			var name = typeToConvert.Name;
			if (name.Length > 1 && name[0] == 'I')
			{
				var concreteName = typeToConvert.Namespace + "." + name.Substring(1);
				var openConcrete = typeToConvert.Assembly.GetType(concreteName);
				if (openConcrete != null && openConcrete.IsGenericTypeDefinition)
					return openConcrete.MakeGenericType(typeToConvert.GetGenericArguments());
				if (openConcrete != null)
					return openConcrete;
			}

			return null;
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var proxyRequest = (IProxyRequest)value;
			using var ms = _settings.MemoryStreamFactory.Create();
			proxyRequest.WriteJson(_settings.SourceSerializer, ms, SerializationFormatting.None);
			ms.Position = 0;

			using var doc = JsonDocument.Parse(ms);
			doc.RootElement.WriteTo(writer);
		}
	}
}
