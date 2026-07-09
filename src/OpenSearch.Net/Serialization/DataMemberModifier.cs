/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

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

			foreach (var property in typeInfo.Properties)
			{
				var dataMemberAttr = property.AttributeProvider?
					.GetCustomAttributes(typeof(DataMemberAttribute), true)
					.OfType<DataMemberAttribute>()
					.FirstOrDefault();

				if (dataMemberAttr?.Name != null)
					property.Name = dataMemberAttr.Name;

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
	}
}
