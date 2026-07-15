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
	/// Polymorphic converter for <see cref="ITokenFilter"/> that dispatches on the <c>"type"</c>
	/// discriminator when reading, and serializes the concrete runtime type when writing.
	/// Replaces the Utf8Json <c>TokenFilterFormatter</c>.
	/// </summary>
	internal sealed class TokenFilterConverter : JsonConverter<ITokenFilter>
	{
		private static readonly Dictionary<string, Type> TypeMapping = new(StringComparer.Ordinal)
		{
			["asciifolding"] = typeof(AsciiFoldingTokenFilter),
			["common_grams"] = typeof(CommonGramsTokenFilter),
			["delimited_payload"] = typeof(DelimitedPayloadTokenFilter),
			["delimited_payload_filter"] = typeof(DelimitedPayloadTokenFilter),
			["dictionary_decompounder"] = typeof(DictionaryDecompounderTokenFilter),
			["edge_ngram"] = typeof(EdgeNGramTokenFilter),
			["elision"] = typeof(ElisionTokenFilter),
			["hunspell"] = typeof(HunspellTokenFilter),
			["hyphenation_decompounder"] = typeof(HyphenationDecompounderTokenFilter),
			["keep_types"] = typeof(KeepTypesTokenFilter),
			["keep"] = typeof(KeepWordsTokenFilter),
			["keyword_marker"] = typeof(KeywordMarkerTokenFilter),
			["kstem"] = typeof(KStemTokenFilter),
			["length"] = typeof(LengthTokenFilter),
			["limit"] = typeof(LimitTokenCountTokenFilter),
			["lowercase"] = typeof(LowercaseTokenFilter),
			["ngram"] = typeof(NGramTokenFilter),
			["pattern_capture"] = typeof(PatternCaptureTokenFilter),
			["pattern_replace"] = typeof(PatternReplaceTokenFilter),
			["porter_stem"] = typeof(PorterStemTokenFilter),
			["phonetic"] = typeof(PhoneticTokenFilter),
			["reverse"] = typeof(ReverseTokenFilter),
			["shingle"] = typeof(ShingleTokenFilter),
			["snowball"] = typeof(SnowballTokenFilter),
			["stemmer"] = typeof(StemmerTokenFilter),
			["stemmer_override"] = typeof(StemmerOverrideTokenFilter),
			["stop"] = typeof(StopTokenFilter),
			["synonym"] = typeof(SynonymTokenFilter),
			["synonym_graph"] = typeof(SynonymGraphTokenFilter),
			["trim"] = typeof(TrimTokenFilter),
			["truncate"] = typeof(TruncateTokenFilter),
			["unique"] = typeof(UniqueTokenFilter),
			["uppercase"] = typeof(UppercaseTokenFilter),
			["word_delimiter"] = typeof(WordDelimiterTokenFilter),
			["word_delimiter_graph"] = typeof(WordDelimiterGraphTokenFilter),
			["fingerprint"] = typeof(FingerprintTokenFilter),
			["nori_part_of_speech"] = typeof(NoriPartOfSpeechTokenFilter),
			["kuromoji_readingform"] = typeof(KuromojiReadingFormTokenFilter),
			["kuromoji_part_of_speech"] = typeof(KuromojiPartOfSpeechTokenFilter),
			["kuromoji_stemmer"] = typeof(KuromojiStemmerTokenFilter),
			["icu_collation"] = typeof(IcuCollationTokenFilter),
			["icu_folding"] = typeof(IcuFoldingTokenFilter),
			["icu_normalizer"] = typeof(IcuNormalizationTokenFilter),
			["icu_transform"] = typeof(IcuTransformTokenFilter),
			["condition"] = typeof(ConditionTokenFilter),
			["multiplexer"] = typeof(MultiplexerTokenFilter),
			["predicate_token_filter"] = typeof(PredicateTokenFilter),
		};

		public override ITokenFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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

			return (ITokenFilter)root.Deserialize(concreteType, options);
		}

		public override void Write(Utf8JsonWriter writer, ITokenFilter value, JsonSerializerOptions options)
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
