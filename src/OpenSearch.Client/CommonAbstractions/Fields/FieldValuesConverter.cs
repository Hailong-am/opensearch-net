/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Deserializes the <c>fields</c> object of a hit / get response into a <see cref="FieldValues"/>
	/// dictionary. Unlike the generic <c>IsADictionaryConverterFactory</c>, this converter injects the
	/// connection settings <see cref="Inferrer"/> so that <see cref="FieldValues.ValueOf{T,TValue}"/> and
	/// <see cref="FieldValues.ValuesOf{TValue}"/> can resolve expression/field lookups (the generic factory
	/// only matches a parameterless or single-<c>IDictionary</c> constructor and therefore loses the
	/// inferrer, leaving every lookup empty). Restores the pre-STJ <c>FieldValuesFormatter</c> behavior.
	/// </summary>
	internal sealed class FieldValuesConverter : JsonConverter<FieldValues>
	{
		private readonly IConnectionSettingsValues _settings;

		public FieldValuesConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override FieldValues Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return FieldValues.Empty;
			}

			var container = new Dictionary<string, LazyDocument>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var key = reader.GetString();
				reader.Read();
				var value = JsonSerializer.Deserialize<LazyDocument>(ref reader, options);
				container[key] = value;
			}

			return new FieldValues(_settings.Inferrer, container);
		}

		public override void Write(Utf8JsonWriter writer, FieldValues value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			foreach (var kvp in value)
			{
				writer.WritePropertyName(kvp.Key);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}
