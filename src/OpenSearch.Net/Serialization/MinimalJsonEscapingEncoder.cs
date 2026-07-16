/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Encodings.Web;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JavaScriptEncoder"/> that escapes only the characters that are strictly required
	/// by the JSON specification, matching the historical Utf8Json wire behavior (and Newtonsoft.Json's
	/// default): the C0 control characters (U+0000–U+001F), the quotation mark (<c>"</c>) and reverse
	/// solidus (<c>\</c>), plus the three characters that are valid in JSON strings but are line
	/// terminators in ECMAScript (U+0085 NEL, U+2028 LINE SEPARATOR, U+2029 PARAGRAPH SEPARATOR).
	/// </summary>
	/// <remarks>
	/// System.Text.Json's built-in encoders (including <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>)
	/// additionally escape U+007F and every code point outside their allow-listed Unicode ranges (e.g.
	/// unassigned code points). That produces a much larger set of <c>\uXXXX</c> escapes than the client
	/// historically emitted, which is both wasteful and diverges from the previous wire format. This encoder
	/// leaves all other code points (including non-ASCII and unassigned) as literal UTF-8, exactly like
	/// Utf8Json did (the only difference being lowercase hex digits, which is irrelevant since JSON escape
	/// sequences are case-insensitive).
	/// </remarks>
	internal sealed class MinimalJsonEscapingEncoder : JavaScriptEncoder
	{
		public static readonly MinimalJsonEscapingEncoder Shared = new();

		// A backslash escape is at most 6 chars (\uXXXX).
		public override int MaxOutputCharactersPerInputCharacter => 6;

		public override bool WillEncode(int unicodeScalar) =>
			unicodeScalar < 0x20
			|| unicodeScalar == '"'
			|| unicodeScalar == '\\'
			|| unicodeScalar == 0x85
			|| unicodeScalar == 0x2028
			|| unicodeScalar == 0x2029;

		public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
		{
			for (var i = 0; i < textLength; i++)
			{
				if (WillEncode(text[i]))
					return i;
			}
			return -1;
		}

		public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
		{
			string escaped;
			switch (unicodeScalar)
			{
				case '"': escaped = "\\\""; break;
				case '\\': escaped = "\\\\"; break;
				case 0x08: escaped = "\\b"; break;
				case 0x09: escaped = "\\t"; break;
				case 0x0A: escaped = "\\n"; break;
				case 0x0C: escaped = "\\f"; break;
				case 0x0D: escaped = "\\r"; break;
				default: escaped = "\\u" + unicodeScalar.ToString("x4"); break;
			}

			if (escaped.Length > bufferLength)
			{
				numberOfCharactersWritten = 0;
				return false;
			}

			for (var i = 0; i < escaped.Length; i++)
				buffer[i] = escaped[i];

			numberOfCharactersWritten = escaped.Length;
			return true;
		}
	}
}
