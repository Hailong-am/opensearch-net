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
	/// Polymorphic converter for <see cref="ITokenizer"/> that dispatches on the <c>"type"</c>
	/// discriminator when reading, and serializes the concrete runtime type when writing.
	/// Replaces the Utf8Json <c>TokenizerFormatter</c>.
	/// </summary>
	internal sealed class TokenizerConverter : JsonConverter<ITokenizer>
	{
		private static readonly Dictionary<string, Type> TypeMapping = new(StringComparer.Ordinal)
		{
			["char_group"] = typeof(CharGroupTokenizer),
			["edgengram"] = typeof(EdgeNGramTokenizer),
			["edge_ngram"] = typeof(EdgeNGramTokenizer),
			["ngram"] = typeof(NGramTokenizer),
			["path_hierarchy"] = typeof(PathHierarchyTokenizer),
			["pattern"] = typeof(PatternTokenizer),
			["standard"] = typeof(StandardTokenizer),
			["uax_url_email"] = typeof(UaxEmailUrlTokenizer),
			["whitespace"] = typeof(WhitespaceTokenizer),
			["kuromoji_tokenizer"] = typeof(KuromojiTokenizer),
			["icu_tokenizer"] = typeof(IcuTokenizer),
			["nori_tokenizer"] = typeof(NoriTokenizer),
		};

		public override ITokenizer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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

			return (ITokenizer)root.Deserialize(concreteType, options);
		}

		public override void Write(Utf8JsonWriter writer, ITokenizer value, JsonSerializerOptions options)
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
