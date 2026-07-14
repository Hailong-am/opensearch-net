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
	/// Serializes the body of an update-index-settings request directly as the
	/// <see cref="IDynamicIndexSettings"/> content (the flattened settings object), rather than
	/// wrapping it in an <c>{ "indexSettings": { ... } }</c> envelope. Replaces the Utf8Json
	/// <c>UpdateIndexSettingsRequestFormatter</c>.
	/// </summary>
	internal sealed class UpdateIndexSettingsRequestConverter : JsonConverter<IUpdateIndexSettingsRequest>
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeof(IUpdateIndexSettingsRequest).IsAssignableFrom(typeToConvert);

		public override IUpdateIndexSettingsRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var settings = JsonSerializer.Deserialize<IDynamicIndexSettings>(ref reader, options);
			return new UpdateIndexSettingsRequest { IndexSettings = settings };
		}

		public override void Write(Utf8JsonWriter writer, IUpdateIndexSettingsRequest value, JsonSerializerOptions options)
		{
			if (value?.IndexSettings == null)
			{
				writer.WriteStartObject();
				writer.WriteEndObject();
				return;
			}

			JsonSerializer.Serialize(writer, value.IndexSettings, typeof(IDynamicIndexSettings), options);
		}
	}
}
