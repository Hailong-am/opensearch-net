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
using System.Text.RegularExpressions;

namespace OpenSearch.Net
{
	/// <summary>
	/// Shared ISO-8601 date-string normalization so that the flexible offset and fractional-second
	/// forms OpenSearch emits round-trip through System.Text.Json (whose built-in DateTime/DateTimeOffset
	/// readers only accept a stricter subset). Replaces the Utf8Json ISO8601 date formatters.
	/// </summary>
	internal static class IsoDateTimeHelper
	{
		// Matches a trailing numeric time-zone offset in "basic" form that STJ does not accept:
		//   +HH, -HH, +HHmm, -HHmm  (but NOT the already-extended +HH:mm).
		private static readonly Regex BasicOffset =
			new Regex(@"(?<sign>[+-])(?<hh>\d{2})(?<mm>\d{2})?$", RegexOptions.Compiled);

		// Matches the fractional-seconds group so we can clamp it to the 7 digits DateTime.Parse allows.
		private static readonly Regex FractionalSeconds =
			new Regex(@"(?<=T\d{2}:\d{2}:\d{2})\.(?<frac>\d+)", RegexOptions.Compiled);

		/// <summary>
		/// Normalizes an OpenSearch ISO-8601 date string so <see cref="DateTime.Parse(string, IFormatProvider, DateTimeStyles)"/>
		/// / <see cref="DateTimeOffset.Parse(string, IFormatProvider, DateTimeStyles)"/> can read it:
		/// converts a trailing basic-format offset (<c>+1000</c>, <c>+10</c>) to extended form
		/// (<c>+10:00</c>). Throws <see cref="FormatException"/> for malformed offsets (e.g. <c>+100</c>).
		/// </summary>
		public static string Normalize(string value)
		{
			if (string.IsNullOrEmpty(value))
				return value;

			// Only consider the portion after the time component so a date like 2020-07-31 is untouched.
			var tIndex = value.IndexOf('T');
			if (tIndex < 0)
				return value;

			// Clamp fractional seconds to 7 digits (the maximum DateTime/DateTimeOffset.Parse accepts).
			// OpenSearch may emit more precision than the CLR's 100ns tick resolution can represent.
			value = FractionalSeconds.Replace(value, m =>
			{
				var frac = m.Groups["frac"].Value;
				return "." + (frac.Length > 7 ? frac.Substring(0, 7) : frac);
			});

			var timePart = value.Substring(tIndex);

			// Already extended (has a ':' in an offset) or ends in Z → leave as-is.
			if (timePart.EndsWith("Z", StringComparison.Ordinal))
				return value;

			// Detect a trailing offset. Guard against malformed lengths like +100 / -10000 which are
			// neither +HH nor +HHmm; those must throw rather than silently mis-parse.
			var lastSign = value.LastIndexOfAny(new[] { '+', '-' });
			if (lastSign > tIndex) // a sign within the time part is an offset
			{
				var offset = value.Substring(lastSign); // e.g. "+100", "+1000", "+10"
				var digits = offset.Length - 1;
				if (digits != 2 && digits != 4 && !offset.Contains(":"))
					throw new InvalidOperationException($"Unsupported time-zone offset '{offset}' in date '{value}'.");

				var match = BasicOffset.Match(value);
				if (match.Success)
				{
					var mm = match.Groups["mm"].Success ? match.Groups["mm"].Value : "00";
					var normalizedOffset = $"{match.Groups["sign"].Value}{match.Groups["hh"].Value}:{mm}";
					return value.Substring(0, match.Index) + normalizedOffset;
				}
			}

			return value;
		}
	}

	/// <summary>Reads/writes <see cref="DateTime"/> with OpenSearch-flexible ISO-8601 handling.</summary>
	internal sealed class IsoDateTimeConverter : JsonConverter<DateTime>
	{
		public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var raw = reader.GetString();
				var normalized = IsoDateTimeHelper.Normalize(raw);
				return DateTime.Parse(normalized, CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
			}

			return reader.GetDateTime();
		}

		public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		{
			// Round-trip ("o") form, but drop an all-zero fractional-second component so a whole-second
			// UTC value serializes as e.g. "2016-01-01T01:01:01Z" (matching the historical wire format).
			var formatted = value.ToString("o", CultureInfo.InvariantCulture);
			writer.WriteStringValue(StripZeroFractionalSeconds(formatted));
		}

		internal static string StripZeroFractionalSeconds(string roundTrip) =>
			roundTrip.Replace(".0000000", string.Empty);
	}

	/// <summary>Reads/writes <see cref="DateTimeOffset"/> with OpenSearch-flexible ISO-8601 handling.</summary>
	internal sealed class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
	{
		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var raw = reader.GetString();
				var normalized = IsoDateTimeHelper.Normalize(raw);
				return DateTimeOffset.Parse(normalized, CultureInfo.InvariantCulture,
					DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces);
			}

			return reader.GetDateTimeOffset();
		}

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture));
	}
}
