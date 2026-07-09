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
	internal static class InterfaceDataContractModifier
	{
		public static void Modify(JsonTypeInfo typeInfo)
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
				return;

			// Types serialized by a dedicated converter (dictionaries, scripts, etc.) never reach here.
			if (typeof(IIsADictionary).IsAssignableFrom(type))
				return;

			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			// Build the contract from the interface [DataMember] properties.
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var rebuilt = new List<JsonPropertyInfo>();

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

					if (!seen.Add(jsonName))
						continue;

					var jsonProp = typeInfo.CreateJsonPropertyInfo(ifaceProp.PropertyType, jsonName);

					// Pin the options-registered converter for leaf value types and enums.
					// These factory converters (Field/IndexName/RelationName, enums) are skipped for
					// properties on contracts rebuilt by this modifier when used at depth. We only pin
					// for concrete non-generic types to avoid interfering with collection/interface
					// resolution (which must fall through to STJ's built-in handling).
					var propType = ifaceProp.PropertyType;
					var underlying = Nullable.GetUnderlyingType(propType) ?? propType;
					if (!propType.IsGenericType && !propType.IsInterface && !propType.IsAbstract
						&& (underlying.IsEnum || ConverterBackedValueTypes.Contains(underlying)))
					{
						try
						{
							var conv = typeInfo.Options.GetConverter(propType);
							if (conv != null && conv.GetType().Namespace?.StartsWith("System", StringComparison.Ordinal) != true)
								jsonProp.CustomConverter = conv;
						}
						catch { /* If GetConverter throws, leave it unset — STJ will resolve normally */ }
					}

					var captured = ifaceProp;
					if (captured.CanRead)
						jsonProp.Get = obj => captured.GetValue(obj);
					if (captured.CanWrite)
						jsonProp.Set = (obj, value) => captured.SetValue(obj, value);

					rebuilt.Add(jsonProp);
				}
			}

			// If the interface contract declares no [DataMember] properties, decide between two cases:
			//  - The concrete class itself declares [DataMember] properties (e.g. response types such as
			//    GetResponse<T> whose members live on the class, not the interface). Leave the default
			//    contract intact so those class properties still (de)serialize.
			//  - Neither the interface nor the concrete class declares [DataMember] properties on their
			//    own members (e.g. GlobalAggregation, whose only inherited property Aggregations is not a
			//    [DataMember] and is emitted by the aggregation container, not inline). Fall through to
			//    rebuild the contract to an empty object so it serializes as {}.
			if (rebuilt.Count == 0)
			{
				if (ConcreteTypeDeclaresDataMember(type))
					return;
			}

			// These types are (de)serialized purely by their property contract. Many define a
			// public convenience constructor (e.g. AverageAggregation(string name, Field field))
			// that STJ would otherwise treat as the deserialization constructor and fail to bind.
			// Force construction through a parameterless constructor instead.
			var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
				binder: null, Type.EmptyTypes, modifiers: null);
			if (ctor != null)
				typeInfo.CreateObject = () => ctor.Invoke(null);

			// Rebuild the contract deterministically from the interface [DataMember] properties.
			typeInfo.Properties.Clear();
			foreach (var jsonProp in rebuilt)
				typeInfo.Properties.Add(jsonProp);
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
