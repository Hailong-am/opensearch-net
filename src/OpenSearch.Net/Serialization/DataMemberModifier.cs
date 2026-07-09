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
