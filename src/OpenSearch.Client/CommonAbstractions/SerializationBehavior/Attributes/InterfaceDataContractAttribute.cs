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
	/// Marks an interface as defining the JSON serialization contract for concrete types that implement it.
	/// When applied, the <c>InterfaceDataContractModifier</c> rebuilds the JSON contract for those types
	/// using only the properties declared on the interface, honoring <see cref="System.Runtime.Serialization.DataMemberAttribute"/>
	/// for JSON property names and <see cref="System.Runtime.Serialization.IgnoreDataMemberAttribute"/> for exclusions.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
	public class InterfaceDataContractAttribute : Attribute { }
}
