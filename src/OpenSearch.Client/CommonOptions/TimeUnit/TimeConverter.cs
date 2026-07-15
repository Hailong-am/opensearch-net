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
	/// Serializes <see cref="Time"/> as either a string (<c>"7d"</c>) or milliseconds integer.
	/// <list type="bullet">
	/// <item>-1 → <c>-1</c> (special "minus one")</item>
	/// <item>0 → <c>0</c> (special "zero")</item>
	/// <item>Factor + Interval → string like <c>"7d"</c></item>
	/// <item>Milliseconds only → integer</item>
	/// </list>
	/// </summary>
	internal sealed class TimeConverter : JsonConverter<Time>
	{
		public override Time Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.String:
					var str = reader.GetString();
					return str == null ? null : new Time(str);
				case JsonTokenType.Number:
					var ms = reader.GetInt64();
					if (ms == -1) return Time.MinusOne;
					if (ms == 0) return Time.Zero;
					return new Time(ms);
				case JsonTokenType.Null:
					return null;
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, Time value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value == Time.MinusOne)
				writer.WriteNumberValue(-1);
			else if (value == Time.Zero)
				writer.WriteNumberValue(0);
			else if (value.Factor.HasValue && value.Interval.HasValue)
				writer.WriteStringValue(value.ToString());
			else if (value.Milliseconds != null)
				writer.WriteNumberValue((long)value.Milliseconds);
			else
				writer.WriteNullValue();
		}
	}
}
