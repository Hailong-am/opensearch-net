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
	/// Handles deserialization of concrete subclasses of <see cref="ResolvableDictionaryProxy{TKey,TValue}"/>
	/// (e.g. <c>FieldCapabilitiesFields</c>, <c>IndicesStatsDictionary</c>). These types require
	/// <see cref="IConnectionConfigurationValues"/> in their constructors, so STJ cannot instantiate
	/// them directly. This factory reads the JSON object into a <c>Dictionary&lt;TKey, TValue&gt;</c>
	/// and invokes the appropriate constructor.
	/// </summary>
	internal sealed class ResolvableDictionaryProxyConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public ResolvableDictionaryProxyConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			!typeToConvert.IsAbstract && !typeToConvert.IsInterface && GetProxyArguments(typeToConvert) != null;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetProxyArguments(typeToConvert);
			var converterType = typeof(ResolvableDictionaryProxyConverter<,,>).MakeGenericType(typeToConvert, args[0], args[1]);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}

		/// <summary>
		/// Returns the [TKey, TValue] type arguments if <paramref name="type"/> derives from
		/// <see cref="ResolvableDictionaryProxy{TKey,TValue}"/>, otherwise null.
		/// </summary>
		private static Type[] GetProxyArguments(Type type)
		{
			var current = type.BaseType;
			while (current != null && current != typeof(object))
			{
				if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(ResolvableDictionaryProxy<,>))
					return current.GetGenericArguments();
				current = current.BaseType;
			}
			return null;
		}
	}

	internal sealed class ResolvableDictionaryProxyConverter<TProxy, TKey, TValue> : JsonConverter<TProxy>
		where TProxy : ResolvableDictionaryProxy<TKey, TValue>
		where TKey : IUrlParameter
	{
		private readonly IConnectionSettingsValues _settings;
		private readonly ConstructorInfo _ctor;

		public ResolvableDictionaryProxyConverter(IConnectionSettingsValues settings)
		{
			_settings = settings;

			// Find the constructor that takes (IConnectionConfigurationValues, IReadOnlyDictionary<TKey, TValue>)
			_ctor = typeof(TProxy).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
				binder: null,
				new[] { typeof(IConnectionConfigurationValues), typeof(IReadOnlyDictionary<TKey, TValue>) },
				modifiers: null);

			if (_ctor == null)
				throw new InvalidOperationException(
					$"Type {typeof(TProxy).FullName} does not have a constructor accepting " +
					$"(IConnectionConfigurationValues, IReadOnlyDictionary<{typeof(TKey).Name}, {typeof(TValue).Name}>).");
		}

		public override TProxy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			var dict = new Dictionary<TKey, TValue>();

			reader.Read();
			while (reader.TokenType != JsonTokenType.EndObject)
			{
				var key = ReadKey(reader.GetString());
				reader.Read();
				var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
				dict[key] = value;
				reader.Read();
			}

			return (TProxy)_ctor.Invoke(new object[] { _settings, (IReadOnlyDictionary<TKey, TValue>)dict });
		}

		public override void Write(Utf8JsonWriter writer, TProxy value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			foreach (var kvp in (IEnumerable<KeyValuePair<TKey, TValue>>)value)
			{
				var keyString = kvp.Key?.GetString(_settings);
				if (keyString == null) continue;
				writer.WritePropertyName(keyString);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}

		private static TKey ReadKey(string keyString)
		{
			if (typeof(TKey) == typeof(string))
				return (TKey)(object)keyString;
			if (typeof(TKey) == typeof(IndexName))
				return (TKey)(object)(IndexName)keyString;
			if (typeof(TKey) == typeof(Field))
				return (TKey)(object)(Field)keyString;
			if (typeof(TKey) == typeof(RelationName))
				return (TKey)(object)(RelationName)keyString;
			return (TKey)(object)keyString;
		}
	}
}
