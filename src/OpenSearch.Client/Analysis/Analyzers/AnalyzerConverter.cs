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
	/// Polymorphic converter for <see cref="IAnalyzer"/> that dispatches on the <c>"type"</c>
	/// discriminator when reading, and serializes the concrete runtime type when writing.
	/// Replaces the Utf8Json <c>AnalyzerFormatter</c>.
	/// </summary>
	/// <remarks>
	/// When no known <c>"type"</c> is present the analyzer is treated as a <see cref="CustomAnalyzer"/>
	/// if a <c>"tokenizer"</c> is present, otherwise as a <see cref="LanguageAnalyzer"/> — matching the
	/// behavior of the original Utf8Json formatter.
	/// </remarks>
	internal sealed class AnalyzerConverter : JsonConverter<IAnalyzer>
	{
		private static readonly Dictionary<string, Type> TypeMapping = new(StringComparer.Ordinal)
		{
			["stop"] = typeof(StopAnalyzer),
			["standard"] = typeof(StandardAnalyzer),
			["snowball"] = typeof(SnowballAnalyzer),
			["pattern"] = typeof(PatternAnalyzer),
			["keyword"] = typeof(KeywordAnalyzer),
			["whitespace"] = typeof(WhitespaceAnalyzer),
			["simple"] = typeof(SimpleAnalyzer),
			["fingerprint"] = typeof(FingerprintAnalyzer),
			["kuromoji"] = typeof(KuromojiAnalyzer),
			["nori"] = typeof(NoriAnalyzer),
			["icu_analyzer"] = typeof(IcuAnalyzer),
			["custom"] = typeof(CustomAnalyzer),
		};

		public override IAnalyzer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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

			Type concreteType = null;
			if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
			{
				var type = typeProp.GetString();
				if (type != null)
					TypeMapping.TryGetValue(type, out concreteType);
			}

			if (concreteType == null)
			{
				// No known "type" discriminator: a "tokenizer" implies a custom analyzer, otherwise it is
				// a language analyzer (mirrors the Utf8Json AnalyzerFormatter default behavior).
				concreteType = root.TryGetProperty("tokenizer", out _)
					? typeof(CustomAnalyzer)
					: typeof(LanguageAnalyzer);
			}

			return (IAnalyzer)root.Deserialize(concreteType, options);
		}

		public override void Write(Utf8JsonWriter writer, IAnalyzer value, JsonSerializerOptions options)
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
