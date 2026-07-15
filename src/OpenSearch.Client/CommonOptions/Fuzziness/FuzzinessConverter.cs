/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes <see cref="IFuzziness"/>/<see cref="Fuzziness"/> as:
	/// <list type="bullet">
	/// <item><c>"AUTO"</c> or <c>"AUTO:low,high"</c></item>
	/// <item>integer edit distance</item>
	/// <item>double ratio</item>
	/// </list>
	/// </summary>
	internal sealed class FuzzinessConverter : JsonConverter<IFuzziness>
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeof(IFuzziness).IsAssignableFrom(typeToConvert);

		public override IFuzziness Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType == JsonTokenType.String)
			{
				var str = reader.GetString();
				if (string.Equals(str, "AUTO", StringComparison.OrdinalIgnoreCase))
					return Fuzziness.Auto;

				if (str != null && str.StartsWith("AUTO:", StringComparison.OrdinalIgnoreCase))
				{
					var parts = str.Substring(5).Split(',');
					if (parts.Length == 2
						&& int.TryParse(parts[0], out var low)
						&& int.TryParse(parts[1], out var high))
						return Fuzziness.AutoLength(low, high);
				}

				return Fuzziness.Auto;
			}

			if (reader.TokenType == JsonTokenType.Number)
			{
				if (reader.TryGetInt32(out var editDistance))
					return Fuzziness.EditDistance(editDistance);
				if (reader.TryGetDouble(out var ratio))
					return Fuzziness.Ratio(ratio);
			}

			reader.Skip();
			return null;
		}

		public override void Write(Utf8JsonWriter writer, IFuzziness value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			if (value.Auto)
			{
				if (!value.Low.HasValue || !value.High.HasValue)
					writer.WriteStringValue("AUTO");
				else
					writer.WriteStringValue($"AUTO:{value.Low},{value.High}");
			}
			else if (value.EditDistance.HasValue)
				writer.WriteNumberValue(value.EditDistance.Value);
			else if (value.Ratio.HasValue)
				writer.WriteNumberValue(value.Ratio.Value);
			else
				writer.WriteNullValue();
		}
	}
}
