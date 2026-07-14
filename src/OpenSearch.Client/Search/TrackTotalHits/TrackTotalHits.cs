/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/


using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using OpenSearch.Net.Utf8Json;
namespace OpenSearch.Client;

[JsonConverter(typeof(TrackTotalHitsConverter))]
[JsonFormatter(typeof(TrackTotalHitsFormatter))]
public class TrackTotalHits : Union<bool, long>
{
	public TrackTotalHits(bool item) : base(item) { }

	public TrackTotalHits(long item) : base(item) { }

	public static implicit operator TrackTotalHits(bool trackTotalHits) => new(trackTotalHits);
	public static implicit operator TrackTotalHits(bool? trackTotalHits) => trackTotalHits is {} b ? new TrackTotalHits(b) : null;

	public static implicit operator TrackTotalHits(long trackTotalHitsUpTo) => new(trackTotalHitsUpTo);
	public static implicit operator TrackTotalHits(long? trackTotalHitsUpTo) => trackTotalHitsUpTo is {} l ? new TrackTotalHits(l) : null;

	public override string ToString() => Tag switch
	{
		0 => Item1.ToString(),
		1 => Item2.ToString(),
		_ => null
	};
}

/// <summary>
/// (De)serializes <see cref="TrackTotalHits"/> (a <see cref="Union{Boolean, Int64}"/> subtype).
/// Reads via the underlying union and wraps the resulting member in a <see cref="TrackTotalHits"/>;
/// writes whichever member is set.
/// </summary>
internal sealed class TrackTotalHitsConverter : JsonConverter<TrackTotalHits>
{
	public override TrackTotalHits Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Null)
			return null;

		var union = System.Text.Json.JsonSerializer.Deserialize<Union<bool, long>>(ref reader, options);
		if (union == null)
			return null;

		return union.Tag switch
		{
			0 => new TrackTotalHits(union.Item1),
			1 => new TrackTotalHits(union.Item2),
			_ => null
		};
	}

	public override void Write(Utf8JsonWriter writer, TrackTotalHits value, JsonSerializerOptions options)
	{
		if (value == null)
		{
			writer.WriteNullValue();
			return;
		}

		System.Text.Json.JsonSerializer.Serialize(writer, value, typeof(Union<bool, long>), options);
	}
}
