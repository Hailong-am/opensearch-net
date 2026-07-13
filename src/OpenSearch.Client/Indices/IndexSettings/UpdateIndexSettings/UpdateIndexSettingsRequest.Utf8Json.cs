/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class UpdateIndexSettingsRequestFormatter : IJsonFormatter<IUpdateIndexSettingsRequest>
	{
		private static readonly DynamicIndexSettingsFormatter DynamicIndexSettingsFormatter =
			new DynamicIndexSettingsFormatter();

		public IUpdateIndexSettingsRequest Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var dynamicSettings = DynamicIndexSettingsFormatter.Deserialize(ref reader, formatterResolver);
			return new UpdateIndexSettingsRequest { IndexSettings = dynamicSettings };
		}

		public void Serialize(ref JsonWriter writer, IUpdateIndexSettingsRequest value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			DynamicIndexSettingsFormatter.Serialize(ref writer, value.IndexSettings, formatterResolver);
		}
	}

}
