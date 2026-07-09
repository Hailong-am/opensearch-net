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
	/// A <see cref="JsonConverterFactory"/> that delegates serialization of user document types
	/// to the configured <see cref="IConnectionSettingsValues.SourceSerializer"/>.
	/// This replaces the Utf8Json-based <c>SourceFormatter&lt;T&gt;</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This factory is registered as the lowest-precedence domain converter in
	/// <see cref="OpenSearchClientSerializerOptions"/>, acting as a catch-all for user document
	/// types that are not handled by any other registered converter.
	/// </para>
	/// <para>
	/// Fast path: if the source serializer implements <see cref="IInternalSerializer"/> and
	/// exposes <see cref="JsonSerializerOptions"/>, the converter delegates directly to the
	/// <see cref="Utf8JsonReader"/>/<see cref="Utf8JsonWriter"/> without buffering.
	/// </para>
	/// <para>
	/// Slow path: if the source serializer is an opaque <see cref="IOpenSearchSerializer"/>,
	/// JSON is buffered to a stream and passed to the serializer's stream-based methods.
	/// </para>
	/// </remarks>
	internal sealed class SourceConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		private readonly IConnectionSettingsValues _settings;

		public SourceConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		/// <inheritdoc />
		/// <remarks>
		/// Returns <c>true</c> for types that should be serialized by the SourceSerializer.
		/// This includes types NOT defined in the OpenSearch.Net or OpenSearch.Client assemblies,
		/// effectively acting as a catch-all for user document types (e.g., the generic <c>T</c>
		/// in <c>SearchResponse&lt;T&gt;</c>).
		/// </remarks>
		public override bool CanConvert(Type typeToConvert)
		{
			if (typeToConvert == null) return false;

			// System.Object must be handled by STJ's runtime-type dispatch, never by the
			// source serializer. Capturing it here causes infinite recursion when the JsonNet
			// source serializer round-trips a nested OpenSearch type back through the built-in
			// serializer as object (HandleOscTypesOnSourceJsonConverter -> builtin -> SourceConverter<object>).
			if (typeToConvert == typeof(object))
				return false;

			// Primitive types and common BCL types are handled by STJ natively
			if (typeToConvert.IsPrimitive || typeToConvert == typeof(string) || typeToConvert == typeof(decimal)
				|| typeToConvert == typeof(DateTime) || typeToConvert == typeof(DateTimeOffset)
				|| typeToConvert == typeof(Guid) || typeToConvert == typeof(Uri)
				|| typeToConvert == typeof(byte[]))
				return false;

			// Nullable primitives
			var underlying = Nullable.GetUnderlyingType(typeToConvert);
			if (underlying != null && (underlying.IsPrimitive || underlying == typeof(decimal)
				|| underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
				|| underlying == typeof(Guid)))
				return false;

			// Types from the OpenSearch framework assemblies are NOT source types
			var assembly = typeToConvert.Assembly;
			var assemblyName = assembly.GetName().Name;
			if (assemblyName != null &&
				(assemblyName.StartsWith("OpenSearch.Net", StringComparison.Ordinal) ||
				 assemblyName.StartsWith("OpenSearch.Client", StringComparison.Ordinal)))
				return false;

			// Everything else is a user document type that should be delegated to the SourceSerializer
			return true;
		}

		/// <inheritdoc />
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var converterType = typeof(SourceConverter<>).MakeGenericType(type);
				return (JsonConverter)Activator.CreateInstance(converterType, _settings);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> that delegates serialization/deserialization of user
	/// document type <typeparamref name="T"/> to the configured SourceSerializer.
	/// </summary>
	/// <typeparam name="T">The user document type being serialized or deserialized.</typeparam>
	internal sealed class SourceConverter<T> : JsonConverter<T>
	{
		private readonly IConnectionSettingsValues _settings;

		public SourceConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		/// <inheritdoc />
		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Fast path: if source serializer is STJ-based, delegate directly
			if (_settings.SourceSerializer is IInternalSerializer s
				&& s.TryGetJsonSerializerOptions(out var sourceOptions))
			{
				if (ReferenceEquals(sourceOptions, options))
					return JsonSerializer.Deserialize<T>(ref reader, SourceConverterHelper.DefaultOptions);
				return JsonSerializer.Deserialize<T>(ref reader, sourceOptions);
			}

			// Slow path: buffer JSON to a stream, then pass to the source serializer
			using var doc = JsonDocument.ParseValue(ref reader);
			using var ms = _settings.MemoryStreamFactory.Create();
			using (var writer = new Utf8JsonWriter(ms))
			{
				doc.WriteTo(writer);
				writer.Flush();
			}
			ms.Position = 0;
			return _settings.SourceSerializer.Deserialize<T>(ms);
		}

		/// <inheritdoc />
		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			// Fast path: if source serializer is STJ-based, delegate directly
			if (_settings.SourceSerializer is IInternalSerializer s
				&& s.TryGetJsonSerializerOptions(out var sourceOptions))
			{
				if (ReferenceEquals(sourceOptions, options))
					JsonSerializer.Serialize(writer, value, SourceConverterHelper.DefaultOptions);
				else
					JsonSerializer.Serialize(writer, value, sourceOptions);
				return;
			}

			// Slow path: serialize to buffer stream, then write raw JSON to the writer
			using var ms = _settings.MemoryStreamFactory.Create();
			_settings.SourceSerializer.Serialize(value, ms, SerializationFormatting.None);
			ms.Position = 0;
			using var doc = JsonDocument.Parse(ms);
			doc.RootElement.WriteTo(writer);
		}
	}

	internal static class SourceConverterHelper
	{
		internal static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
	}
}
