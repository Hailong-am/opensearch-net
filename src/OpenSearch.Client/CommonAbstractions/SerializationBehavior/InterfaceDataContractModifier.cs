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
	/// A <see cref="DefaultJsonTypeInfoResolver"/> modifier that ADDITIVELY adjusts the JSON contract
	/// produced by the default resolver so that types behave the way Utf8Json's
	/// <c>[InterfaceDataContract]</c> behavior expected — <b>without</b> clearing the default property set.
	/// <para>
	/// The default STJ contract is kept intact (so options-registered converter factories still resolve
	/// for every default-surfaced property, which is what lets the central converter registrations in
	/// <see cref="OpenSearchClientSerializerOptions"/> do the heavy lifting). On top of that the modifier
	/// only layers on:
	/// </para>
	/// <list type="bullet">
	/// <item>interface [DataMember(Name="...")] name resolution and [IgnoreDataMember] exclusion;</item>
	/// <item>non-public [DataMember] members (e.g. ResponseBase.Error / StatusCode);</item>
	/// <item>parameterless-constructor selection and non-public setter support;</item>
	/// <item>exclusion of types STJ cannot serialize (System.Type, delegates, ...);</item>
	/// <item>type-level <c>ShouldSerialize(IConnectionSettingsValues)</c> emission guards (e.g. Routing)
	/// and conditionless-QueryContainer omission;</item>
	/// <item>and — critically for descriptors that implement their contract via <b>explicit interface
	/// implementation</b> (e.g. <c>IAnalyzers IAnalysis.Analyzers { get; set; }</c>), which the default
	/// resolver never surfaces — the ADDITION of the missing contract-interface [DataMember] members.
	/// Those hand-built properties bypass the options converter-factory list at depth, so the settings-aware
	/// converters (Field/IndexName/enum/source/etc.) are pinned onto them explicitly.</item>
	/// </list>
	/// </summary>
	internal sealed class InterfaceDataContractModifier
	{
		// Enum-member converter factory used to pin the correct string/numeric enum converter onto
		// added-contract enum properties (bypassing the options factory list at depth).
		private static readonly OpenSearch.Net.EnumMemberConverterFactory EnumMemberConverterFactoryInstance =
			new OpenSearch.Net.EnumMemberConverterFactory(useVerbatimName: true);

		private readonly IConnectionSettingsValues _settings;

		public InterfaceDataContractModifier(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public void Modify(JsonTypeInfo typeInfo)
		{
			if (typeInfo.Kind != JsonTypeInfoKind.Object)
				return;

			var type = typeInfo.Type;

			// For interface types being serialized directly (e.g. IRenameProcessor),
			// apply [DataMember] directly from the interface properties.
			if (type.IsInterface)
			{
				ApplyDirectDataMemberNames(typeInfo);
				ExcludeUnsupportedTypes(typeInfo);
				ApplyTypeLevelShouldSerialize(typeInfo);
				return;
			}

			// Apply interface DataMember name resolution for ALL types (not just [InterfaceDataContract])
			// because many OpenSearch types have [DataMember(Name="...")] on interface properties.
			ApplyInterfaceDataMemberNames(typeInfo, type);

			// Apply [IgnoreDataMember] from interfaces.
			ApplyInterfaceIgnoreDataMember(typeInfo, type);

			// Add non-public [DataMember] properties for deserialization.
			AddNonPublicDataMembers(typeInfo, type);

			// For types that participate in an interface data contract, suppress default-surfaced public
			// properties that are not part of that contract (e.g. query-string parameters like TypedKeys
			// that have no [DataMember]/[PropertyName]). This mirrors the Utf8Json [InterfaceDataContract]
			// behavior where only annotated members were emitted. Done non-destructively (ShouldSerialize)
			// so the kept properties keep their default converter resolution.
			SuppressNonContractDefaultMembers(typeInfo, type);

			// Add contract-interface [DataMember] members that the default resolver did not surface
			// (explicit interface implementations, e.g. descriptors). This is what makes descriptors and
			// request classes serialize against their interface contract without a destructive rebuild.
			AddMissingInterfaceDataMembers(typeInfo, type);

			// For contract types, pin settings-aware converters onto default-surfaced (implicitly
			// implemented) contract properties whose value type requires them (enums, Field/Index/etc.).
			// Without this, such a property resolves through the options converter-factory list where the
			// lowest-precedence SourceConverterFactory (a catch-all that is not excluded for a Nullable<enum>,
			// whose own assembly is CoreLib) claims it and — under source_serializer=true — delegates a
			// [Flags] enum to the source serializer, dropping the "AND|NEAR" formatting.
			PinConvertersOnDefaultContractMembers(typeInfo, type);

			// Pick parameterless constructor for deserialization (public or non-public).
			ResolveConstructor(typeInfo, type);

			// Support internal/private setters for deserialization.
			SupportNonPublicSetters(typeInfo);

			// Exclude properties whose types STJ cannot handle (System.Type, etc.).
			ExcludeUnsupportedTypes(typeInfo);

			// Honor type-level ShouldSerialize hooks and conditionless-QueryContainer omission.
			ApplyTypeLevelShouldSerialize(typeInfo);
		}

		/// <summary>
		/// For interface types serialized directly, apply [DataMember(Name="...")] from
		/// the interface's own properties.
		/// </summary>
		private static void ApplyDirectDataMemberNames(JsonTypeInfo typeInfo)
		{
			foreach (var prop in typeInfo.Properties)
			{
				var member = prop.AttributeProvider;
				if (member == null) continue;

				if (member.IsDefined(typeof(IgnoreDataMemberAttribute), true))
				{
					prop.ShouldSerialize = static (_, _) => false;
					continue;
				}

				var attrs = member.GetCustomAttributes(typeof(DataMemberAttribute), true);
				if (attrs.Length > 0)
				{
					var dmAttr = (DataMemberAttribute)attrs[0];
					if (!string.IsNullOrEmpty(dmAttr.Name))
						prop.Name = dmAttr.Name;
				}
			}
		}

		/// <summary>
		/// Resolves property names from interface [DataMember(Name="...")] declarations.
		/// When the concrete property doesn't have [DataMember] but the interface does, use the interface's name.
		/// </summary>
		private static void ApplyInterfaceDataMemberNames(JsonTypeInfo typeInfo, Type type)
		{
			if (type.IsInterface) return;

			var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var toRemove = new List<JsonPropertyInfo>();

			foreach (var prop in typeInfo.Properties)
			{
				var member = prop.AttributeProvider;
				if (member == null) continue;

				var directAttrs = member.GetCustomAttributes(typeof(DataMemberAttribute), true);
				if (directAttrs.Length > 0)
				{
					var dmAttr = (DataMemberAttribute)directAttrs[0];
					if (!string.IsNullOrEmpty(dmAttr.Name))
						prop.Name = dmAttr.Name;
				}
				else if (member is PropertyInfo propInfo)
				{
					var interfaceName = GetInterfaceDataMemberName(type, propInfo.Name);
					if (interfaceName != null)
						prop.Name = interfaceName;
				}

				if (!seenNames.Add(prop.Name))
					toRemove.Add(prop);
			}

			foreach (var dup in toRemove)
				typeInfo.Properties.Remove(dup);
		}

		/// <summary>
		/// Checks [IgnoreDataMember] on both the concrete property and interface declarations.
		/// </summary>
		private static void ApplyInterfaceIgnoreDataMember(JsonTypeInfo typeInfo, Type type)
		{
			if (type.IsInterface) return;

			foreach (var prop in typeInfo.Properties)
			{
				var member = prop.AttributeProvider;
				if (member == null) continue;

				if (member.IsDefined(typeof(IgnoreDataMemberAttribute), true))
				{
					prop.ShouldSerialize = static (_, _) => false;
					continue;
				}

				if (member is PropertyInfo propInfo)
				{
					foreach (var iface in type.GetInterfaces())
					{
						var ifaceProp = iface.GetProperty(propInfo.Name);
						if (ifaceProp != null && ifaceProp.IsDefined(typeof(IgnoreDataMemberAttribute), true))
						{
							prop.ShouldSerialize = static (_, _) => false;
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Adds non-public properties that have [DataMember] to the type info (walking the base hierarchy),
		/// e.g. response types with internal setters like ServerError / ResponseBase.Error.
		/// </summary>
		private static void AddNonPublicDataMembers(JsonTypeInfo typeInfo, Type type)
		{
			if (type.IsInterface) return;

			var existingNames = new HashSet<string>(
				typeInfo.Properties.Select(p => p.Name),
				StringComparer.OrdinalIgnoreCase);

			for (var baseType = type; baseType != null && baseType != typeof(object); baseType = baseType.BaseType)
			{
				var nonPublicProps = baseType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				foreach (var pi in nonPublicProps)
				{
					var dmAttr = pi.GetCustomAttribute<DataMemberAttribute>();
					if (dmAttr == null) continue;
					if (IsUnsupportedType(pi.PropertyType)) continue;

					var name = !string.IsNullOrEmpty(dmAttr.Name) ? dmAttr.Name : pi.Name;
					if (existingNames.Contains(name)) continue;

					var jsonProp = typeInfo.CreateJsonPropertyInfo(pi.PropertyType, name);
					jsonProp.AttributeProvider = pi;

					var getter = pi.GetGetMethod(true);
					if (getter != null)
						jsonProp.Get = obj => getter.Invoke(obj, null);

					var setter = pi.GetSetMethod(true);
					if (setter != null)
						jsonProp.Set = (obj, val) => setter.Invoke(obj, new[] { val });

					typeInfo.Properties.Add(jsonProp);
					existingNames.Add(name);
				}
			}
		}

		/// <summary>
		/// For types participating in an interface data contract, suppresses (non-destructively) the
		/// default-surfaced public properties that carry no contract annotation — i.e. neither a
		/// [DataMember] (on the concrete member or a same-named interface member) nor a [PropertyName].
		/// This prevents request query-string parameters (e.g. TypedKeys / typed_keys) from leaking into
		/// the serialized request body, matching the Utf8Json [InterfaceDataContract] behavior where only
		/// annotated members were emitted.
		/// </summary>
		private static void SuppressNonContractDefaultMembers(JsonTypeInfo typeInfo, Type type)
		{
			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			var toRemove = new List<JsonPropertyInfo>();

			foreach (var prop in typeInfo.Properties)
			{
				if (prop.AttributeProvider is not PropertyInfo pi)
					continue;

				// Keep members explicitly annotated for serialization.
				if (pi.GetCustomAttribute<DataMemberAttribute>() != null
					|| pi.GetCustomAttribute<PropertyNameAttribute>() != null)
					continue;

				// Locate the same-named contract-interface [DataMember] declaration, if any.
				PropertyInfo ifaceMember = null;
				foreach (var iface in type.GetInterfaces())
				{
					var ifaceProp = iface.GetProperty(pi.Name);
					if (ifaceProp?.GetCustomAttribute<DataMemberAttribute>() != null)
					{
						ifaceMember = ifaceProp;
						break;
					}
				}

				if (ifaceMember != null)
				{
					// An implicit interface implementation has the exact same property type as the
					// interface member. If the public property's type differs (e.g. a convenience
					// `bool Coerce` shadowing the explicit `bool? INumberProperty.Coerce`), it is a
					// convenience shadow, not the contract member — remove it so the real (nullable,
					// null-omitting) interface member is added by AddMissingInterfaceDataMembers.
					if (ifaceMember.PropertyType == pi.PropertyType)
						continue; // implicit implementation: this IS the contract member — keep it.

					toRemove.Add(prop);
					continue;
				}

				// Not part of the contract (e.g. request query-string parameters like TypedKeys) — drop it.
				toRemove.Add(prop);
			}

			foreach (var prop in toRemove)
				typeInfo.Properties.Remove(prop);
		}

		/// <summary>
		/// For a contract type, pins the settings-aware converter onto each default-surfaced (implicitly
		/// implemented) contract property whose value type needs one — mirroring the pinning applied to
		/// hand-built members in <see cref="AddMissingInterfaceDataMembers"/>. Leaves properties that
		/// already have a custom converter (e.g. from a [JsonConverter] attribute) untouched.
		/// </summary>
		private static void PinConvertersOnDefaultContractMembers(JsonTypeInfo typeInfo, Type type)
		{
			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			foreach (var prop in typeInfo.Properties)
			{
				if (prop.CustomConverter != null)
					continue;

				PinConverterIfNeeded(prop, prop.PropertyType, typeInfo.Options);
			}
		}

		/// <summary>
		/// Adds the contract-interface [DataMember] members that the default resolver did not surface as
		/// public properties. This covers descriptors (explicit interface implementations) and any request
		/// class whose interface declares body members not present on the concrete type's public surface.
		/// The hand-built properties bypass the options converter-factory list at depth, so settings-aware
		/// converters are pinned onto them explicitly.
		/// </summary>
		private void AddMissingInterfaceDataMembers(JsonTypeInfo typeInfo, Type type)
		{
			if (type.IsAbstract) return;

			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			var existingNames = new HashSet<string>(
				typeInfo.Properties.Select(p => p.Name),
				StringComparer.Ordinal);

			foreach (var iface in contractInterfaces)
			{
				foreach (var ifaceProp in iface.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (ifaceProp.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
						continue;

					var dataMember = ifaceProp.GetCustomAttribute<DataMemberAttribute>();
					if (dataMember == null)
						continue;

					var jsonName = dataMember.Name
						?? JsonNamingPolicy.CamelCase.ConvertName(ifaceProp.Name);

					if (!existingNames.Add(jsonName))
						continue;

					var jsonProp = typeInfo.CreateJsonPropertyInfo(ifaceProp.PropertyType, jsonName);

					// [SourceSerialization] members carry an embedded user-document payload that must be
					// (de)serialized by the SourceSerializer rather than the high-level serializer.
					if (ifaceProp.GetCustomAttribute<SourceSerializationAttribute>() != null)
					{
						var sourceConverter = GetSourceConverter(typeInfo.Options, ifaceProp.PropertyType);
						if (sourceConverter != null)
							jsonProp.CustomConverter = sourceConverter;

						BindAccessors(jsonProp, ifaceProp, type);
						typeInfo.Properties.Add(jsonProp);
						continue;
					}

					// Honor an explicit [JsonConverter] attribute on the interface property.
					var converterAttr = ifaceProp.GetCustomAttribute<JsonConverterAttribute>();
					if (converterAttr != null)
					{
						var converter = converterAttr.ConverterType != null
							? (JsonConverter)Activator.CreateInstance(converterAttr.ConverterType)
							: converterAttr.CreateConverter(ifaceProp.PropertyType);
						if (converter != null)
							jsonProp.CustomConverter = converter;
					}

					PinConverterIfNeeded(jsonProp, ifaceProp.PropertyType, typeInfo.Options);

					BindAccessors(jsonProp, ifaceProp, type);

					// Conditionless QueryContainer omission and type-level ShouldSerialize hooks.
					if (ifaceProp.PropertyType == typeof(QueryContainer))
						jsonProp.ShouldSerialize = (_, value) => value is QueryContainer qc && qc.IsWritable;
					else
						ApplyTypeLevelShouldSerialize(jsonProp, ifaceProp.PropertyType);

					typeInfo.Properties.Add(jsonProp);
				}
			}
		}

		/// <summary>
		/// Binds a hand-built <see cref="JsonPropertyInfo"/> to the interface member's getter/setter,
		/// falling back to the concrete type's (possibly non-public) setter for getter-only interface members.
		/// </summary>
		private static void BindAccessors(JsonPropertyInfo jsonProp, PropertyInfo ifaceProp, Type type)
		{
			var captured = ifaceProp;
			if (captured.CanRead)
				jsonProp.Get = obj => captured.GetValue(obj);
			if (captured.CanWrite)
			{
				jsonProp.Set = (obj, value) => captured.SetValue(obj, value);
			}
			else
			{
				var concreteProp = type.GetProperty(ifaceProp.Name,
					BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				var concreteSetter = concreteProp?.GetSetMethod(true);
				if (concreteSetter != null)
					jsonProp.Set = (obj, value) => concreteSetter.Invoke(obj, new[] { value });
			}
		}

		/// <summary>
		/// Pins the options-registered converter for leaf value types, enums, Field-keyed dictionaries and
		/// IEnumerable&lt;double&gt; onto a hand-built property (these are skipped by the options factory list
		/// when a property is resolved at depth on a modified contract).
		/// </summary>
		private static void PinConverterIfNeeded(JsonPropertyInfo jsonProp, Type propType, JsonSerializerOptions options)
		{
			var underlying = Nullable.GetUnderlyingType(propType) ?? propType;

			if (underlying.IsEnum && EnumMemberConverterFactoryInstance.CanConvert(propType))
			{
				try
				{
					var conv = EnumMemberConverterFactoryInstance.CreateConverter(propType, options);
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
					var conv = options.GetConverter(propType);
					if (conv != null && conv.GetType().Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
						jsonProp.CustomConverter = conv;
				}
				catch { /* leave unset */ }
			}
			else if (TryGetFieldKeyedDictionaryValueType(propType, out var dictValueType))
			{
				try
				{
					var convType = typeof(FieldKeyedDictionaryConverter<>).MakeGenericType(dictValueType);
					jsonProp.CustomConverter = (JsonConverter)Activator.CreateInstance(convType);
				}
				catch { /* leave unset */ }
			}
			else if (propType == typeof(IEnumerable<double>))
			{
				jsonProp.CustomConverter = new DoubleEnumerableConverter();
			}
		}

		/// <summary>
		/// Picks the parameterless constructor (public or non-public) for deserialization.
		/// </summary>
		private static void ResolveConstructor(JsonTypeInfo typeInfo, Type type)
		{
			if (type.IsAbstract || type.IsInterface) return;

			var ctor = type.GetConstructor(
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
				null, Type.EmptyTypes, null);

			if (ctor != null)
				typeInfo.CreateObject = () => ctor.Invoke(null);
		}

		/// <summary>
		/// Enables non-public setters for deserialization.
		/// </summary>
		private static void SupportNonPublicSetters(JsonTypeInfo typeInfo)
		{
			foreach (var prop in typeInfo.Properties)
			{
				if (prop.Set != null) continue;

				if (prop.AttributeProvider is PropertyInfo propertyInfo && propertyInfo.CanWrite)
				{
					var setter = propertyInfo.GetSetMethod(true);
					if (setter != null)
						prop.Set = (obj, val) => setter.Invoke(obj, new[] { val });
				}
			}
		}

		/// <summary>
		/// Excludes properties whose types cannot be serialized by System.Text.Json.
		/// </summary>
		private static void ExcludeUnsupportedTypes(JsonTypeInfo typeInfo)
		{
			foreach (var prop in typeInfo.Properties)
			{
				if (IsUnsupportedType(prop.PropertyType))
				{
					prop.ShouldSerialize = static (_, _) => false;
					continue;
				}

				if (prop.Name == "TypeId" || prop.Name == "typeId")
				{
					if (typeof(Attribute).IsAssignableFrom(typeInfo.Type))
					{
						prop.ShouldSerialize = static (_, _) => false;
						continue;
					}
				}

				if (prop.PropertyType == typeof(object) && typeof(Attribute).IsAssignableFrom(typeInfo.Type))
				{
					if (prop.AttributeProvider is PropertyInfo pi && pi.Name == "TypeId")
						prop.ShouldSerialize = static (_, _) => false;
				}
			}
		}

		private static bool IsUnsupportedType(Type t) =>
			t == typeof(Type)
			|| typeof(Type).IsAssignableFrom(t)
			|| typeof(MemberInfo).IsAssignableFrom(t)
			|| typeof(Delegate).IsAssignableFrom(t);

		private static string GetInterfaceDataMemberName(Type type, string propertyName)
		{
			foreach (var iface in type.GetInterfaces())
			{
				var ifaceProp = iface.GetProperty(propertyName);
				if (ifaceProp == null) continue;

				var attr = ifaceProp.GetCustomAttribute<DataMemberAttribute>();
				if (attr != null && !string.IsNullOrEmpty(attr.Name))
					return attr.Name;
			}
			return null;
		}

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, MethodInfo> ShouldSerializeMethods = new();

		/// <summary>
		/// Applies type-level <c>ShouldSerialize(IConnectionSettingsValues)</c> emission guards to every
		/// default-surfaced property whose type declares one, and conditionless-query omission to
		/// QueryContainer properties. Preserves any existing predicate (e.g. an [IgnoreDataMember] exclusion).
		/// </summary>
		private void ApplyTypeLevelShouldSerialize(JsonTypeInfo typeInfo)
		{
			foreach (var prop in typeInfo.Properties)
			{
				var propType = prop.PropertyType;
				if (propType == typeof(QueryContainer))
				{
					var existing = prop.ShouldSerialize;
					prop.ShouldSerialize = (obj, value) =>
						(existing == null || existing(obj, value))
						&& value is QueryContainer qc && qc.IsWritable;
					continue;
				}

				ApplyTypeLevelShouldSerialize(prop, propType);
			}
		}

		/// <summary>
		/// Applies the type-level <c>ShouldSerialize(IConnectionSettingsValues)</c> hook (if the property
		/// type declares one) as a STJ ShouldSerialize predicate, preserving any existing predicate.
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
			var existing = jsonProp.ShouldSerialize;
			jsonProp.ShouldSerialize = (obj, value) =>
				(existing == null || existing(obj, value))
				&& value != null && (bool)method.Invoke(value, new object[] { settings });
		}

		private static JsonConverter GetSourceConverter(JsonSerializerOptions options, Type propertyType)
		{
			foreach (var converter in options.Converters)
			{
				if (converter is SourceConverterFactory factory)
				{
					try { return factory.CreateSourceConverter(propertyType, options); }
					catch { return null; }
				}
			}
			return null;
		}

		/// <summary>
		/// Concrete non-generic value types whose serialization depends on settings-aware converter factories.
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

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> ContractInterfaceCache = new();

		/// <summary>
		/// Returns whether <paramref name="iface"/> participates in an interface data contract, recognizing
		/// (1) an explicit <c>InterfaceDataContractAttribute</c> matched by simple name — so both the
		/// OpenSearch.Client-native <see cref="InterfaceDataContractAttribute"/> and the legacy Utf8Json
		/// attribute are honored — and (2), as a marker-agnostic fallback, any OpenSearch.Client-assembly
		/// interface that declares at least one <see cref="DataMemberAttribute"/> property. The fallback lets
		/// the migration drop most explicit markers while still rebuilding the contract, and keeps the modifier
		/// decoupled from the Utf8Json engine.
		/// </summary>
		private static bool IsContractInterface(Type iface) =>
			ContractInterfaceCache.GetOrAdd(iface, static i =>
			{
				// Explicit marker (OSC-native or legacy Utf8Json, matched by simple name).
				foreach (var attr in i.GetCustomAttributes(inherit: false))
				{
					if (attr.GetType().Name == nameof(InterfaceDataContractAttribute))
						return true;
				}

				// Marker-agnostic fallback: an OpenSearch.Client-assembly interface that declares at least
				// one [DataMember] property is a data contract. This lets domain interfaces participate in
				// the interface-contract rebuild without an explicit [InterfaceDataContract] marker, so the
				// migration does not have to sprinkle the attribute across the domain. Restricted to the
				// client assembly so user/plugin interfaces are never force-rebuilt.
				var asm = i.Assembly.GetName().Name;
				if (asm == null || !asm.StartsWith("OpenSearch.Client", StringComparison.Ordinal))
					return false;

				foreach (var p in i.GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (p.GetCustomAttribute<DataMemberAttribute>() != null)
						return true;
				}
				return false;
			});

		/// <summary>
		/// Returns all interfaces in <paramref name="type"/>'s hierarchy that participate in the interface
		/// data contract — reachable from an interface marked <see cref="InterfaceDataContractAttribute"/> —
		/// most-derived first so overriding [DataMember] names win.
		/// </summary>
		private static List<Type> GetContractInterfaces(Type type)
		{
			var roots = type.GetInterfaces()
				.Where(IsContractInterface)
				.ToList();

			if (roots.Count == 0)
				return roots;

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
