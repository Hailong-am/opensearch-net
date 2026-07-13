/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class DateMathTimeFormatter: IJsonFormatter<DateMathTime>
	{
		public void Serialize(ref JsonWriter writer, DateMathTime value, IJsonFormatterResolver formatterResolver)
		{
			if (value is null) writer.WriteNull();
			else writer.WriteString(value.ToString());
		}

		public DateMathTime Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver) => reader.ReadString();
	}

}
