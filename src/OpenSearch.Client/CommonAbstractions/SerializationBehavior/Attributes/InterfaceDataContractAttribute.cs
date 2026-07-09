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
	/// Marker attribute indicating that an interface supports polymorphic deserialization
	/// via <see cref="ReadAsAttribute"/>. Used in conjunction with <see cref="ReadAsConverterFactory"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface)]
	internal class InterfaceDataContractAttribute : Attribute { }
}
