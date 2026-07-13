/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class AttachmentFormatter : IJsonFormatter<Attachment>
	{
		private static readonly AutomataDictionary AutomataDictionary = new AutomataDictionary
		{
			{ "_content", 0 },
			{ "content", 0 },
			{ "_name", 1 },
			{ "name", 1 },
			{ "author", 2 },
			{ "keywords", 3 },
			{ "date", 4 },
			{ "_content_type", 5 },
			{ "content_type", 5 },
			{ "_content_length", 6 },
			{ "content_length", 6 },
			{ "contentlength", 6 },
			{ "_language", 7 },
			{ "language", 7 },
			{ "_detect_language", 8 },
			{ "detect_language", 8 },
			{ "_indexed_chars", 9 },
			{ "indexed_chars", 9 },
			{ "title", 10 },
		};

		public Attachment Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();

			if (token == JsonToken.String)
				return new Attachment { Content = reader.ReadString() };

			if (token == JsonToken.BeginObject)
			{
				var attachment = new Attachment();
				var count = 0;
				while (reader.ReadIsInObject(ref count))
				{
					var propertyName = reader.ReadPropertyNameSegmentRaw();
					if (AutomataDictionary.TryGetValue(propertyName, out var value))
					{
						switch (value)
						{
							case 0:
								attachment.Content = reader.ReadString();
								break;
							case 1:
								attachment.Name = reader.ReadString();
								break;
							case 2:
								attachment.Author = reader.ReadString();
								break;
							case 3:
								attachment.Keywords = reader.ReadString();
								break;
							case 4:
								attachment.Date = formatterResolver.GetFormatter<DateTime?>()
									.Deserialize(ref reader, formatterResolver);
								break;
							case 5:
								attachment.ContentType = reader.ReadString();
								break;
							case 6:
								attachment.ContentLength = reader.ReadNullableLong();
								break;
							case 7:
								attachment.Language = reader.ReadString();
								break;
							case 8:
								attachment.DetectLanguage = reader.ReadNullableBoolean();
								break;
							case 9:
								attachment.IndexedCharacters = reader.ReadNullableLong();
								break;
							case 10:
								attachment.Title = reader.ReadString();
								break;
						}
					}
				}

				return attachment;
			}

			return null;
		}

		public void Serialize(ref JsonWriter writer, Attachment value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			if (!value.ContainsMetadata)
				writer.WriteString(value.Content);
			else
			{
				writer.WriteBeginObject();
				writer.WritePropertyName("content");
				writer.WriteString(value.Content);

				if (!string.IsNullOrEmpty(value.Author))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("author");
					writer.WriteString(value.Author);
				}

				if (value.ContentLength.HasValue)
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("content_length");
					writer.WriteInt64(value.ContentLength.Value);
				}

				if (!string.IsNullOrEmpty(value.ContentType))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("content_type");
					writer.WriteString(value.ContentType);
				}

				if (value.Date.HasValue)
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("date");
					formatterResolver.GetFormatter<DateTime?>().Serialize(ref writer, value.Date, formatterResolver);
				}

				if (value.DetectLanguage.HasValue)
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("detect_language");
					writer.WriteBoolean(value.DetectLanguage.Value);
				}

				if (value.IndexedCharacters.HasValue)
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("indexed_chars");
					writer.WriteInt64(value.IndexedCharacters.Value);
				}

				if (!string.IsNullOrEmpty(value.Keywords))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("keywords");
					writer.WriteString(value.Keywords);
				}

				if (!string.IsNullOrEmpty(value.Language))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("language");
					writer.WriteString(value.Language);
				}

				if (!string.IsNullOrEmpty(value.Name))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("name");
					writer.WriteString(value.Name);
				}

				if (!string.IsNullOrEmpty(value.Title))
				{
					writer.WriteValueSeparator();
					writer.WritePropertyName("title");
					writer.WriteString(value.Title);
				}

				writer.WriteEndObject();
			}
		}
	}

}
