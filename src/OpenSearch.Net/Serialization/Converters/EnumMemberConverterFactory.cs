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
	/// <see cref="EnumMemberAttribute"/> values when present, falling back to the verbatim
	/// enum member name. Replaces the Utf8Json-based <c>OpenSearchNetEnumResolver</c>.
	/// <para>
	/// Only enums explicitly marked <see cref="StringEnumAttribute"/> or <see cref="FlagsAttribute"/>
	/// are serialized by name; all other enums fall through to the System.Text.Json default (numeric
	/// underlying value), matching the legacy Utf8Json <c>OpenSearchNetEnumResolver</c> behavior.
	/// <c>[StringEnum]</c> also works through the attribute channel; this factory is the
	/// options-registered fallback (and the sole handler for un-attributed <c>[Flags]</c> enums).
	/// </para>
	/// </summary>
	internal sealed class EnumMemberConverterFactory : JsonConverterFactory
	{
		// Retained for API/attribute-channel compatibility (see StringEnumAttribute). Naming is now
		// always verbatim for the enums this factory handles, so the flag no longer alters behavior.
		private readonly bool _useVerbatimName;

		public EnumMemberConverterFactory(bool useVerbatimName = true) => _useVerbatimName = useVerbatimName;

		public override bool CanConvert(Type typeToConvert)
		{
			var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
			if (!enumType.IsEnum)
				return false;

			// Serialize by name for:
			//  - [StringEnum] enums (explicit opt-in to string form),
			//  - [Flags] enums (need pipe-joined string output, e.g. "AND|NEAR"),
			//  - enums that define any [EnumMember] value (their string form is meaningful,
			//    e.g. GeoOrientation -> "cw"/"ccw").
			// All other enums (e.g. GeoHashPrecision/GeoTilePrecision, which are bare numeric
			// levels) fall through to the System.Text.Json numeric default, matching the legacy
			// Utf8Json OpenSearchNetEnumResolver behavior.
			return enumType.GetCustomAttribute<StringEnumAttribute>() != null
				|| enumType.GetCustomAttribute<FlagsAttribute>() != null
				|| HasAnyEnumMember(enumType);
		}

		private static bool HasAnyEnumMember(Type enumType)
		{
			foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
			{
				if (field.GetCustomAttribute<EnumMemberAttribute>() != null)
					return true;
			}
			return false;
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var enumType = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;
			var isNullable = Nullable.GetUnderlyingType(typeToConvert) != null;

			var converterType = isNullable
				? typeof(NullableEnumMemberConverter<>).MakeGenericType(enumType)
				: typeof(EnumMemberConverter<>).MakeGenericType(enumType);

			return (JsonConverter)Activator.CreateInstance(converterType, _useVerbatimName);
		}
	}

	/// <summary>
	/// Handles serialization/deserialization of a non-nullable enum type <typeparamref name="TEnum"/>.
	/// Uses <see cref="EnumMemberAttribute"/> values when available, then <see cref="DataMemberAttribute"/>
	/// names, falling back to the verbatim enum member name (matching the legacy Utf8Json behavior).
	/// <see cref="FlagsAttribute"/> enums (de)serialize combined values as a pipe-joined list (e.g.
	/// <c>"AND|NEAR"</c>). Returns <c>default(TEnum)</c> for unknown string values during deserialization.
	/// </summary>
	internal sealed class EnumMemberConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
	{
		private readonly Dictionary<TEnum, string> _enumToString;
		private readonly Dictionary<string, TEnum> _stringToEnum;
		private readonly bool _isFlags;
		// Ordered (flag-bits, string) pairs in enum declaration order, used to emit [Flags]
		// combined values deterministically (e.g. "AND|NEAR").
		private readonly List<KeyValuePair<long, string>> _orderedFlags;

		public EnumMemberConverter() : this(true) { }

		public EnumMemberConverter(bool useVerbatimName)
		{
			var enumType = typeof(TEnum);
			var values = (TEnum[])Enum.GetValues(enumType);
			_isFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;

			_enumToString = new Dictionary<TEnum, string>(values.Length);
			_stringToEnum = new Dictionary<string, TEnum>(values.Length, StringComparer.OrdinalIgnoreCase);
			_orderedFlags = _isFlags ? new List<KeyValuePair<long, string>>(values.Length) : null;

			foreach (var value in values)
			{
				var name = value.ToString();
				var field = enumType.GetField(name);
				var enumMemberAttr = field?.GetCustomAttribute<EnumMemberAttribute>();
				var dataMemberAttr = field?.GetCustomAttribute<DataMemberAttribute>();

				// EnumMember value wins, then DataMember name, then the verbatim CLR member name
				// (matches the Utf8Json EnumFormatter behavior — no camelCasing).
				var stringValue = enumMemberAttr?.Value ?? dataMemberAttr?.Name ?? name;

				_enumToString[value] = stringValue;
				// Only store the first mapping for duplicate string values
				if (!_stringToEnum.ContainsKey(stringValue))
					_stringToEnum[stringValue] = value;

				_orderedFlags?.Add(new KeyValuePair<long, string>(Convert.ToInt64(value), stringValue));
			}
		}

		public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var stringValue = reader.GetString();
				if (stringValue == null)
					return default;

				if (_stringToEnum.TryGetValue(stringValue, out var enumValue))
					return enumValue;

				// For [Flags] enums, the wire value may be a pipe-joined list (e.g. "AND|NEAR").
				if (_isFlags && stringValue.IndexOf('|') >= 0)
				{
					long combined = 0;
					foreach (var part in stringValue.Split('|'))
					{
						var trimmed = part.Trim();
						if (trimmed.Length != 0 && _stringToEnum.TryGetValue(trimmed, out var flag))
							combined |= Convert.ToInt64(flag);
					}
					return (TEnum)Enum.ToObject(typeof(TEnum), combined);
				}

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
			{
				writer.WriteStringValue(stringValue);
				return;
			}

			// For [Flags] enums, a combined value that is not a single named member is written as a
			// pipe-joined list of the individual flag EnumMember values (e.g. "AND|NEAR").
			if (_isFlags)
			{
				var combined = Convert.ToInt64(value);
				if (combined != 0)
				{
					var parts = new List<string>();
					foreach (var pair in _orderedFlags)
					{
						var flag = pair.Key;
						if (flag != 0 && (combined & flag) == flag)
							parts.Add(pair.Value);
					}

					if (parts.Count > 0)
					{
						writer.WriteStringValue(string.Join("|", parts));
						return;
					}
				}
			}

			// Fall back to the verbatim member name (matches legacy Utf8Json behavior).
			writer.WriteStringValue(value.ToString());
		}
	}

	/// <summary>
	/// Handles serialization/deserialization of nullable enum types (<c>TEnum?</c>).
	/// Serializes <c>null</c> → JSON null, deserializes JSON null → <c>null</c>.
	/// </summary>
	internal sealed class NullableEnumMemberConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
	{
		private readonly EnumMemberConverter<TEnum> _innerConverter;

		public NullableEnumMemberConverter() : this(true) { }

		public NullableEnumMemberConverter(bool useVerbatimName) =>
			_innerConverter = new EnumMemberConverter<TEnum>(useVerbatimName);

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
