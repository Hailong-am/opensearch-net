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
	/// Marker attribute indicating the preferred constructor for deserialization.
	/// Retained for source compatibility; STJ uses <see cref="System.Text.Json.Serialization.JsonConstructorAttribute"/>.
	/// </summary>
	[AttributeUsage(AttributeTargets.Constructor)]
	internal class SerializationConstructorAttribute : Attribute { }
}
