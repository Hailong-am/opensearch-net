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
		public override bool CanConvert(Type typeToConvert)
		{
			if (GetUnionGenericArgs(typeToConvert) == null)
				return false;

			// Derived Union types (e.g. Context : Union<string, GeoLocation>) that declare their
			// own [JsonConverter] should be handled by that converter, not the generic factory —
			// otherwise this factory would produce a base Union<,> instance that cannot be assigned
			// to the derived property type.
			var declared = System.Reflection.CustomAttributeExtensions
				.GetCustomAttributes(typeToConvert, typeof(JsonConverterAttribute), inherit: false);
			foreach (var attr in declared)
			{
				if (((JsonConverterAttribute)attr).ConverterType != typeof(UnionConverterFactory))
					return false;
			}

			return true;
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var args = GetUnionGenericArgs(typeToConvert);
			if (args == null)
				throw new JsonException($"{typeToConvert} is not a Union<,> type.");
		var args = GetUnionGenericArgs(typeToConvert);
		if (args == null)
			throw new InvalidOperationException($"Type {typeToConvert.Name} is not a Union<,> type.");
		var converterType = typeof(UnionConverter<,>).MakeGenericType(args[0], args[1]);
		return (JsonConverter)Activator.CreateInstance(converterType, typeToConvert);
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
		// The concrete (possibly derived) union type requested, e.g. MinimumShouldMatch : Union<int?, string>.
		// Constructing the derived type (rather than the base Union<,>) is required so the value can be
		// assigned to a property typed as the derived type.
		private readonly Type _unionType;

		public UnionConverter() : this(typeof(Union<TFirst, TSecond>)) { }

		public UnionConverter(Type unionType) => _unionType = unionType ?? typeof(Union<TFirst, TSecond>);

		public override bool CanConvert(Type typeToConvert) =>
			typeof(Union<TFirst, TSecond>).IsAssignableFrom(typeToConvert);

		public override Union<TFirst, TSecond> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// Buffer the value so we can attempt both member types against the same JSON.
			using var doc = JsonDocument.ParseValue(ref reader);
			var element = doc.RootElement;

			if (TryRead<TFirst>(element, options, out var first))
				return Construct(first);

			if (TryRead<TSecond>(element, options, out var second))
				return Construct(second);

			return null;
		}

		private Union<TFirst, TSecond> Construct<TItem>(TItem item)
		{
			// If the requested type is exactly the base Union<,>, construct it directly.
			if (_unionType != typeof(Union<TFirst, TSecond>))
			{
				// Find a single-parameter constructor on the derived type whose parameter type is
				// compatible with the item (handles e.g. MinimumShouldMatch(int) for a TFirst of int?).
				foreach (var ctor in _unionType.GetConstructors())
				{
					var ps = ctor.GetParameters();
					if (ps.Length != 1)
						continue;

					var pt = ps[0].ParameterType;
					var underlying = Nullable.GetUnderlyingType(pt) ?? pt;

					if (item == null)
					{
						if (!pt.IsValueType || Nullable.GetUnderlyingType(pt) != null)
							return (Union<TFirst, TSecond>)ctor.Invoke(new object[] { null });
						continue;
					}

					if (pt.IsInstanceOfType(item) || underlying.IsInstanceOfType(item))
						return (Union<TFirst, TSecond>)ctor.Invoke(new object[] { item });
				}
			}

			// Fall back to the base type if no matching derived constructor exists.
			return new Union<TFirst, TSecond>((dynamic)item);
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
			// Some value types (e.g. Distance, Time) throw on unparseable input while attempting the
			// first union member; treat these as "not this member" so the second member is attempted.
			catch (InvalidCastException)
			{
				value = default;
				return false;
			}
			catch (FormatException)
			{
				value = default;
				return false;
			}
			catch (ArgumentException)
			{
				value = default;
				return false;
			}
		}
	}
}
