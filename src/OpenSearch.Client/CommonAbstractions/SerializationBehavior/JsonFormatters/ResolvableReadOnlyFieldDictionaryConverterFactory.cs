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
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Deserializes response properties typed <see cref="IReadOnlyDictionary{TKey,TValue}"/> with an
	/// inferrer-resolved key (<see cref="Field"/>, <see cref="IndexName"/>, <see cref="RelationName"/>) into
	/// a settings-aware <see cref="ResolvableDictionaryProxy{TKey,TValue}"/>. Examples:
	/// <c>TermVectorsResult.TermVectors</c>, <c>TypeFieldMappings.Mappings</c> (Field);
	/// <c>ClusterHealthResponse.Indices</c> (IndexName).
	/// <para>
	/// The proxy stores keys as their inferrer-resolved wire strings and resolves lookups the same way, so an
	/// expression-path <see cref="Field"/> or an inferred <see cref="IndexName"/> matches the deserialized
	/// string-based key. Restores the pre-STJ <c>ResolvableReadOnlyDictionaryFormatter&lt;TKey, T&gt;</c>
	/// behavior; without it these dictionaries deserialize into plain typed-key maps whose keys never compare
	/// equal to an inferred key, so lookups miss (<see cref="KeyNotFoundException"/> or an empty result).
	/// </para>
	/// </summary>
	internal sealed class ResolvableReadOnlyFieldDictionaryConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public ResolvableReadOnlyFieldDictionaryConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType) return false;
			if (typeToConvert.GetGenericTypeDefinition() != typeof(IReadOnlyDictionary<,>)) return false;
			var keyType = typeToConvert.GetGenericArguments()[0];
			return keyType == typeof(Field) || keyType == typeof(IndexName) || keyType == typeof(RelationName);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = typeToConvert.GetGenericArguments();
			var converterType = typeof(ResolvableReadOnlyDictionaryConverter<,>).MakeGenericType(args[0], args[1]);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	internal sealed class ResolvableReadOnlyDictionaryConverter<TKey, TValue> : JsonConverter<IReadOnlyDictionary<TKey, TValue>>
		where TKey : IUrlParameter
	{
		private readonly IConnectionSettingsValues _settings;

		public ResolvableReadOnlyDictionaryConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IReadOnlyDictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return new ResolvableDictionaryProxy<TKey, TValue>(_settings, new Dictionary<TKey, TValue>());
			}

			var dict = new Dictionary<TKey, TValue>();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var key = ConvertKey(reader.GetString());
				reader.Read();
				dict[key] = JsonSerializer.Deserialize<TValue>(ref reader, options);
			}

			return new ResolvableDictionaryProxy<TKey, TValue>(_settings, dict);
		}

		public override void Write(Utf8JsonWriter writer, IReadOnlyDictionary<TKey, TValue> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			foreach (var kvp in value)
			{
				var keyString = (kvp.Key as IUrlParameter)?.GetString(_settings);
				if (keyString == null) continue;
				writer.WritePropertyName(keyString);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}

		private static TKey ConvertKey(string key)
		{
			if (typeof(TKey) == typeof(Field))
				return (TKey)(object)(Field)key;
			if (typeof(TKey) == typeof(IndexName))
				return (TKey)(object)(IndexName)key;
			if (typeof(TKey) == typeof(RelationName))
				return (TKey)(object)(RelationName)key;
			return (TKey)(object)key;
		}
	}
}
