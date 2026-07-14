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

namespace OpenSearch.Net
{
	/// <summary>
	/// Serializes <see cref="double"/> so that integral values are written with a trailing
	/// <c>.0</c> (e.g. <c>1.0</c> rather than <c>1</c>), matching the client's historical
	/// (Utf8Json) behavior and disambiguating floating-point fields on the wire.
	/// </summary>
	internal sealed class DoubleConverter : JsonConverter<double>
	{
		public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				return double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
			}
			return reader.GetDouble();
		}

		public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
		{
			if (!double.IsNaN(value) && !double.IsInfinity(value)
				&& Math.Floor(value) == value && Math.Abs(value) < 1e16)
			{
				writer.WriteRawValue(
					value.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
				return;
			}

			writer.WriteNumberValue(value);
		}
	}

	/// <summary>
	/// Serializes <see cref="float"/> so that integral values are written with a trailing <c>.0</c>.
	/// </summary>
	internal sealed class SingleConverter : JsonConverter<float>
	{
		public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				return float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
			}
			return reader.GetSingle();
		}

		public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
		{
			if (!float.IsNaN(value) && !float.IsInfinity(value)
				&& Math.Floor((double)value) == value && Math.Abs(value) < 1e7)
			{
				writer.WriteRawValue(
					value.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
				return;
			}

			writer.WriteNumberValue(value);
		}
	}

	/// <summary>
	/// Serializes <see cref="decimal"/> so that integral values are written with a trailing <c>.0</c>.
	/// </summary>
	internal sealed class DecimalConverter : JsonConverter<decimal>
	{
		public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				return decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
			}
			return reader.GetDecimal();
		}

		public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
		{
			if (decimal.Truncate(value) == value)
			{
				writer.WriteRawValue(
					value.ToString("0.0", CultureInfo.InvariantCulture), skipInputValidation: true);
				return;
			}

			writer.WriteNumberValue(value);
		}
	}
}
