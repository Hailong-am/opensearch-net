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
	/// Polymorphic converter for <see cref="ICharFilter"/> that dispatches on the <c>"type"</c>
	/// discriminator when reading, and serializes the concrete runtime type when writing.
	/// Replaces the Utf8Json <c>CharFilterFormatter</c>.
	/// </summary>
	internal sealed class CharFilterConverter : JsonConverter<ICharFilter>
	{
		private static readonly Dictionary<string, Type> TypeMapping = new(StringComparer.Ordinal)
		{
			["html_strip"] = typeof(HtmlStripCharFilter),
			["mapping"] = typeof(MappingCharFilter),
			["pattern_replace"] = typeof(PatternReplaceCharFilter),
			["kuromoji_iteration_mark"] = typeof(KuromojiIterationMarkCharFilter),
			["icu_normalizer"] = typeof(IcuNormalizationCharFilter),
		};

		public override ICharFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			if (!root.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
				return null;

			var type = typeProp.GetString();
			if (type == null || !TypeMapping.TryGetValue(type, out var concreteType))
				return null;

			return (ICharFilter)root.Deserialize(concreteType, options);
		}

		public override void Write(Utf8JsonWriter writer, ICharFilter value, JsonSerializerOptions options)
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
