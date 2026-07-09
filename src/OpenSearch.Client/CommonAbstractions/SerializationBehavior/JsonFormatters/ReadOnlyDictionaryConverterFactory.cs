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
	/// <summary>
	/// Deserializes dictionary types that System.Text.Json cannot construct on its own — those without
	/// a public parameterless constructor, such as <see cref="System.Collections.ObjectModel.ReadOnlyDictionary{TKey,TValue}"/>
	/// and custom <c>IReadOnlyDictionary&lt;,&gt;</c>/<c>IDictionary&lt;,&gt;</c> implementations that only
	/// expose a single <c>IDictionary&lt;,&gt;</c>-accepting constructor. STJ's built-in dictionary
	/// converter handles serialization fine but throws on deserialization for these; the legacy JsonNet
	/// source serializer supported them, so this factory restores that behavior on the source path.
	/// </summary>
	internal sealed class ReadOnlyDictionaryConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsGenericType || typeToConvert.IsInterface || typeToConvert.IsAbstract)
				return false;

			var args = GetDictionaryArguments(typeToConvert);
			if (args == null)
				return false;

			// Only claim types STJ cannot construct itself (no public parameterless ctor) but which
			// DO expose a single-parameter constructor accepting an IDictionary<,> / IReadOnlyDictionary<,>.
			if (typeToConvert.GetConstructor(Type.EmptyTypes) != null)
				return false;

			return FindDictionaryConstructor(typeToConvert, args) != null;
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetDictionaryArguments(typeToConvert);
			var converterType = typeof(ReadOnlyDictionaryConverter<,,>).MakeGenericType(args[0], args[1], typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType);
		}

		internal static Type[] GetDictionaryArguments(Type type)
		{
			foreach (var iface in type.GetInterfaces())
			{
				if (!iface.IsGenericType)
					continue;
				var def = iface.GetGenericTypeDefinition();
				if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
					return iface.GetGenericArguments();
			}
			return null;
		}

		internal static ConstructorInfo FindDictionaryConstructor(Type type, Type[] args)
		{
			var dict = typeof(IDictionary<,>).MakeGenericType(args);
			var readOnly = typeof(IReadOnlyDictionary<,>).MakeGenericType(args);
			return type.GetConstructors()
				.FirstOrDefault(c =>
				{
					var ps = c.GetParameters();
					return ps.Length == 1
						&& (ps[0].ParameterType.IsAssignableFrom(dict) || ps[0].ParameterType.IsAssignableFrom(readOnly));
				});
		}
	}

	internal sealed class ReadOnlyDictionaryConverter<TKey, TValue, TDictionary> : JsonConverter<TDictionary>
		where TDictionary : class
	{
		public override TDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var backing = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(ref reader, options)
				?? new Dictionary<TKey, TValue>();

			var args = ReadOnlyDictionaryConverterFactory.GetDictionaryArguments(typeToConvert);
			var ctor = ReadOnlyDictionaryConverterFactory.FindDictionaryConstructor(typeToConvert, args);
			return (TDictionary)ctor.Invoke(new object[] { backing });
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
				writer.WritePropertyName(kvp.Key?.ToString() ?? string.Empty);
				JsonSerializer.Serialize(writer, kvp.Value, options);
			}
			writer.WriteEndObject();
		}
	}
}
