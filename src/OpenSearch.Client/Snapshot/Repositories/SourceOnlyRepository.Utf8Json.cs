/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Runtime.Serialization;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class SourceOnlyRepositoryFormatter : IJsonFormatter<ISourceOnlyRepository>
	{
		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "type", 0 },
			{ "settings", 1 }
		};

		private static readonly byte[] DelegateType = JsonWriter.GetEncodedPropertyNameWithoutQuotation("delegate_type");

		public void Serialize(ref JsonWriter writer, ISourceOnlyRepository value, IJsonFormatterResolver formatterResolver)
		{
			if (value.DelegateType.IsNullOrEmpty())
			{
				writer.WriteNull();
				return;
			}
			writer.WriteBeginObject();
			writer.WritePropertyName("type");
			writer.WriteString("source");
			if (value.DelegateSettings != null)
			{
				writer.WriteValueSeparator();
				writer.WritePropertyName("settings");
				writer.WriteBeginObject();
				writer.WritePropertyName("delegate_type");
				writer.WriteString(value.DelegateType);
				writer.WriteValueSeparator();

				var innerWriter = new JsonWriter();
				switch (value.DelegateType)
				{
					case "s3":
						Serialize<IS3RepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
					case "azure":
						Serialize<IAzureRepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
					case "url":
						Serialize<IReadOnlyUrlRepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
					case "hdfs":
						Serialize<IHdfsRepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
					case "fs":
						Serialize<IFileSystemRepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
					default:
						Serialize<IRepositorySettings>(ref innerWriter, value.DelegateSettings, formatterResolver);
						break;
				}

				var buffer = innerWriter.GetBuffer();
				// get all the written bytes between the opening and closing {}
				for (var i = 1; i < buffer.Count - 1; i++)
					writer.WriteRawUnsafe(buffer.Array[i]);

				writer.WriteEndObject();
			}
			writer.WriteEndObject();
		}

		private static void Serialize<TRepositorySettings>(ref JsonWriter writer, object value, IJsonFormatterResolver formatterResolver)
			where TRepositorySettings : class, IRepositorySettings
		{
			var formatter = formatterResolver.GetFormatter<TRepositorySettings>();
			formatter.Serialize(ref writer, value as TRepositorySettings, formatterResolver);
		}

		private static TRepositorySettings Deserialize<TRepositorySettings>(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
			where TRepositorySettings : class, IRepositorySettings
		{
			var formatter = formatterResolver.GetFormatter<TRepositorySettings>();
			return formatter.Deserialize(ref reader, formatterResolver);
		}

		public ISourceOnlyRepository Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() != JsonToken.BeginObject)
			{
				reader.ReadNextBlock();
				return null;
			}

			var count = 0;
			ArraySegment<byte> settings = default;

			while (reader.ReadIsInObject(ref count))
			{
				var propertyName = reader.ReadPropertyNameSegmentRaw();
				if (Fields.TryGetValue(propertyName, out var value))
				{
					switch (value)
					{
						case 0:
							reader.ReadNext();
							break;
						case 1:
							settings = reader.ReadNextBlockSegment();
							break;
					}

				}
				else
					reader.ReadNextBlock();
			}

			if (settings == default)
				return null;

			var segmentReader = new JsonReader(settings.Array, settings.Offset);
			string delegateType = null;
			object delegateSettings = null;

			// reset count to zero to so that ReadIsInObject skips opening brace
			count = 0;
			while (segmentReader.ReadIsInObject(ref count))
			{
				var propertyName = segmentReader.ReadPropertyNameSegmentRaw();
				if (propertyName.EqualsBytes(DelegateType))
				{
					delegateType = segmentReader.ReadString();
					break;
				}

				segmentReader.ReadNextBlock();
			}

			// reset the offset
			segmentReader.ResetOffset();

			switch (delegateType)
			{
				case "s3":
					delegateSettings = Deserialize<S3RepositorySettings>(ref segmentReader, formatterResolver);
					break;
				case "azure":
					delegateSettings = Deserialize<AzureRepositorySettings>(ref segmentReader, formatterResolver);
					break;
				case "url":
					delegateSettings = Deserialize<ReadOnlyUrlRepositorySettings>(ref segmentReader, formatterResolver);
					break;
				case "hdfs":
					delegateSettings = Deserialize<HdfsRepositorySettings>(ref segmentReader, formatterResolver);
					break;
				case "fs":
					delegateSettings = Deserialize<FileSystemRepositorySettings>(ref segmentReader, formatterResolver);
					break;
			}

			return new SourceOnlyRepository(delegateType, delegateSettings);
		}
	}

}
