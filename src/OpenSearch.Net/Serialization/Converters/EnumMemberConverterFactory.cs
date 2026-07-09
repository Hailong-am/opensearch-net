/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> that handles serialization of enum types using
	/// <see cref="EnumMemberAttribute"/> values when present, falling back to camelCase
	/// string representation. Replaces the Utf8Json-based <c>OpenSearchNetEnumResolver</c>.
	/// </summary>
	internal sealed class EnumMemberConverterFactory : JsonConverterFactory
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert.IsEnum || (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum ?? false);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
			var isNullable = Nullable.GetUnderlyingType(typeToConvert) != null;

			var converterType = isNullable
				? typeof(NullableEnumMemberConverter<>).MakeGenericType(enumType)
				: typeof(EnumMemberConverter<>).MakeGenericType(enumType);

			return (JsonConverter)Activator.CreateInstance(converterType);
		}
	}

	/// <summary>
	/// Handles serialization/deserialization of a non-nullable enum type <typeparamref name="TEnum"/>.
	/// Uses <see cref="EnumMemberAttribute"/> values when available, camelCase name otherwise.
	/// Returns <c>default(TEnum)</c> for unknown string values during deserialization.
	/// </summary>
	internal sealed class EnumMemberConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
	{
		private readonly Dictionary<TEnum, string> _enumToString;
		private readonly Dictionary<string, TEnum> _stringToEnum;

		public EnumMemberConverter()
		{
			var enumType = typeof(TEnum);
			var values = (TEnum[])Enum.GetValues(enumType);

			_enumToString = new Dictionary<TEnum, string>(values.Length);
			_stringToEnum = new Dictionary<string, TEnum>(values.Length, StringComparer.OrdinalIgnoreCase);

			foreach (var value in values)
			{
				var name = value.ToString();
				var field = enumType.GetField(name);
				var enumMemberAttr = field?.GetCustomAttribute<EnumMemberAttribute>();

				var stringValue = enumMemberAttr?.Value ?? ToCamelCase(name);

				_enumToString[value] = stringValue;
				// Only store the first mapping for duplicate string values
				if (!_stringToEnum.ContainsKey(stringValue))
					_stringToEnum[stringValue] = value;
			}
		}

		public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var stringValue = reader.GetString();
				if (stringValue != null && _stringToEnum.TryGetValue(stringValue, out var enumValue))
					return enumValue;

				// Unknown string value: return default (matching Utf8Json behavior)
				return default;
			}

			if (reader.TokenType == JsonTokenType.Number)
			{
				// Handle numeric enum values
				if (reader.TryGetInt32(out var intValue))
					return Enum.IsDefined(typeof(TEnum), intValue)
						? (TEnum)(object)intValue
						: default;
			}

			if (reader.TokenType == JsonTokenType.Null)
				return default;

			// For any other token type, return default
			return default;
		}

		public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
		{
			if (_enumToString.TryGetValue(value, out var stringValue))
				writer.WriteStringValue(stringValue);
			else
				writer.WriteStringValue(ToCamelCase(value.ToString()));
		}

		private static string ToCamelCase(string name)
		{
			if (string.IsNullOrEmpty(name))
				return name;

			if (char.IsLower(name[0]))
				return name;

			return char.ToLowerInvariant(name[0]) + name.Substring(1);
		}
	}

	/// <summary>
	/// Handles serialization/deserialization of nullable enum types (<c>TEnum?</c>).
	/// Serializes <c>null</c> → JSON null, deserializes JSON null → <c>null</c>.
	/// </summary>
	internal sealed class NullableEnumMemberConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
	{
		private readonly EnumMemberConverter<TEnum> _innerConverter = new EnumMemberConverter<TEnum>();

		public override TEnum? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			return _innerConverter.Read(ref reader, typeof(TEnum), options);
		}

		public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				_innerConverter.Write(writer, value.Value, options);
		}
	}
}
