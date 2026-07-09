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

			// Generic collection types (IList<T>, IEnumerable<T>, etc.) whose element types
			// are from OpenSearch assemblies are NOT user document types — they are domain
			// collections that STJ's built-in collection handling should process, with element
			// converters resolved from the options. Without this check, SourceConverter claims
			// them and delegates to JsonNet, which can't handle OpenSearch interfaces/types.
			if (typeToConvert.IsGenericType)
			{
				var args = typeToConvert.GetGenericArguments();
				if (args.Length > 0 && AllOpenSearchTypes(args))
					return false;
			}

			// Everything else is a user document type that should be delegated to the SourceSerializer
			return true;
		}

		private static bool AllOpenSearchTypes(Type[] types)
		{
			foreach (var t in types)
			{
				var name = t.Assembly.GetName().Name;
				if (name == null)
					return false;
				if (!name.StartsWith("OpenSearch.Net", StringComparison.Ordinal)
					&& !name.StartsWith("OpenSearch.Client", StringComparison.Ordinal))
					return false;
			}
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
					return JsonSerializer.Deserialize<T>(ref reader, SourceConverterHelper.GetRecursionSafeOptions(options, typeof(T)));
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
					JsonSerializer.Serialize(writer, value, SourceConverterHelper.GetRecursionSafeOptions(options, typeof(T)));
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

		// Cache of recursion-safe options keyed by the originating domain options instance. These
		// preserve every domain converter (JoinField, RelationName, enums, etc.) so that OpenSearch
		// domain types embedded in a user document still serialize correctly, but strip the
		// document-delegating factories (SourceConverterFactory / ProxyRequestConverterFactory) so a
		// value handled here does not recurse straight back into this converter.
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<JsonSerializerOptions, JsonSerializerOptions>
			RecursionSafeOptionsCache = new();

		internal static JsonSerializerOptions GetRecursionSafeOptions(JsonSerializerOptions domainOptions, Type targetType = null)
		{
			if (domainOptions == null)
				return DefaultOptions;

			// For plain BCL types (e.g. Dictionary<string, object> used to read index settings) use the
			// stripped Web-default options so untyped `object` values round-trip as JsonElement, exactly
			// as before the domain-aware recursion guard was introduced. Only OpenSearch domain document
			// types need the domain converters (JoinField, RelationName, enums) preserved.
			if (targetType != null && IsBclType(targetType))
				return DefaultOptions;

			return RecursionSafeOptionsCache.GetOrAdd(domainOptions, static source =>
			{
				var copy = new JsonSerializerOptions(source);
				for (var i = copy.Converters.Count - 1; i >= 0; i--)
				{
					var converter = copy.Converters[i];
					if (converter is SourceConverterFactory || converter is ProxyRequestConverterFactory)
						copy.Converters.RemoveAt(i);
				}
				return copy;
			});
		}

		/// <summary>
		/// True if the type (and, for a generic type, all of its type arguments) lives in a System.*
		/// assembly — i.e. it is a plain BCL type such as <c>Dictionary&lt;string, object&gt;</c> rather
		/// than an OpenSearch domain document type.
		/// </summary>
		private static bool IsBclType(Type type)
		{
			var name = type.Assembly.GetName().Name;
			var isSystem = name != null
				&& (name.StartsWith("System", StringComparison.Ordinal)
					|| name.Equals("mscorlib", StringComparison.Ordinal)
					|| name.Equals("netstandard", StringComparison.Ordinal));
			if (!isSystem)
				return false;

			if (type.IsGenericType)
			{
				foreach (var arg in type.GetGenericArguments())
				{
					if (!IsBclType(arg))
						return false;
				}
			}

			return true;
		}
	}
}
