/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Formatters;

namespace OpenSearch.Client
{
	[AttributeUsage(AttributeTargets.Property)]
	public class EpochDateTimeAttribute : Attribute { }
	internal static class DateTimeUtil
	{
		public static readonly DateTimeOffset UnixEpoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);
	}

	internal class EpochDateTimeOffsetFormatter : IJsonFormatter<DateTimeOffset>
	{
		public DateTimeOffset Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();

			if (token == JsonToken.String)
			{
				var formatter = formatterResolver.GetFormatter<DateTimeOffset>();
				return formatter.Deserialize(ref reader, formatterResolver);
			}
			if (token == JsonToken.Null)
			{
				reader.ReadNext();
				return default;
			}

			if (token == JsonToken.Number)
			{
				var millisecondsSinceEpoch = reader.ReadDouble();
				var dateTimeOffset = DateTimeUtil.UnixEpoch.AddMilliseconds(millisecondsSinceEpoch);
				return dateTimeOffset;
			}

			throw new Exception($"Cannot deserialize {nameof(DateTimeOffset)} from token {token}");
		}

		public virtual void Serialize(ref JsonWriter writer, DateTimeOffset value, IJsonFormatterResolver formatterResolver) =>
			ISO8601DateTimeOffsetFormatter.Default.Serialize(ref writer, value, formatterResolver);
	}

	internal class EpochDateTimeFormatter : IJsonFormatter<DateTime>
	{
		public static readonly EpochDateTimeFormatter Instance = new EpochDateTimeFormatter();

		public DateTime Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();

			if (token == JsonToken.String)
			{
				var formatter = formatterResolver.GetFormatter<DateTime>();
				return formatter.Deserialize(ref reader, formatterResolver);
			}
			if (token == JsonToken.Null)
			{
				reader.ReadNext();
				return default;
			}

			if (token == JsonToken.Number)
			{
				var millisecondsSinceEpoch = reader.ReadDouble();
				var dateTimeOffset = DateTimeUtil.UnixEpoch.AddMilliseconds(millisecondsSinceEpoch);
				return dateTimeOffset.DateTime;
			}

			throw new Exception($"Cannot deserialize {nameof(DateTimeOffset)} from token {token}");
		}

		public void Serialize(ref JsonWriter writer, DateTime value, IJsonFormatterResolver formatterResolver) =>
			ISO8601DateTimeFormatter.Default.Serialize(ref writer, value, formatterResolver);
	}


}
