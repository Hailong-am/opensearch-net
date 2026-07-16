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
	/// Polymorphic converter for <see cref="INormalizer"/>. OpenSearch only supports custom
	/// normalizers, so every normalizer is read as a <see cref="CustomNormalizer"/> and written
	/// via its concrete runtime type. Replaces the Utf8Json <c>NormalizerFormatter</c>.
	/// </summary>
	internal sealed class NormalizerConverter : JsonConverter<INormalizer>
	{
		public override INormalizer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			return JsonSerializer.Deserialize<CustomNormalizer>(ref reader, options);
		}

		public override void Write(Utf8JsonWriter writer, INormalizer value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
