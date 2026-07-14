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
	internal sealed class PropertyNameConverter : JsonConverter<PropertyName>
	{
		private readonly IConnectionSettingsValues _settings;

		public PropertyNameConverter() { }

		public PropertyNameConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override PropertyName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var name = reader.GetString();
			return name == null ? null : new PropertyName(name);
		}

		public override void Write(Utf8JsonWriter writer, PropertyName value, JsonSerializerOptions options)
		{
			if (value == null || value.IsConditionless())
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStringValue(Resolve(value));
		}

		public override PropertyName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var name = reader.GetString();
			return name == null ? null : new PropertyName(name);
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, PropertyName value, JsonSerializerOptions options)
		{
			if (value == null || value.IsConditionless())
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			writer.WritePropertyName(Resolve(value) ?? string.Empty);
		}

		// Resolve the wire name via the Inferrer (honors expressions, suffixes, property mappings and
		// the DefaultFieldNameInferrer). Falls back to the literal name when no settings are available.
		private string Resolve(PropertyName value) =>
			_settings != null ? _settings.Inferrer.PropertyName(value) : value.Name;
	}
}
