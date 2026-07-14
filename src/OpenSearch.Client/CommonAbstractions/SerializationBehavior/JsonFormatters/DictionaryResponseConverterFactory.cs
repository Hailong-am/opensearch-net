/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Deserializes responses derived from <see cref="DictionaryResponseBase{TKey,TValue}"/> (e.g.
	/// <c>GetIndexResponse</c>, <c>GetAliasResponse</c>, <c>GetMappingResponse</c>, <c>GetPipelineResponse</c>).
	/// The response body is a JSON object whose members are the dictionary entries, plus the optional
	/// server <c>error</c>/<c>status</c> fields. This replaces the Utf8Json
	/// <c>ResolvableDictionaryResponseFormatter</c> / <c>DictionaryResponseFormatter</c> that were applied via
	/// a <c>[JsonFormatter]</c> attribute in the pre-migration client.
	/// </summary>
	/// <remarks>
	/// When the key type implements <see cref="IUrlParameter"/> the backing dictionary is wrapped in a
	/// <see cref="ResolvableDictionaryProxy{TKey,TValue}"/> so that inferred keys resolve correctly, matching
	/// the behavior of the old <c>ResolvableDictionaryResponseFormatter</c>.
	/// </remarks>
	internal sealed class DictionaryResponseConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public DictionaryResponseConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			!typeToConvert.IsAbstract && GetDictionaryResponseArguments(typeToConvert) != null;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetDictionaryResponseArguments(typeToConvert);
			var converterType = typeof(DictionaryResponseConverter<,,>).MakeGenericType(typeToConvert, args[0], args[1]);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}

		/// <summary>
		/// Returns the <c>[TKey, TValue]</c> type arguments if <paramref name="type"/> derives from
		/// <see cref="DictionaryResponseBase{TKey,TValue}"/>, otherwise <c>null</c>.
		/// </summary>
		private static Type[] GetDictionaryResponseArguments(Type type)
		{
			var current = type.BaseType;
			while (current != null && current != typeof(object))
			{
				if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(DictionaryResponseBase<,>))
					return current.GetGenericArguments();
				current = current.BaseType;
			}
			return null;
		}
	}

	internal sealed class DictionaryResponseConverter<TResponse, TKey, TValue> : JsonConverter<TResponse>
		where TResponse : ResponseBase, IDictionaryResponse<TKey, TValue>, new()
	{
		private readonly IConnectionSettingsValues _settings;

		public DictionaryResponseConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override TResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			var response = new TResponse();
			var dictionary = new Dictionary<TKey, TValue>();

			reader.Read();
			while (reader.TokenType != JsonTokenType.EndObject)
			{
				var property = reader.GetString();
				reader.Read();

				if (property == "error")
				{
					if (reader.TokenType == JsonTokenType.String)
						response.Error = new Error { Reason = reader.GetString() };
					else
						response.Error = JsonSerializer.Deserialize<Error>(ref reader, options);
				}
				else if (property == "status")
				{
					if (reader.TokenType == JsonTokenType.Number)
						response.StatusCode = reader.GetInt32();
					else
						reader.Skip();
				}
				else
				{
					var key = ConvertKey(property);
					var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
					dictionary[key] = value;
				}

				reader.Read();
			}

			response.BackingDictionary = CreateBackingDictionary(dictionary);
			return response;
		}

		public override void Write(Utf8JsonWriter writer, TResponse value, JsonSerializerOptions options) =>
			throw new NotSupportedException($"{typeof(TResponse).Name} is a response type and is not serialized.");

		private static TKey ConvertKey(string keyString)
		{
			if (typeof(TKey) == typeof(string))
				return (TKey)(object)keyString;
			if (typeof(TKey) == typeof(IndexName))
				return (TKey)(object)(IndexName)keyString;
			if (typeof(TKey) == typeof(Name))
				return (TKey)(object)(Name)keyString;
			return (TKey)(object)keyString;
		}

		private IReadOnlyDictionary<TKey, TValue> CreateBackingDictionary(Dictionary<TKey, TValue> dictionary)
		{
			// IUrlParameter keys (e.g. IndexName) get the resolvable proxy so inferred keys resolve
			// to their wire form, matching the old ResolvableDictionaryResponseFormatter behavior.
			if (typeof(IUrlParameter).IsAssignableFrom(typeof(TKey)))
			{
				var proxyType = typeof(ResolvableDictionaryProxy<,>).MakeGenericType(typeof(TKey), typeof(TValue));
				var ctor = proxyType.GetConstructor(
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
					binder: null,
					new[] { typeof(IConnectionConfigurationValues), typeof(IReadOnlyDictionary<TKey, TValue>) },
					modifiers: null);

				if (ctor != null)
					return (IReadOnlyDictionary<TKey, TValue>)ctor.Invoke(new object[] { _settings, dictionary });
			}

			return dictionary;
		}
	}
}
