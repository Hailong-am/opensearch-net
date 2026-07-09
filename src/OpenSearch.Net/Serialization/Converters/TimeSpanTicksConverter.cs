/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="TimeSpan"/> that serializes as ticks (a long number)
	/// and can deserialize from either a number (ticks) or a string representation.
	/// Replaces the Utf8Json-based <c>TimeSpanTicksFormatter</c> in OpenSearch.Client.
	/// </summary>
	internal class TimeSpanTicksConverter : JsonConverter<TimeSpan>
	{
		public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Number:
					return new TimeSpan(reader.GetInt64());
				case JsonTokenType.String:
					var str = reader.GetString();
					if (long.TryParse(str, out var ticks))
						return new TimeSpan(ticks);
					return TimeSpan.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
				default:
					throw new JsonException($"Cannot convert token of type {reader.TokenType} to {nameof(TimeSpan)}.");
			}
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
			writer.WriteNumberValue(value.Ticks);
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for nullable <see cref="TimeSpan"/> that serializes as ticks (a long number).
	/// Replaces the Utf8Json-based <c>NullableTimeSpanTicksFormatter</c> in OpenSearch.Client.
	/// </summary>
	internal class NullableTimeSpanTicksConverter : JsonConverter<TimeSpan?>
	{
		private static readonly TimeSpanTicksConverter InnerConverter = new TimeSpanTicksConverter();

		public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			return InnerConverter.Read(ref reader, typeof(TimeSpan), options);
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteNumberValue(value.Value.Ticks);
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="TimeSpan"/> that serializes as its string form
	/// (e.g. <c>"10.10:38:32"</c>), and reads either a string or a ticks number.
	/// </summary>
	internal class TimeSpanStringConverter : JsonConverter<TimeSpan>
	{
		public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.String:
					var str = reader.GetString();
					if (long.TryParse(str, out var ticks))
						return new TimeSpan(ticks);
					return TimeSpan.Parse(str, System.Globalization.CultureInfo.InvariantCulture);
				case JsonTokenType.Number:
					return new TimeSpan(reader.GetInt64());
				default:
					throw new JsonException($"Cannot convert token of type {reader.TokenType} to {nameof(TimeSpan)}.");
			}
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.ToString());
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for nullable <see cref="TimeSpan"/> that serializes as its string form.
	/// </summary>
	internal class NullableTimeSpanStringConverter : JsonConverter<TimeSpan?>
	{
		private static readonly TimeSpanStringConverter InnerConverter = new TimeSpanStringConverter();

		public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			return InnerConverter.Read(ref reader, typeof(TimeSpan), options);
		}

		public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				InnerConverter.Write(writer, value.Value, options);
		}
	}
}
