/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for <see cref="ISuggestDictionary{T}"/> that
	/// handles the suggest result dictionary in search responses.
	/// Replaces the Utf8Json-based <c>SuggestDictionaryFormatter</c>.
	/// </summary>
	internal sealed class SuggestDictionaryConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType) return false;

			var genericDef = typeToConvert.GetGenericTypeDefinition();
			return genericDef == typeof(ISuggestDictionary<>) || genericDef == typeof(SuggestDictionary<>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var docType = type.IsGenericType
					? type.GetGenericArguments()[0]
					: typeof(object);

				var converterType = typeof(SuggestDictionaryConverter<>).MakeGenericType(docType);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="ISuggestDictionary{TDocument}"/>.
	/// Reads a JSON object where each key is a suggest name and the value is an array of suggest results.
	/// </summary>
	internal sealed class SuggestDictionaryConverter<TDocument> : JsonConverter<ISuggestDictionary<TDocument>>
		where TDocument : class
	{
		public override ISuggestDictionary<TDocument> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return SuggestDictionary<TDocument>.Default;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return SuggestDictionary<TDocument>.Default;
			}

			var dict = JsonSerializer.Deserialize<Dictionary<string, ISuggest<TDocument>[]>>(ref reader, options);
			return new SuggestDictionary<TDocument>(dict ?? new Dictionary<string, ISuggest<TDocument>[]>());
		}

		public override void Write(Utf8JsonWriter writer, ISuggestDictionary<TDocument> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			foreach (var key in value.Keys)
			{
				writer.WritePropertyName(key);
				JsonSerializer.Serialize(writer, value[key], options);
			}
			writer.WriteEndObject();
		}
	}
}
