/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="DefaultJsonTypeInfoResolver"/> modifier that rebuilds the JSON contract
	/// for any concrete type implementing an interface marked with
	/// <see cref="InterfaceDataContractAttribute"/>.
	/// <para>
	/// This is the System.Text.Json equivalent of Utf8Json's <c>[InterfaceDataContract]</c> behavior:
	/// the type is serialized/deserialized purely against its interface contract. Only interface
	/// properties carrying a <see cref="DataMemberAttribute"/> are emitted, using the
	/// <see cref="DataMemberAttribute.Name"/> as the JSON name. This works uniformly for:
	/// </para>
	/// <list type="bullet">
	/// <item>Descriptors (explicit interface implementations, e.g. <c>IAliases IIndexState.Aliases</c>)</item>
	/// <item>Request classes (public properties that implement interface members, where non-body
	/// members like route values are <see cref="IgnoreDataMemberAttribute"/> on the interface)</item>
	/// </list>
	/// <para>
	/// Because it uses the interface property getters/setters, deserialization works for free —
	/// no write-only converter is needed.
	/// </para>
	/// </summary>
	internal sealed class InterfaceDataContractModifier
	{
		// Enum-member converter factory used to pin the correct string/numeric enum converter onto
		// rebuilt-contract enum properties (bypassing the options factory list, whose lowest-precedence
		// SourceConverterFactory also claims enums). Naming is verbatim, matching the base serializer.
		private static readonly OpenSearch.Net.EnumMemberConverterFactory EnumMemberConverterFactoryInstance =
			new OpenSearch.Net.EnumMemberConverterFactory(useVerbatimName: true);

		private readonly IConnectionSettingsValues _settings;

		public InterfaceDataContractModifier(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public void Modify(JsonTypeInfo typeInfo)
		{
			if (typeInfo.Kind != JsonTypeInfoKind.Object)
				return;

			// Never override a converter already resolved from a [JsonConverter] attribute
			// (dedicated polymorphic/wrapper converters). Their JsonTypeInfoKind is typically
			// None, but guard explicitly so collection-element resolution keeps the converter.
			if (typeInfo.Converter is not null && typeInfo.Converter.GetType().Namespace?.StartsWith("System") == false)
				return;

			var type = typeInfo.Type;

			if (type.IsInterface || type.IsAbstract)
			{
				// Interfaces/abstracts are not rebuilt here (their contract is produced by
				// DataMemberPropertyNameModifier), but a value serialized against its interface type
				// (e.g. each IMultiTermVectorOperation element of an IEnumerable<...> contract) must still
				// honor any type-level ShouldSerialize hook declared by a property's type — otherwise a
				// Routing that resolves to empty is emitted as "routing": null. Apply those hooks to the
				// existing interface-contract properties.
				foreach (var prop in typeInfo.Properties)
					ApplyTypeLevelShouldSerialize(prop, prop.PropertyType);
				return;
			}

			// Types serialized by a dedicated converter (dictionaries, scripts, etc.) never reach here.
			if (typeof(IIsADictionary).IsAssignableFrom(type))
				return;

			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			// User-defined analysis components (custom ITokenizer/ITokenFilter/ICharFilter/IAnalyzer/
			// INormalizer implementations declared outside the client assembly) are serialized by their
			// full public property set — matching the Utf8Json behavior where unknown analysis types fell
			// through to the object formatter. Interface [DataMember]s still take precedence for naming.
			var isUserDefinedAnalysisComponent = IsUserDefinedAnalysisComponent(type);

			// Build the contract from the interface [DataMember] properties.
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var rebuilt = new List<JsonPropertyInfo>();
			// CLR member names that the contract interfaces declare as [IgnoreDataMember] — their concrete
			// counterparts must never be emitted even if the concrete property carries its own annotation.
			var ignoredMemberNames = new HashSet<string>(StringComparer.Ordinal);

			foreach (var iface in contractInterfaces)
			{
				foreach (var ifaceProp in iface.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (ifaceProp.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
					{
						ignoredMemberNames.Add(ifaceProp.Name);
						continue;
					}

					var dataMember = ifaceProp.GetCustomAttribute<DataMemberAttribute>();
					if (dataMember == null)
						continue;

					var jsonName = dataMember.Name
						?? JsonNamingPolicy.CamelCase.ConvertName(ifaceProp.Name);

					if (!seen.Add(jsonName))
						continue;

					var jsonProp = typeInfo.CreateJsonPropertyInfo(ifaceProp.PropertyType, jsonName);

					// Properties marked [SourceSerialization] carry an embedded user-document payload
					// (typically typed as object) that must be (de)serialized by the SourceSerializer.
					// STJ would otherwise handle an object-typed property with the high-level serializer.
					if (ifaceProp.GetCustomAttribute<SourceSerializationAttribute>() != null)
					{
						var sourceConverter = GetSourceConverter(typeInfo.Options, ifaceProp.PropertyType);
						if (sourceConverter != null)
							jsonProp.CustomConverter = sourceConverter;

						var capturedSource = ifaceProp;
						if (capturedSource.CanRead)
							jsonProp.Get = obj => capturedSource.GetValue(obj);
						if (capturedSource.CanWrite)
							jsonProp.Set = (obj, value) => capturedSource.SetValue(obj, value);

						rebuilt.Add(jsonProp);
						continue;
					}

					// Honor an explicit [JsonConverter] attribute on the interface property. The
					// manually-rebuilt JsonPropertyInfo does not inherit attribute-channel converters,
					// so apply it here (e.g. SourceValueWriteConverter on weakly-typed query values).
					var converterAttr = ifaceProp.GetCustomAttribute<JsonConverterAttribute>();
					if (converterAttr != null)
					{
						var converter = converterAttr.ConverterType != null
							? (JsonConverter)Activator.CreateInstance(converterAttr.ConverterType)
							: converterAttr.CreateConverter(ifaceProp.PropertyType);
						if (converter != null)
							jsonProp.CustomConverter = converter;
					}

					// Pin the options-registered converter for leaf value types and enums.
					// These factory converters (Field/IndexName/RelationName, enums) are skipped for
					// properties on contracts rebuilt by this modifier when used at depth.
					var propType = ifaceProp.PropertyType;
					var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
					// Enums (including Nullable<enum>) that serialize to a string form ([StringEnum],
					// [Flags], or any [EnumMember] value) must be pinned to the enum-member converter.
					// The lowest-precedence SourceConverterFactory (a catch-all for non-framework types)
					// also claims enums — and a nullable enum's own assembly is CoreLib, so it is not
					// excluded as a framework type. Resolving through the options factory list would
					// therefore hand back a SourceConverter that delegates the enum to the source
					// serializer, dropping the [StringEnum]/[Flags] "AND|NEAR" formatting. Build the
					// enum-member converter directly instead of asking the options. Enums that serialize
					// numerically (e.g. GeoHashPrecision) are not claimed by the factory and are left to
					// STJ's numeric default.
					if (underlying.IsEnum && EnumMemberConverterFactoryInstance.CanConvert(propType))
					{
						try
						{
							var conv = EnumMemberConverterFactoryInstance.CreateConverter(propType, typeInfo.Options);
							if (conv != null)
								jsonProp.CustomConverter = conv;
						}
						catch { /* leave unset — STJ will resolve normally */ }
					}
					else if (!propType.IsGenericType && !propType.IsInterface && !propType.IsAbstract
						&& ConverterBackedValueTypes.Contains(underlying))
					{
						try
						{
							var conv = typeInfo.Options.GetConverter(propType);
							if (conv != null && conv.GetType().Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
								jsonProp.CustomConverter = conv;
						}
						catch { /* If GetConverter throws, leave it unset — STJ will resolve normally */ }
					}
					// Dictionaries keyed by Field need settings-aware key inference. STJ skips the
					// options-registered Field converter factory for dictionary keys at depth, so pin a
					// dedicated converter that resolves each key through the Field converter.
					else if (TryGetFieldKeyedDictionaryValueType(propType, out var dictValueType))
					{
						try
						{
							var convType = typeof(FieldKeyedDictionaryConverter<>).MakeGenericType(dictValueType);
							jsonProp.CustomConverter = (System.Text.Json.Serialization.JsonConverter)Activator.CreateInstance(convType);
						}
						catch { /* If construction fails, leave it unset — STJ will resolve normally */ }
					}
					// IEnumerable<double> properties must keep the client's trailing-".0" wire format for
					// integral values. STJ skips the options-registered DoubleConverter for collection
					// elements at depth, so pin a converter that routes each element through it.
					else if (propType == typeof(IEnumerable<double>))
					{
						jsonProp.CustomConverter = new DoubleEnumerableConverter();
					}

					var captured = ifaceProp;
					if (captured.CanRead)
						jsonProp.Get = obj => captured.GetValue(obj);
					if (captured.CanWrite)
						jsonProp.Set = (obj, value) => captured.SetValue(obj, value);

					// A QueryContainer that is conditionless (e.g. an empty bool query) must be
					// omitted entirely rather than emitted as "prop": null. Utf8Json achieved this
					// by not writing conditionless queries; mirror it with a ShouldSerialize guard.
					if (propType == typeof(QueryContainer))
						jsonProp.ShouldSerialize = (_, value) => value is QueryContainer qc && qc.IsWritable;
					else
						ApplyTypeLevelShouldSerialize(jsonProp, propType);

					rebuilt.Add(jsonProp);
				}
			}

			// Custom (e.g. plugin) implementations may declare additional public properties that carry
			// their own [PropertyName]/[DataMember] annotation but are not part of the contract interface.
			// Emit those too (honoring the interface-declared [IgnoreDataMember] exclusions), matching the
			// Utf8Json behavior where any [PropertyName]/[DataMember]-marked property is serialized.
			foreach (var clrProp in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (ignoredMemberNames.Contains(clrProp.Name))
					continue;

				var propertyNameAttr = clrProp.GetCustomAttribute<PropertyNameAttribute>();
				var dataMemberAttr = clrProp.GetCustomAttribute<DataMemberAttribute>();
				// For user-defined analysis components emit every readable/writable public property, even
				// unannotated ones (e.g. a plugin tokenizer's custom setting). Otherwise, only emit
				// properties carrying their own [PropertyName]/[DataMember] annotation.
				if (propertyNameAttr == null && dataMemberAttr == null && !isUserDefinedAnalysisComponent)
					continue;

				if (propertyNameAttr?.Ignore == true)
					continue;

				if (clrProp.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
					continue;

				// Skip indexers and non-public accessors for the unannotated fallback path.
				if (isUserDefinedAnalysisComponent && propertyNameAttr == null && dataMemberAttr == null
					&& clrProp.GetIndexParameters().Length > 0)
					continue;

				var jsonName = propertyNameAttr?.Name
					?? dataMemberAttr?.Name
					?? JsonNamingPolicy.CamelCase.ConvertName(clrProp.Name);

				if (!seen.Add(jsonName))
					continue;

				var jsonProp = typeInfo.CreateJsonPropertyInfo(clrProp.PropertyType, jsonName);

				var captured = clrProp;
				if (captured.CanRead)
					jsonProp.Get = obj => captured.GetValue(obj);
				if (captured.CanWrite)
					jsonProp.Set = (obj, value) => captured.SetValue(obj, value);

				ApplyTypeLevelShouldSerialize(jsonProp, clrProp.PropertyType);

				rebuilt.Add(jsonProp);
			}

			// If the rebuilt contract has no properties, decide between two cases:
			//  - The concrete class itself declares [DataMember] properties (e.g. response types such as
			//    GetResponse<T> whose members live on the class, not the interface). Leave the default
			//    contract intact so those class properties still (de)serialize.
			//  - Neither the interface nor the concrete class declares [DataMember] properties on their
			//    own members (e.g. GlobalAggregation, whose only inherited property Aggregations is not a
			//    [DataMember] and is emitted by the aggregation container, not inline). Fall through to
			//    rebuild the contract to an empty object so it serializes as {}.
			if (rebuilt.Count == 0 && ConcreteTypeDeclaresDataMember(type))
				return;

			// These types are (de)serialized purely by their property contract. Many define a
			// public convenience constructor (e.g. AverageAggregation(string name, Field field))
			// that STJ would otherwise treat as the deserialization constructor and fail to bind.
			// Force construction through a parameterless constructor instead.
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null, Type.EmptyTypes, modifiers: null);
			if (ctor != null)
				typeInfo.CreateObject = () => ctor.Invoke(null);

			// Rebuild the contract deterministically from the collected properties.
			typeInfo.Properties.Clear();
			foreach (var jsonProp in rebuilt)
				typeInfo.Properties.Add(jsonProp);
		}

		/// <summary>
		/// Cache of the type-level <c>bool ShouldSerialize(IConnectionSettingsValues)</c> method (if any)
		/// declared by a property type. This mirrors Utf8Json's per-type <c>ShouldSerialize</c> hook
		/// (see <c>ReflectionExtensions.GetShouldSerializeMethod</c>): any property whose <em>type</em>
		/// declares such a method had it invoked to decide whether the property is emitted at all.
		/// </summary>
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> ShouldSerializeMethods = new();

		/// <summary>
		/// Applies the type-level <c>ShouldSerialize(IConnectionSettingsValues)</c> hook (if the property
		/// type declares one) as a STJ <see cref="JsonPropertyInfo.ShouldSerialize"/> predicate. This is the
		/// System.Text.Json equivalent of Utf8Json's per-type <c>ShouldSerialize</c> emission guard, used by
		/// e.g. <see cref="Routing"/> so that a routing value that resolves to empty is omitted entirely
		/// rather than written as <c>"routing": null</c>.
		/// </summary>
		private void ApplyTypeLevelShouldSerialize(JsonPropertyInfo jsonProp, Type propType)
		{
			var method = ShouldSerializeMethods.GetOrAdd(propType, static t =>
				t.GetMethod("ShouldSerialize",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
					binder: null, new[] { typeof(IConnectionSettingsValues) }, modifiers: null) is { ReturnType: var rt } m
				&& rt == typeof(bool)
					? m
					: null);

			if (method == null)
				return;

			var settings = _settings;
			jsonProp.ShouldSerialize = (_, value) =>
				value != null && (bool)method.Invoke(value, new object[] { settings });
		}

		/// <summary>
		/// Resolves a <see cref="System.Text.Json.Serialization.JsonConverter"/> that delegates to the
		/// SourceSerializer for the given property type, by locating the <c>SourceConverterFactory</c>
		/// registered on the options. Returns <c>null</c> if it cannot be resolved.
		/// </summary>
		private static System.Text.Json.Serialization.JsonConverter GetSourceConverter(JsonSerializerOptions options, Type propertyType)
		{
			foreach (var converter in options.Converters)
			{
				if (converter is SourceConverterFactory factory)
				{
					try
					{
						return factory.CreateSourceConverter(propertyType, options);
					}
					catch
					{
						return null;
					}
				}
			}

			return null;
		}

		/// <summary>
		/// Analysis component base interfaces. A concrete type implementing one of these but defined
		/// outside the OpenSearch.Client assembly is a user-defined (plugin) analysis component whose
		/// full public property set must be serialized (matching the Utf8Json object-formatter fallback).
		/// </summary>
		private static readonly Type[] AnalysisComponentInterfaces =
		{
			typeof(IAnalyzer),
			typeof(INormalizer),
			typeof(ITokenizer),
			typeof(ITokenFilter),
			typeof(ICharFilter),
		};

		private static bool IsUserDefinedAnalysisComponent(Type type)
		{
			// Types shipped in the client assembly are handled by their explicit contracts.
			if (type.Assembly == typeof(InterfaceDataContractModifier).Assembly)
				return false;

			foreach (var iface in AnalysisComponentInterfaces)
			{
				if (iface.IsAssignableFrom(type))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns true if the concrete type (or a base class) declares at least one instance property
		/// carrying a <see cref="DataMemberAttribute"/>. Used to distinguish response-style types whose
		/// contract lives on the class from parameterless marker types (e.g. GlobalAggregation).
		/// </summary>
		private static bool ConcreteTypeDeclaresDataMember(Type type)
		{
			foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
			{
				if (prop.GetCustomAttribute<DataMemberAttribute>() != null)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Concrete non-generic value types whose serialization depends on settings-aware converter
		/// factories registered on the options. STJ skips options-registered factories for properties
		/// on rebuilt contracts at depth, so we pin the converter explicitly via CustomConverter.
		/// Only leaf (non-collection, non-interface) types are included to avoid interfering with
		/// STJ's built-in collection/interface resolution.
		/// </summary>
		private static readonly HashSet<Type> ConverterBackedValueTypes = new()
		{
			typeof(Field),
			typeof(IndexName),
			typeof(Indices),
			typeof(RelationName),
			typeof(Id),
			typeof(Routing),
			typeof(Name),
			typeof(Names),
			typeof(TaskId),
		};

		/// <summary>
		/// Determines whether <paramref name="propType"/> is an <see cref="IDictionary{TKey,TValue}"/>
		/// (or a type implementing it) whose key type is <see cref="Field"/>, returning the value type.
		/// </summary>
		private static bool TryGetFieldKeyedDictionaryValueType(Type propType, out Type valueType)
		{
			valueType = null;

			if (!propType.IsGenericType)
				return false;

			var def = propType.GetGenericTypeDefinition();
			if (def != typeof(IDictionary<,>) && def != typeof(Dictionary<,>))
				return false;

			var args = propType.GetGenericArguments();
			if (args.Length != 2 || args[0] != typeof(Field))
				return false;

			valueType = args[1];
			return true;
		}

		/// <summary>
		/// Returns all interfaces in <paramref name="type"/>'s hierarchy that participate in the
		/// interface data contract — i.e. reachable from an interface marked
		/// <see cref="InterfaceDataContractAttribute"/> — most-derived first so that overriding
		/// [DataMember] names win.
		/// </summary>
		private static List<Type> GetContractInterfaces(Type type)
		{
			var roots = type.GetInterfaces()
				.Where(i => i.GetCustomAttribute<InterfaceDataContractAttribute>() != null)
				.ToList();

			if (roots.Count == 0)
				return roots;

			// Order roots most-derived first, so a derived contract interface's [DataMember] properties
			// are emitted before the base interface's (e.g. ICompletionSuggester's "fuzzy" before
			// ISuggester's "field"/"size"). Type.GetInterfaces() does not guarantee this ordering.
			// A more-derived interface implements (transitively) more interfaces than its bases, so
			// ordering by implemented-interface count (descending) yields derived-first and is a stable sort.
			roots = roots
				.OrderByDescending(i => i.GetInterfaces().Length)
				.ToList();

			var ordered = new List<Type>();
			var seen = new HashSet<Type>();

			void Visit(Type iface)
			{
				if (!seen.Add(iface))
					return;
				ordered.Add(iface);
				foreach (var parent in iface.GetInterfaces())
				{
					if (parent.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
						continue;
					Visit(parent);
				}
			}

			foreach (var root in roots)
				Visit(root);

			return ordered;
		}
	}
}
