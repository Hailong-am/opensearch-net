/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class DateMathFormatter : IJsonFormatter<DateMath>
	{
		public DateMath Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();
			if (token != JsonToken.String)
				return null;

			var segment = reader.ReadStringSegmentUnsafe();

			if (!segment.ContainsDateMathSeparator() && segment.IsDateTime(formatterResolver, out var dateTime))
				return DateMath.Anchored(dateTime);

			var value = segment.Utf8String();
			return DateMath.FromString(value);
		}

		public void Serialize(ref JsonWriter writer, DateMath value, IJsonFormatterResolver formatterResolver) =>
			writer.WriteString(value.ToString());
	}

}
