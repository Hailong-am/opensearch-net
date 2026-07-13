/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class ContextFormatter : IJsonFormatter<Context>
	{
		public Context Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var formatter = formatterResolver.GetFormatter<Union<string, GeoLocation>>();
			var union = formatter.Deserialize(ref reader, formatterResolver);
			switch (union.Tag)
			{
				case 0:
					return new Context(union.Item1);
				case 1:
					return new Context(union.Item2);
				default:
					return null;
			}
		}

		public void Serialize(ref JsonWriter writer, Context value, IJsonFormatterResolver formatterResolver)
		{
			var formatter = formatterResolver.GetFormatter<Union<string, GeoLocation>>();
			formatter.Serialize(ref writer, value, formatterResolver);
		}
	}

}
