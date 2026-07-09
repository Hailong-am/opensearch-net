/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes an <see cref="ICreateRepositoryRequest"/> as the repository object itself
	/// (<c>{ "type": "...", "settings": { ... } }</c>) rather than wrapping it in a
	/// <c>repository</c> property. Mirrors the historical (Utf8Json) <c>CreateRepositoryFormatter</c>.
	/// </summary>
	internal sealed class CreateRepositoryConverter : JsonConverter<ICreateRepositoryRequest>
	{
		public override ICreateRepositoryRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		public override void Write(Utf8JsonWriter writer, ICreateRepositoryRequest value, JsonSerializerOptions options)
		{
			if (value?.Repository == null)
			{
				writer.WriteStartObject();
				writer.WriteEndObject();
				return;
			}

			// Source-only repositories carry a dedicated interface-level converter (they have no
			// [InterfaceDataContract] and their settings are flattened with a delegate_type
			// discriminator). Route them through the interface so that converter is used.
			if (value.Repository is ISourceOnlyRepository sourceOnly)
			{
				JsonSerializer.Serialize(writer, sourceOnly, options);
				return;
			}

			// Other repositories serialize by their runtime type so the interface data contract
			// (type + settings) is emitted.
			JsonSerializer.Serialize(writer, value.Repository, value.Repository.GetType(), options);
		}
	}
}
