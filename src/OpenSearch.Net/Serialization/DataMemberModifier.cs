/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="DefaultJsonTypeInfoResolver"/> modifier that reads
	/// <see cref="DataMemberAttribute.Name"/> and applies it as the JSON property name,
	/// and ensures properties with non-public (internal) setters can be deserialized.
	/// This allows STJ to honor existing [DataMember(Name = "...")] attributes
	/// without requiring [JsonPropertyName] on every property.
	/// </summary>
	internal static class DataMemberPropertyNameModifier
	{
		public static void Modify(JsonTypeInfo typeInfo)
		{
			if (typeInfo.Kind != JsonTypeInfoKind.Object)
				return;

			for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
			{
				var property = typeInfo.Properties[i];

				var hasIgnore = property.AttributeProvider?
					.GetCustomAttributes(typeof(IgnoreDataMemberAttribute), true)
					.Any() == true;

				if (hasIgnore)
				{
					typeInfo.Properties.RemoveAt(i);
					continue;
				}

				var dataMemberAttr = property.AttributeProvider?
					.GetCustomAttributes(typeof(DataMemberAttribute), true)
					.OfType<DataMemberAttribute>()
					.FirstOrDefault();

				if (dataMemberAttr?.Name != null)
				{
					property.Name = dataMemberAttr.Name;
				}
				else if (property.AttributeProvider is PropertyInfo propInfo)
				{
					var interfaceName = FindInterfaceDataMemberName(typeInfo.Type, propInfo);
					if (interfaceName != null)
						property.Name = interfaceName;
				}

				// Ensure properties with non-public setters (e.g. internal set) can be deserialized.
				// STJ by default only writes to public setters; this enables internal/protected setters.
				if (property.Set == null && property.AttributeProvider is MemberInfo memberInfo)
				{
					var propertyInfo = memberInfo as PropertyInfo
						?? typeInfo.Type.GetProperty(memberInfo.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

					if (propertyInfo?.GetSetMethod(true) != null)
					{
						property.Set = (obj, value) => propertyInfo.SetValue(obj, value);
					}
				}
			}

			AddNonPublicDataMembers(typeInfo);
		}

		// STJ's DefaultJsonTypeInfoResolver surfaces only public properties, so a wholly non-public
		// (e.g. internal) property carrying [DataMember] is dropped entirely — the pre-STJ Utf8Json resolver
		// serialized these via AllowPrivate. Re-add any non-public instance property that carries
		// [DataMember] (and is not [IgnoreDataMember]) which STJ omitted, so members such as
		// ResponseBase.Error/StatusCode, GetTaskResponse.Response and CatNodesRecord's internal aliases
		// (de)serialize. Walk the base-type chain with DeclaredOnly so inherited members on derived responses
		// are also picked up.
		private static void AddNonPublicDataMembers(JsonTypeInfo typeInfo)
		{
			var existingMembers = new System.Collections.Generic.HashSet<string>();
			// Map each occupied JSON wire name to the property currently holding it, so a non-public
			// [DataMember] can decide whether to yield (a real settable / [DataMember] property wins) or
			// take over (a read-only computed alias property, which cannot be a deserialization target).
			var existingByJsonName = new System.Collections.Generic.Dictionary<string, JsonPropertyInfo>(StringComparer.Ordinal);
			foreach (var p in typeInfo.Properties)
			{
				if (p.AttributeProvider is MemberInfo mi)
					existingMembers.Add(mi.Name);
				existingByJsonName[p.Name] = p;
			}

			for (var t = typeInfo.Type; t != null && t != typeof(object); t = t.BaseType)
			{
				foreach (var pi in t.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
				{
					if (!existingMembers.Add(pi.Name))
						continue;

					var dm = pi.GetCustomAttribute<DataMemberAttribute>();
					if (dm == null || pi.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
						continue;

					var jsonName = dm.Name ?? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(pi.Name);

					if (existingByJsonName.TryGetValue(jsonName, out var occupant))
					{
						// A read-only computed alias property (no setter, no [DataMember] of its own — e.g.
						// CatNodesRecord.Build => _b ?? _build) cannot deserialize; the internal [DataMember]
						// backing member is the real wire target, so replace the occupant. Otherwise (a
						// settable or [DataMember]-bearing property such as ClusterHealthResponse.Status) the
						// existing property wins and we skip to avoid an STJ property-name collision.
						var occupantIsReplaceableAlias = occupant.Set == null
							&& occupant.AttributeProvider is MemberInfo om
							&& om.GetCustomAttribute<DataMemberAttribute>() == null;

						if (!occupantIsReplaceableAlias)
							continue;

						typeInfo.Properties.Remove(occupant);
						existingByJsonName.Remove(jsonName);
					}

					var jsonProperty = typeInfo.CreateJsonPropertyInfo(pi.PropertyType, jsonName);

					var getter = pi.GetGetMethod(true);
					if (getter != null)
						jsonProperty.Get = obj => getter.Invoke(obj, null);

					var setter = pi.GetSetMethod(true);
					if (setter != null)
						jsonProperty.Set = (obj, value) => setter.Invoke(obj, new[] { value });

					typeInfo.Properties.Add(jsonProperty);
					existingByJsonName[jsonName] = jsonProperty;
				}
			}
		}

		private static string FindInterfaceDataMemberName(Type type, PropertyInfo property)
		{
			var getter = property.GetGetMethod(true);
			if (getter == null) return null;

			foreach (var iface in type.GetInterfaces())
			{
				if (iface.Namespace?.StartsWith("System") == true)
					continue;

				try
				{
					var map = type.GetInterfaceMap(iface);
					for (var i = 0; i < map.TargetMethods.Length; i++)
					{
						if (map.TargetMethods[i] == getter)
						{
							var ifaceMethod = map.InterfaceMethods[i];
							var ifaceProp = iface.GetProperties()
								.FirstOrDefault(p => p.GetGetMethod() == ifaceMethod);

							if (ifaceProp == null) break;

							var attr = ifaceProp.GetCustomAttribute<DataMemberAttribute>();
							if (attr?.Name != null)
								return attr.Name;

							break;
						}
					}
				}
				catch (ArgumentException) { }
			}

			return null;
		}
	}
}
