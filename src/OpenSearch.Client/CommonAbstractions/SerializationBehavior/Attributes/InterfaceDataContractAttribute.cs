/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;

namespace OpenSearch.Client
{
	/// <summary>
	/// Marks an interface whose implementing types are (de)serialized purely against the interface's
	/// data contract — i.e. only interface members carrying a
	/// <see cref="System.Runtime.Serialization.DataMemberAttribute"/> are emitted, using the
	/// <see cref="System.Runtime.Serialization.DataMemberAttribute.Name"/> as the JSON name.
	/// <para>
	/// This is the OpenSearch.Client-native replacement for Utf8Json's
	/// <c>OpenSearch.Net.Utf8Json.InterfaceDataContractAttribute</c>, letting high-level domain types
	/// declare their contract without taking a dependency on the vendored Utf8Json engine. The
	/// System.Text.Json <see cref="InterfaceDataContractModifier"/> recognizes either attribute (matched
	/// by simple name), so domain interfaces may carry whichever one is in scope.
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface)]
	internal sealed class InterfaceDataContractAttribute : Attribute { }

	/// <summary>
	/// Helpers for detecting the interface-data-contract marker regardless of which
	/// <c>InterfaceDataContractAttribute</c> (the OpenSearch.Client-native one or the legacy
	/// <c>OpenSearch.Net.Utf8Json</c> one) a type carries. Matched by the attribute's simple name so the
	/// high-level machinery does not depend on the Utf8Json engine's attribute type.
	/// </summary>
	internal static class InterfaceDataContract
	{
		public static bool IsDefinedOn(Type type)
		{
			foreach (var attr in type.GetCustomAttributes(inherit: false))
			{
				if (attr.GetType().Name == nameof(InterfaceDataContractAttribute))
					return true;
			}
			return false;
		}
	}
}
