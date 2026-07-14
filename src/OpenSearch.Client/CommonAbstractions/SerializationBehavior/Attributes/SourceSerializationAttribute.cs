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
	/// Marks a contract property whose value must be (de)serialized by the configured
	/// <see cref="IConnectionSettingsValues.SourceSerializer"/> rather than the high-level
	/// serializer. Used for embedded user-document payloads (e.g. the <c>doc</c> of a
	/// more_like_this <c>like</c> item, or a percolate document) which are typed as
	/// <see cref="object"/> and would otherwise be handled by the high-level serializer.
	/// <para>
	/// This is the System.Text.Json equivalent of the Utf8Json
	/// <c>[JsonFormatter(typeof(SourceFormatter&lt;object&gt;))]</c> annotation.
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	internal sealed class SourceSerializationAttribute : Attribute { }
}
