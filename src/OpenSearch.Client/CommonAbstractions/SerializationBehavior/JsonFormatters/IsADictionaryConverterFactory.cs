/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	internal sealed class IsADictionaryConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IsADictionaryConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IIsADictionary).IsAssignableFrom(typeToConvert) && GetDictionaryArguments(typeToConvert) != null;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetDictionaryArguments(typeToConvert);
			var converterType = typeof(IsADictionaryConverter<,,>).MakeGenericType(args[0], args[1], typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}

		private static Type[] GetDictionaryArguments(Type type)
		{
			var iface = type.GetInterfaces()
				.FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IIsADictionary<,>));

			if (iface != null)
				return iface.GetGenericArguments();

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IIsADictionary<,>))
				return type.GetGenericArguments();

			return null;
		}
	}

	internal sealed class IsADictionaryConverter<TKey, TValue, TDictionary> : JsonConverter<TDictionary>
		where TDictionary : class, IIsADictionary<TKey, TValue>
	{
		private readonly IConnectionSettingsValues _settings;

		public IsADictionaryConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override TDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			var dict = new Dictionary<TKey, TValue>();

			reader.Read();
			while (reader.TokenType != JsonTokenType.EndObject)
			{
				var key = ReadKey(ref reader, options);
				reader.Read();
				var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
				dict[key] = value;
				reader.Read();
			}

			return CreateInstance(dict);
		}

		public override void Write(Utf8JsonWriter writer, TDictionary value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			foreach (var kvp in (IEnumerable<KeyValuePair<TKey, TValue>>)value)
			{
				if (kvp.Value == null)
					continue;

				var keyString = ResolveKey(kvp.Key);
				if (keyString == null)
					continue;

				writer.WritePropertyName(keyString);
				// Serialize by the value's runtime type so polymorphic values (e.g. ITokenFilter,
				// IAnalyzer, IProperty) emit their full concrete contract rather than the (often empty)
				// declared-interface contract. STJ resolves the runtime type's converter/contract.
				if (kvp.Value is null)
					writer.WriteNullValue();
				else
					JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
			}

			writer.WriteEndObject();
		}

		private string ResolveKey(TKey key)
		{
			if (key == null) return string.Empty;

			if (key is PropertyName propertyName)
			{
				if (propertyName.Property != null
					&& _settings.PropertyMappings.TryGetValue(propertyName.Property, out var mapping)
					&& mapping.Ignore)
					return null;

				return _settings.Inferrer.PropertyName(propertyName) ?? string.Empty;
			}

			if (key is IndexName indexName)
				return _settings.Inferrer.IndexName(indexName) ?? string.Empty;

			if (key is Field field)
				return _settings.Inferrer.Field(field) ?? string.Empty;

			if (key is RelationName relationName)
				return _settings.Inferrer.RelationName(relationName) ?? string.Empty;

			if (key is string s)
				return s;

			return key.ToString();
		}

		private TKey ReadKey(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var keyString = reader.GetString();

			if (typeof(TKey) == typeof(string))
				return (TKey)(object)keyString;

			if (typeof(TKey) == typeof(PropertyName))
				return (TKey)(object)(PropertyName)keyString;

			if (typeof(TKey) == typeof(IndexName))
				return (TKey)(object)(IndexName)keyString;

			if (typeof(TKey) == typeof(Field))
				return (TKey)(object)(Field)keyString;

			if (typeof(TKey) == typeof(RelationName))
				return (TKey)(object)(RelationName)keyString;

			return (TKey)Convert.ChangeType(keyString, typeof(TKey));
		}

		private static TDictionary CreateInstance(Dictionary<TKey, TValue> dict)
		{
			var type = typeof(TDictionary);
			if (type.IsInterface || type.IsAbstract)
			{
				var readAsAttribute = type.GetCustomAttribute<ReadAsAttribute>();
				if (readAsAttribute != null)
				{
					type = readAsAttribute.Type.IsGenericType && !readAsAttribute.Type.IsConstructedGenericType
						? readAsAttribute.Type.MakeGenericType(typeof(TKey), typeof(TValue))
						: readAsAttribute.Type;
				}
				else
				{
					// Try to find a concrete implementation in the same assembly
					var candidateType = type.Assembly.GetTypes()
						.FirstOrDefault(t => !t.IsInterface && !t.IsAbstract && type.IsAssignableFrom(t));
					if (candidateType != null)
						type = candidateType;
					else
						throw new JsonException($"Unable to deserialize interface {typeof(TDictionary).FullName} — no [ReadAs] attribute found.");
				}
			}

			var genericDictionaryInterface = typeof(IDictionary<TKey, TValue>);

			var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Select(c => new { Constructor = c, Parameters = c.GetParameters() })
				.Where(x => x.Parameters.Length == 0
					|| (x.Parameters.Length == 1 && genericDictionaryInterface.IsAssignableFrom(x.Parameters[0].ParameterType)))
				.OrderByDescending(x => x.Parameters.Length)
				.FirstOrDefault();

			if (ctor == null)
				throw new JsonException($"Cannot create an instance of {type.FullName} — no suitable constructor found.");

			TDictionary instance;
			if (ctor.Parameters.Length == 1)
			{
				instance = (TDictionary)ctor.Constructor.Invoke(new object[] { dict });
			}
			else
			{
				instance = (TDictionary)ctor.Constructor.Invoke(Array.Empty<object>());
				foreach (var kvp in dict)
					((IDictionary<TKey, TValue>)instance).Add(kvp.Key, kvp.Value);
			}

			return instance;
		}
	}
}
