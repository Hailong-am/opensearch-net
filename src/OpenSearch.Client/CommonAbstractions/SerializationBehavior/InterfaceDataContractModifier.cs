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

			var type = typeInfo.Type;

			if (type.IsInterface || type.IsAbstract)
				return;

			// Types serialized by a dedicated converter (dictionaries, scripts, etc.) never reach here.
			if (typeof(IIsADictionary).IsAssignableFrom(type))
				return;

			var contractInterfaces = GetContractInterfaces(type);
			if (contractInterfaces.Count == 0)
				return;

			// Rebuild the contract deterministically from the interface [DataMember] properties.
			typeInfo.Properties.Clear();

			var seen = new HashSet<string>(StringComparer.Ordinal);

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

					var captured = ifaceProp;
					if (captured.CanRead)
						jsonProp.Get = obj => captured.GetValue(obj);
					if (captured.CanWrite)
						jsonProp.Set = (obj, value) => captured.SetValue(obj, value);

					typeInfo.Properties.Add(jsonProp);
				}
			}
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
