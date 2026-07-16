/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for <see cref="System.ValueTuple"/> types
	/// (e.g. <c>(string info, int number)</c>). System.Text.Json does not serialize the public
	/// <c>Item1</c>..<c>Item8</c> fields of a value tuple unless <c>IncludeFields</c> is enabled,
	/// so a value-tuple-typed property is otherwise written as an empty object (<c>{}</c>).
	/// </summary>
	/// <remarks>
	/// This converter emits the tuple as a JSON object with the field names <c>Item1</c>,
	/// <c>Item2</c>, ... exactly as they are declared (they are not run through the naming policy),
	/// matching the historical Utf8Json wire behavior. This also covers <see cref="Nullable{T}"/>
	/// value tuples: STJ unwraps the nullable and delegates the non-null value to this converter.
	/// </remarks>
	internal sealed class ValueTupleConverter : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> Cache = new();

		public override bool CanConvert(Type typeToConvert)
		{
			if (!typeToConvert.IsValueType || !typeToConvert.IsGenericType)
				return false;

			var def = typeToConvert.GetGenericTypeDefinition();
			return def == typeof(ValueTuple<>)
				|| def == typeof(ValueTuple<,>)
				|| def == typeof(ValueTuple<,,>)
				|| def == typeof(ValueTuple<,,,>)
				|| def == typeof(ValueTuple<,,,,>)
				|| def == typeof(ValueTuple<,,,,,>)
				|| def == typeof(ValueTuple<,,,,,,>)
				|| def == typeof(ValueTuple<,,,,,,,>);
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			Cache.GetOrAdd(typeToConvert, static t =>
			{
				var converterType = typeof(ValueTupleConverterInner<>).MakeGenericType(t);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});

		private sealed class ValueTupleConverterInner<T> : JsonConverter<T>
		{
			// Item1..Item7 (and Rest) in declaration order.
			private static readonly FieldInfo[] Fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public);

			public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			{
				if (reader.TokenType == JsonTokenType.Null)
					return default;

				if (reader.TokenType != JsonTokenType.StartObject)
					throw new JsonException($"Expected StartObject when deserializing {typeof(T)}, found {reader.TokenType}.");

				// Boxing is required because ValueType fields cannot be set on a struct value directly via reflection
				// without re-boxing; value tuples have no parameterless-settable path otherwise.
				object boxed = Activator.CreateInstance(typeof(T));

				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					if (reader.TokenType != JsonTokenType.PropertyName)
						continue;

					var name = reader.GetString();
					reader.Read();

					var field = Array.Find(Fields, f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
					if (field == null)
					{
						reader.Skip();
						continue;
					}

					var value = JsonSerializer.Deserialize(ref reader, field.FieldType, options);
					field.SetValue(boxed, value);
				}

				return (T)boxed;
			}

			public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
			{
				writer.WriteStartObject();
				foreach (var field in Fields)
				{
					writer.WritePropertyName(field.Name);
					JsonSerializer.Serialize(writer, field.GetValue(value), field.FieldType, options);
				}
				writer.WriteEndObject();
			}
		}
	}
}
