/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class DateMathExpressionFormatter : IJsonFormatter<DateMathExpression>
	{
		public DateMathExpression Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();
			if (token != JsonToken.String)
				return null;

			var segment = reader.ReadStringSegmentUnsafe();

			if (!segment.ContainsDateMathSeparator() && segment.IsDateTime(formatterResolver, out var dateTime))
				return new DateMathExpression(dateTime);

			var value = segment.Utf8String();
			return new DateMathExpression(value);
		}

		public void Serialize(ref JsonWriter writer, DateMathExpression value, IJsonFormatterResolver formatterResolver) =>
			writer.WriteString(value.ToString());
	}

}
