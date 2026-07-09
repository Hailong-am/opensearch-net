/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes <see cref="Union{TFirst,TSecond}"/> as whichever member is set, and on read
	/// attempts <c>TFirst</c> first, falling back to <c>TSecond</c>. Replaces the Utf8Json
	/// <c>UnionFormatter</c>.
	/// </summary>
	internal sealed class UnionConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert) =>
			GetUnionGenericArgs(typeToConvert) != null;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetUnionGenericArgs(typeToConvert);
			var converterType = typeof(UnionConverter<,>).MakeGenericType(args[0], args[1]);
			return (JsonConverter)Activator.CreateInstance(converterType);
		}

		/// <summary>
		/// Walks the type hierarchy to find the <c>Union&lt;TFirst,TSecond&gt;</c> base class
		/// and returns its generic arguments, or null if not found. This handles both
		/// <c>Union&lt;A,B&gt;</c> directly and derived types like <c>Like : Union&lt;string,ILikeDocument&gt;</c>.
		/// </summary>
		private static Type[] GetUnionGenericArgs(Type type)
		{
			var current = type;
			while (current != null)
			{
				if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Union<,>))
					return current.GetGenericArguments();
				current = current.BaseType;
			}
			return null;
		}
	}

	internal sealed class UnionConverter<TFirst, TSecond> : JsonConverter<Union<TFirst, TSecond>>
	{
		public override Union<TFirst, TSecond> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// Buffer the value so we can attempt both member types against the same JSON.
			using var doc = JsonDocument.ParseValue(ref reader);
			var element = doc.RootElement;

			if (TryRead<TFirst>(element, options, out var first))
				return new Union<TFirst, TSecond>(first);

			if (TryRead<TSecond>(element, options, out var second))
				return new Union<TFirst, TSecond>(second);

			return null;
		}

		public override void Write(Utf8JsonWriter writer, Union<TFirst, TSecond> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value.Tag)
			{
				case 0:
					SerializeValue(writer, value.Item1, options);
					break;
				case 1:
					SerializeValue(writer, value.Item2, options);
					break;
				default:
					throw new JsonException($"Unrecognized Union tag: {value.Tag}");
			}
		}

		private static void SerializeValue<T>(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Use runtime type to ensure the concrete type's contract (with [InterfaceDataContract]
			// properties) is used, not the declared interface type (which would serialize as {}).
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}

		private static bool TryRead<T>(JsonElement element, JsonSerializerOptions options, out T value)
		{
			try
			{
				value = element.Deserialize<T>(options);

				// For reference types, null means "couldn't produce a value".
				if (value == null)
					return false;

				// For enums, the deserializer returns default(TEnum) for unrecognized strings.
				// Validate by round-tripping: serialize back and compare to the original.
				// This prevents "2d" from being accepted as DateInterval.Second.
				if (typeof(T).IsEnum && element.ValueKind == JsonValueKind.String)
				{
					var original = element.GetString();
					var reserialized = JsonSerializer.Serialize(value, options).Trim('"');
					if (!string.Equals(original, reserialized, StringComparison.OrdinalIgnoreCase))
						return false;
				}

				return true;
			}
			catch (JsonException)
			{
				value = default;
				return false;
			}
			catch (NotSupportedException)
			{
				value = default;
				return false;
			}
		}
	}
}
