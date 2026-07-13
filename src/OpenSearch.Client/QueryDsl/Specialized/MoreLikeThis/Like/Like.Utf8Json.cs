/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class LikeFormatter : IJsonFormatter<Like>
	{
		private static readonly UnionFormatter<string, ILikeDocument> UnionFormatter = new UnionFormatter<string, ILikeDocument>();

		public Like Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var union = UnionFormatter.Deserialize(ref reader, formatterResolver);

			if (union == null)
				return null;

			switch (union.Tag)
			{
				case 0:
					return new Like(union.Item1);
				case 1:
					return new Like(union.Item2);
				default:
					return null;
			}
		}

		public void Serialize(ref JsonWriter writer, Like value, IJsonFormatterResolver formatterResolver) =>
			UnionFormatter.Serialize(ref writer, value, formatterResolver);
	}

}
