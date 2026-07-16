/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes a <see cref="DateTimeOffset"/> as a string containing milliseconds-since-Unix-epoch,
	/// and deserializes either an epoch-milliseconds number/string or an ISO-8601 date string. Replaces
	/// the Utf8Json <c>DateTimeOffsetEpochMillisecondsFormatter</c>.
	/// </summary>
	internal sealed class EpochMillisecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
	{
		private static readonly DateTimeOffset UnixEpoch =
			new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Number:
					return UnixEpoch.AddMilliseconds(reader.GetDouble());
				case JsonTokenType.String:
					var s = reader.GetString();
					if (s == null)
						return default;
					if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMs))
						return UnixEpoch.AddMilliseconds(epochMs);
					return DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				default:
					reader.Skip();
					return default;
			}
		}

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
	}
}
