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
using System.Text.Json.Serialization.Metadata;
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
		// Instance-level cache: SourceConverter<T> captures this factory's settings (and therefore its
		// SourceSerializer). A static cache would leak the first client's settings to every other client,
		// e.g. a client configured with a custom source serializer would reuse the default serializer.
		private readonly ConcurrentDictionary<Type, JsonConverter> _converterCache = new();

		private readonly IConnectionSettingsValues _settings;

		public SourceConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		/// <summary>The connection settings (and thus the configured SourceSerializer) this factory is bound to.</summary>
		internal IConnectionSettingsValues Settings => _settings;

		/// <inheritdoc />
		/// <remarks>
		/// Returns <c>true</c> for types that should be serialized by the SourceSerializer.
		/// This includes types NOT defined in the OpenSearch.Net or OpenSearch.Client assemblies,
		/// effectively acting as a catch-all for user document types (e.g., the generic <c>T</c>
		/// in <c>SearchResponse&lt;T&gt;</c>).
		/// </remarks>
		public override bool CanConvert(Type typeToConvert) => IsSourceDocumentType(typeToConvert);

		/// <summary>
		/// Whether <paramref name="typeToConvert"/> is a user document type that should be serialized by
		/// the SourceSerializer rather than the high-level domain machinery. Exposed statically so the
		/// high-level serializer can recognize a bare top-level document and route it through the
		/// inference-aware terminal source options (matching the pre-STJ behavior, where a document
		/// serialized at the JSON root honored OSC field-name inference rather than a distinct source
		/// serializer's own naming — the source serializer only applied to nested <c>_source</c> members).
		/// </summary>
		internal static bool IsSourceDocumentType(Type typeToConvert)
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

			// Custom (user-assembly) implementations of an OpenSearch domain contract interface
			// (e.g. a plugin IProperty) are domain types, not source documents. They must be handled
			// by the high-level contract machinery, not delegated to the source serializer.
			foreach (var iface in typeToConvert.GetInterfaces())
			{
				var ifaceAssembly = iface.Assembly.GetName().Name;
				if (ifaceAssembly != null &&
					(ifaceAssembly.StartsWith("OpenSearch.Net", StringComparison.Ordinal) ||
					 ifaceAssembly.StartsWith("OpenSearch.Client", StringComparison.Ordinal)) &&
					InterfaceDataContract.IsDefinedOn(iface))
					return false;
			}

			// Generic collection types (IList<T>, IDictionary<K,V>, etc.) whose element types
			// are NOT user document types are handled by STJ's built-in collection handling,
			// with element converters resolved from the options. This covers both OpenSearch
			// domain collections (e.g. IList<IProperty>) and dynamic collections keyed/valued by
			// simple framework types such as Dictionary<string, object> (e.g. script params).
			// Delegating the latter to the SourceSerializer would drop the registered element
			// converters (e.g. the fractional-number converter that emits 1.0 rather than 1).
			if (typeToConvert.IsGenericType)
			{
				var args = typeToConvert.GetGenericArguments();
				if (args.Length > 0 && AllNonUserTypes(args) && IsStjConstructible(typeToConvert))
					return false;
			}

			// Everything else is a user document type that should be delegated to the SourceSerializer
			return true;
		}

		/// <summary>
		/// Whether STJ can construct <paramref name="type"/> on deserialize. Interfaces and arrays
		/// map to STJ's default concrete collection; concrete types need a public parameterless
		/// constructor. Types that are not STJ-constructible (e.g. <c>ReadOnlyDictionary&lt;,&gt;</c>)
		/// must still be delegated to the source serializer so round-tripping keeps working.
		/// </summary>
		private static bool IsStjConstructible(Type type)
		{
			if (type.IsInterface || type.IsArray)
				return true;
			return type.GetConstructor(Type.EmptyTypes) != null;
		}

		private static bool AllNonUserTypes(Type[] types)
		{
			foreach (var t in types)
			{
				if (!IsNonUserType(t))
					return false;
			}
			return true;
		}

		private static bool IsNonUserType(Type t)
		{
			// System.Object and simple BCL types are handled natively by STJ (with the
			// registered ObjectConverter / fractional-number converters for object values).
			if (t == typeof(object) || t == typeof(string) || t.IsPrimitive || t == typeof(decimal)
				|| t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid))
				return true;

			var underlying = Nullable.GetUnderlyingType(t);
			if (underlying != null)
				return IsNonUserType(underlying);

			// Unwrap nested generic collections (e.g. IList<ISuggestContextQuery> nested inside a
			// IDictionary<string, IList<...>>) so a domain collection of domain types is still
			// recognised as a non-user type and handled by STJ's built-in collection processing.
			if (t.IsGenericType)
			{
				var innerArgs = t.GetGenericArguments();
				if (innerArgs.Length > 0 && AllNonUserTypes(innerArgs))
					return true;
			}

			// Types from the OpenSearch framework assemblies are domain types, not user documents.
			var name = t.Assembly.GetName().Name;
			return name != null
				&& (name.StartsWith("OpenSearch.Net", StringComparison.Ordinal)
					|| name.StartsWith("OpenSearch.Client", StringComparison.Ordinal));
		}

		/// <inheritdoc />
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return _converterCache.GetOrAdd(typeToConvert, type =>
			{
				var converterType = typeof(SourceConverter<>).MakeGenericType(type);
				return (JsonConverter)Activator.CreateInstance(converterType, _settings);
			});
		}

		/// <summary>
		/// Creates a <see cref="SourceConverter{T}"/> for <paramref name="typeToConvert"/> that delegates
		/// to the SourceSerializer, bypassing the <see cref="CanConvert"/> exclusions. Used to pin a
		/// source converter onto contract properties marked <see cref="SourceSerializationAttribute"/>
		/// (e.g. an <see cref="object"/>-typed embedded document payload).
		/// </summary>
		internal JsonConverter CreateSourceConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			// Do NOT use the shared static ConverterCache here: that cache is keyed only by type,
			// but each SourceConverter<T> is bound to this factory's settings (and its SourceSerializer).
			// This path is the sole creator of SourceConverter<object>, so caching it statically would
			// leak one settings instance's converter into other settings instances.
			var converterType = typeof(SourceConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	/// <summary>
	/// Non-generic marker for <see cref="SourceConverter{T}"/> so callers can detect that the domain
	/// options resolve a given type to the source-delegating converter without knowing its type argument.
	/// </summary>
	internal interface ISourceConverter { }

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> that delegates serialization/deserialization of user
	/// document type <typeparamref name="T"/> to the configured SourceSerializer.
	/// </summary>
	/// <typeparam name="T">The user document type being serialized or deserialized.</typeparam>
	internal sealed class SourceConverter<T> : JsonConverter<T>, ISourceConverter
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
				// When the source serializer IS the high-level serializer (no distinct source
				// serializer configured), delegating back to the same options would re-enter this
				// converter and recurse infinitely. Use the terminal source options — a POCO-oriented
				// clone that honors OSC property naming but does NOT apply the high-level domain
				// converters (Field->string, EnumMember->string, etc.), matching how a plain user
				// document round-trips through a distinct source serializer.
				if (ReferenceEquals(sourceOptions, options))
					return JsonSerializer.Deserialize<T>(ref reader, SourceConverterHelper.GetDefaultSourceOptions(_settings));
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
					JsonSerializer.Serialize(writer, value, SourceConverterHelper.GetDefaultSourceOptions(_settings));
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

		// Cache of "self source" options (a clone of the high-level options without the
		// SourceConverterFactory), keyed by the original options instance. Used when the source
		// serializer is the high-level serializer itself, to avoid infinite recursion while still
		// applying the full domain converter set to an embedded OpenSearch payload.
		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions>
			SelfSourceOptionsCache = new();

		internal static JsonSerializerOptions GetSelfSourceOptions(JsonSerializerOptions options) =>
			SelfSourceOptionsCache.GetValue(options, static o =>
			{
				var clone = new JsonSerializerOptions(o);
				for (var i = clone.Converters.Count - 1; i >= 0; i--)
				{
					if (clone.Converters[i] is SourceConverterFactory)
						clone.Converters.RemoveAt(i);
				}
				return clone;
			});
		// Per-settings options used by the built-in high-level source serializer to (de)serialize user
		// document types. These honor OpenSearch.Client property-name inference (PropertyName/Text(Name)
		// attributes, DataMember, DefaultMappingFor, DefaultFieldNameInferrer) via a settings-aware
		// contract modifier, but do NOT register SourceConverterFactory (which would recurse) — they are
		// the terminal serializer for POCO graphs.
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<IConnectionSettingsValues, JsonSerializerOptions>
			SourceOptionsCache = new();

		internal static JsonSerializerOptions GetDefaultSourceOptions(IConnectionSettingsValues settings) =>
			SourceOptionsCache.GetOrAdd(settings, static s =>
			{
				var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
				{
					DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
					PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
					// Match the base serializer's minimal escaping (only JSON-required characters +
					// U+0085/U+2028/U+2029), matching the historical Utf8Json wire behavior, rather than
					// STJ's default encoder that escapes U+007F and all non-allow-listed code points.
					Encoder = OpenSearch.Net.MinimalJsonEscapingEncoder.Shared,
					TypeInfoResolver = new DefaultJsonTypeInfoResolver
					{
						Modifiers =
						{
							DataMemberPropertyNameModifier.Modify,
							new SourcePropertyNameModifier(s).Modify
						}
					}
				};
				// ObjectConverter so object-typed values deserialize to .NET primitives (not JsonElement)
				// and, crucially, object-keyed dictionaries (IDictionary<object, object>) can (de)serialize
				// their keys — STJ's built-in object converter has no property-name support and throws.
				options.Converters.Add(new OpenSearch.Net.ObjectConverter());

				// Fractional-number converters so whole-valued doubles/floats/decimals keep their
				// trailing ".0" (e.g. 1.0 rather than 1) on the terminal source path, matching the
				// base low-level serializer (OpenSearchNetSerializerOptions) behavior.
				options.Converters.Add(new OpenSearch.Net.DoubleConverter());
				options.Converters.Add(new OpenSearch.Net.SingleConverter());
				options.Converters.Add(new OpenSearch.Net.DecimalConverter());

				// ISO-8601 date converters so flexible offset / fractional-second date strings on user
				// documents round-trip (STJ's built-in DateTime/DateTimeOffset readers are stricter).
				options.Converters.Add(new OpenSearch.Net.IsoDateTimeConverter());
				options.Converters.Add(new OpenSearch.Net.IsoDateTimeOffsetConverter());
				// TimeSpan serialized as ticks (a long) on user documents, matching the wire format.
				options.Converters.Add(new OpenSearch.Net.TimeSpanTicksConverter());
				options.Converters.Add(new OpenSearch.Net.NullableTimeSpanTicksConverter());

				// Read-only / non-parameterless dictionary types (ReadOnlyDictionary<,> and custom
				// IReadOnlyDictionary/IDictionary implementations) that STJ cannot construct on read.
				options.Converters.Add(new ReadOnlyDictionaryConverterFactory());

				// Union<,> support so user-document properties typed as a domain union (e.g.
				// Union<bool, ISourceFilter>) round-trip on the terminal source path. Union members that
				// are OpenSearch types resolve via their own attribute-channel converters.
				options.Converters.Add(new UnionConverterFactory());

				// ValueTuple support: STJ does not serialize a value tuple's public Item1..ItemN fields
				// unless IncludeFields is enabled, so a tuple-typed document property would otherwise be
				// written as an empty object. This converter emits the Item fields verbatim.
				options.Converters.Add(new ValueTupleConverter());

				// OpenSearch.Client value types that can appear as fields on user documents and need
				// settings-aware serialization even on the terminal source path.
				options.Converters.Add(new JoinFieldConverter(s));

				// Id support so a JoinField child's parent id (and any Id-typed user-document property)
				// (de)serializes on the terminal source path. JoinFieldConverter.Read resolves the child
				// "parent" via JsonSerializer.Deserialize<Id>, which otherwise falls through to STJ's
				// default object converter and throws ("cannot convert to OpenSearch.Client.Id").
				options.Converters.Add(new IdConverterFactory(s));

				// Settings-aware Field converter so a Field-typed property (or a Fields collection, whose
				// FieldsConverter resolves each element via options.GetConverter(typeof(Field))) reachable
				// from a user type (e.g. SourceFilter.Includes on a Union<bool, ISourceFilter> document
				// property) serializes to its resolved field-name string rather than a Field POCO object.
				options.Converters.Add(new FieldConverterFactory(s));

				// OpenSearch.Client domain types (QueryContainer, mappings, aggregations, etc.) that appear as
				// members of a user document must serialize through the full high-level domain machinery, not
				// the POCO-oriented terminal options — otherwise e.g. a percolator query stored on
				// ProjectPercolation.Query would emit an empty object (QueryContainer exposes only explicit
				// interface members) and its inner query bodies would lose their field-name-query wrapping.
				// This factory detects such domain-contract types and delegates them to the domain options.
				options.Converters.Add(new EmbeddedDomainTypeConverterFactory(s));
				return options;
			});
	}

	/// <summary>
	/// A <see cref="DefaultJsonTypeInfoResolver"/> modifier that resolves JSON property names for user
	/// document types using the OpenSearch.Client <see cref="Inferrer"/>, so that the built-in source
	/// serializer honors <c>[PropertyName]</c>/<c>[Text(Name=...)]</c> attributes,
	/// <c>DefaultMappingFor&lt;T&gt;</c> property mappings, and the <c>DefaultFieldNameInferrer</c>.
    /// Only applied to non-OpenSearch (user) types; framework types keep their existing contracts.
	/// </summary>
	internal sealed class SourcePropertyNameModifier
	{
		private readonly IConnectionSettingsValues _settings;

		public SourcePropertyNameModifier(IConnectionSettingsValues settings) => _settings = settings;

		public void Modify(System.Text.Json.Serialization.Metadata.JsonTypeInfo typeInfo)
		{
			if (typeInfo.Kind != System.Text.Json.Serialization.Metadata.JsonTypeInfoKind.Object)
				return;

			var assemblyName = typeInfo.Type.Assembly.GetName().Name;
			if (assemblyName != null &&
				(assemblyName.StartsWith("OpenSearch.Net", StringComparison.Ordinal) ||
				 assemblyName.StartsWith("OpenSearch.Client", StringComparison.Ordinal)))
				return;

			for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
			{
				var property = typeInfo.Properties[i];
				if (property.AttributeProvider is not System.Reflection.PropertyInfo propInfo)
					continue;

				// Mirror the Utf8Json resolver's GetMapping precedence:
				//   PropertyMappings (DefaultMappingFor) > OSC property attribute > source PropertyMappingProvider.
				if (!_settings.PropertyMappings.TryGetValue(propInfo, out var propertyMapping))
					propertyMapping = OpenSearchPropertyAttributeBase.From(propInfo);

				var serializerMapping = _settings.PropertyMappingProvider?.CreatePropertyMapping(propInfo);

				var overrideIgnore = propertyMapping?.Ignore ?? serializerMapping?.Ignore;
				if (overrideIgnore == true)
				{
					typeInfo.Properties.RemoveAt(i);
					continue;
				}

				var nameOverride = propertyMapping?.Name ?? serializerMapping?.Name;
				if (!string.IsNullOrEmpty(nameOverride))
				{
					property.Name = nameOverride;
					continue;
				}

				// No explicit override: apply the DefaultFieldNameInferrer to the CLR member name.
				var inferred = _settings.DefaultFieldNameInferrer(propInfo.Name);
				if (!string.IsNullOrEmpty(inferred))
					property.Name = inferred;
			}
		}
	}
}
