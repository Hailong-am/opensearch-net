/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class SimpleQueryStringFlagsFormatter : IJsonFormatter<SimpleQueryStringFlags?>
	{
		public void Serialize(ref JsonWriter writer, SimpleQueryStringFlags? value, IJsonFormatterResolver formatterResolver)
		{
			if (!value.HasValue)
			{
				writer.WriteNull();
				return;
			}

			var e = value.Value;
			var list = new List<string>(13);
			if (e.HasFlag(SimpleQueryStringFlags.All)) list.Add("ALL");
			if (e.HasFlag(SimpleQueryStringFlags.None)) list.Add("NONE");
			if (e.HasFlag(SimpleQueryStringFlags.And)) list.Add("AND");
			if (e.HasFlag(SimpleQueryStringFlags.Or)) list.Add("OR");
			if (e.HasFlag(SimpleQueryStringFlags.Not)) list.Add("NOT");
			if (e.HasFlag(SimpleQueryStringFlags.Prefix)) list.Add("PREFIX");
			if (e.HasFlag(SimpleQueryStringFlags.Phrase)) list.Add("PHRASE");
			if (e.HasFlag(SimpleQueryStringFlags.Precedence)) list.Add("PRECEDENCE");
			if (e.HasFlag(SimpleQueryStringFlags.Escape)) list.Add("ESCAPE");
			if (e.HasFlag(SimpleQueryStringFlags.Whitespace)) list.Add("WHITESPACE");
			if (e.HasFlag(SimpleQueryStringFlags.Fuzzy)) list.Add("FUZZY");
			if (e.HasFlag(SimpleQueryStringFlags.Near)) list.Add("NEAR");
			if (e.HasFlag(SimpleQueryStringFlags.Slop)) list.Add("SLOP");
			var flags = string.Join("|", list);
			writer.WriteString(flags);
		}

		public SimpleQueryStringFlags? Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var flags = reader.ReadString();
			return flags?.Split('|')
				.Select(flag => flag.ToEnum<SimpleQueryStringFlags>())
				.Where(s => s.HasValue)
				.Aggregate(default(SimpleQueryStringFlags), (current, s) => current | s.Value);
		}
	}

}
