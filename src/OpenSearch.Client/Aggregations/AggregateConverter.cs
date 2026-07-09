/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// STJ converter for <see cref="IAggregate"/> that uses heuristic-based parsing
	/// to determine the concrete aggregate type from the JSON structure.
	/// Replaces the Utf8Json-based <c>AggregateFormatter</c>.
	/// </summary>
	internal sealed class AggregateConverter : JsonConverter<IAggregate>
	{
		public static string[] AllReservedAggregationNames { get; }
		public static string UsingReservedAggNameFormat { get; }

		private const string AsStringSuffix = "_as_string";

		static AggregateConverter()
		{
			AllReservedAggregationNames = new[]
			{
				"after_key", "_as_string", "bg_count", "bottom_right",
				"bounds", "buckets", "count", "doc_count",
				"doc_count_error_upper_bound", "fields", "from", "top",
				"type", "from_as_string", "hits", "key", "key_as_string",
				"keys", "location", "max_score", "meta", "min",
				"min_length", "score", "sum_other_doc_count", "to",
				"to_as_string", "top_left", "total", "value",
				"value_as_string", "values", "geometry", "properties"
			};

			var allKeys = string.Join(", ", AllReservedAggregationNames);
			UsingReservedAggNameFormat =
				"'{0}' is one of the reserved aggregation keywords"
				+ " we use a heuristics based response parser and using these reserved keywords"
				+ " could throw its heuristics off course. We are working on a solution in OpenSearch itself to make"
				+ " the response parseable. For now these are all the reserved keywords: "
				+ allKeys;
		}

		public override IAggregate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			ReadAggregate(ref reader, options);

		public override void Write(Utf8JsonWriter writer, IAggregate value, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		internal IAggregate ReadAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			// Depth of the aggregate object's opening brace. The matching closing brace
			// is reported at this same depth by Utf8JsonReader. We use it below to reliably
			// re-synchronise the reader onto this aggregate's own EndObject regardless of
			// where the per-shape helper left it (helpers may stop on an inner EndObject).
			var startDepth = reader.CurrentDepth;

			reader.Read(); // Move past StartObject

			if (reader.TokenType == JsonTokenType.EndObject)
				return null;

			// First property name
			var propertyName = reader.GetString();
			reader.Read(); // Move to value

			Dictionary<string, object> meta = null;
			if (propertyName == "meta")
			{
				meta = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
				reader.Read(); // Move past value to next property name
				if (reader.TokenType == JsonTokenType.EndObject)
					return null;
				propertyName = reader.GetString();
				reader.Read(); // Move to value
			}

			IAggregate aggregate = null;
			switch (propertyName)
			{
				case "values":
					aggregate = GetPercentilesAggregate(ref reader, meta);
					break;
				case "value":
					aggregate = GetValueAggregate(ref reader, options, meta);
					break;
				case "after_key":
					aggregate = GetCompositeAggregate(ref reader, options, meta);
					break;
				case "buckets":
				case "doc_count_error_upper_bound":
					aggregate = GetMultiBucketAggregate(ref reader, options, propertyName, meta);
					break;
				case "count":
					aggregate = GetStatsAggregate(ref reader, options, meta);
					break;
				case "doc_count":
					aggregate = GetSingleBucketAggregate(ref reader, options, meta);
					break;
				case "bounds":
					aggregate = GetGeoBoundsAggregate(ref reader, options, meta);
					break;
				case "hits":
					aggregate = GetTopHitsAggregate(ref reader, options, meta);
					break;
				case "location":
					aggregate = GetGeoCentroidAggregate(ref reader, options, meta);
					break;
				case "fields":
					aggregate = GetMatrixStatsAggregate(ref reader, options, meta);
					break;
				case "min":
					aggregate = GetGeoLineAggregate(ref reader, options, meta);
					break;
				case "top":
				case "type":
					// Skip unrecognized root fields
					reader.Skip();
					break;
				default:
					reader.Skip();
					break;
			}

			// Re-synchronise the reader onto this aggregate object's own closing brace.
			// The per-shape helpers above are inconsistent about where they leave the reader:
			// some stop on the aggregate's own EndObject, others stop on an inner EndObject or
			// on a scalar value. We advance until we reach the EndObject at the same depth as
			// the opening brace, skipping any remaining sibling properties/values. This must
			// leave the reader positioned exactly on the LAST token of the value (the EndObject)
			// so STJ does not report "read too much or not enough".
			while (!(reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == startDepth))
			{
				if (!reader.Read())
					break;
			}

			return aggregate;
		}

		private IAggregate GetPercentilesAggregate(ref Utf8JsonReader reader, IReadOnlyDictionary<string, object> meta)
		{
			var metric = new PercentilesAggregate { Meta = meta };

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					var key = reader.GetString();
					reader.Read();
					if (key.Contains(AsStringSuffix))
					{
						reader.Skip();
						continue;
					}
					metric.Items.Add(new PercentileItem
					{
						Percentile = double.Parse(key, CultureInfo.InvariantCulture),
						Value = reader.TokenType == JsonTokenType.Null ? (double?)null : reader.GetDouble()
					});
				}
			}
			else if (reader.TokenType == JsonTokenType.StartArray)
			{
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					// Each item is { "key": N, "value": V }
					double percentile = 0;
					double? value = null;
					while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
					{
						var prop = reader.GetString();
						reader.Read();
						if (prop == "key") percentile = reader.GetDouble();
						else if (prop == "value") value = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						else reader.Skip();
					}
					metric.Items.Add(new PercentileItem { Percentile = percentile, Value = value });
				}
			}
			else
			{
				reader.Skip();
			}

			return metric;
		}

		private IAggregate GetValueAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			if (reader.TokenType == JsonTokenType.Number || reader.TokenType == JsonTokenType.Null)
			{
				var value = reader.TokenType == JsonTokenType.Null ? (double?)null : reader.GetDouble();
				string valueAsString = null;
				List<string> keys = null;

				// Peek ahead for additional properties within same object
				// We need to check sibling properties (value_as_string, keys)
				// They'll be read by the outer loop, but we use a helper approach:
				// Read remaining properties in the parent object
				while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
				{
					var prop = reader.GetString();
					reader.Read();
					if (prop == "value_as_string")
						valueAsString = reader.GetString();
					else if (prop == "keys")
						keys = JsonSerializer.Deserialize<List<string>>(ref reader, options);
					else if (prop == "meta" || prop.EndsWith(AsStringSuffix))
						reader.Skip();
					else
					{
						// Unknown property - skip
						reader.Skip();
					}
				}

				if (keys != null)
				{
					return new KeyedValueAggregate
					{
						Value = value,
						Keys = keys,
						Meta = meta
					};
				}

				return new ValueAggregate
				{
					Value = value,
					ValueAsString = valueAsString,
					Meta = meta
				};
			}

			// Non-numeric value = scripted metric (object or array)
			using var doc = JsonDocument.ParseValue(ref reader);
			var bytes = System.Text.Encoding.UTF8.GetBytes(doc.RootElement.GetRawText());
			return new ScriptedMetricAggregate { Meta = meta };
		}

		private IAggregate GetCompositeAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			// after_key value is the composite key
			var afterKeyDict = JsonSerializer.Deserialize<IReadOnlyDictionary<string, object>>(ref reader, options);
			var afterKey = afterKeyDict != null ? new CompositeKey(afterKeyDict) : null;

			// Next should be "buckets"
			BucketAggregate bucketAggregate = null;
			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				if (prop == "buckets")
				{
					bucketAggregate = GetMultiBucketAggregate(ref reader, options, "buckets", meta) as BucketAggregate;
					break;
				}
				else
					reader.Skip();
			}

			var result = bucketAggregate ?? new BucketAggregate { Meta = meta };
			result.AfterKey = afterKey;
			return result;
		}

		private IAggregate GetSingleBucketAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			var docCount = reader.TokenType == JsonTokenType.Null ? 0L : reader.GetInt64();
			Dictionary<string, IAggregate> subAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();

				if (prop == "bg_count")
				{
					reader.Skip(); // bg_count in single bucket context
					continue;
				}
				if (prop == "fields")
				{
					var fields = JsonSerializer.Deserialize<List<MatrixStatsField>>(ref reader, options);
					return new MatrixStatsAggregate { DocCount = docCount, Fields = fields, Meta = meta };
				}
				if (prop == "buckets")
				{
					var b = GetMultiBucketAggregate(ref reader, options, "buckets", meta) as BucketAggregate;
					return new BucketAggregate
					{
						DocCount = docCount,
						Items = b?.Items ?? EmptyReadOnly<IBucket>.Collection,
						Meta = meta
					};
				}

				// Sub-aggregation
				if (subAggregates == null)
					subAggregates = new Dictionary<string, IAggregate>();
				var subAgg = ReadAggregate(ref reader, options);
				subAggregates[prop] = subAgg;
			}

			return new SingleBucketAggregate(subAggregates ?? new Dictionary<string, IAggregate>())
			{
				DocCount = docCount,
				Meta = meta
			};
		}

		private IAggregate GetStatsAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			var count = reader.TokenType == JsonTokenType.Null ? 0L : reader.GetInt64();

			// Check if this might just be a GeoCentroidAggregate (only "count" with no stats fields)
			if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
				return new GeoCentroidAggregate { Count = count, Meta = meta };

			// Read remaining stats fields
			double? min = null, max = null, average = null;
			double sum = 0;
			double? sumOfSquares = null, variance = null, stdDeviation = null;
			double? variancePopulation = null, varianceSampling = null;
			double? stdDeviationPopulation = null, stdDeviationSampling = null;
			StandardDeviationBounds stdDeviationBounds = null;
			bool isExtended = false;

			while (reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();

				switch (prop)
				{
					case "min":
						min = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "max":
						max = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "avg":
						average = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "sum":
						sum = reader.TokenType == JsonTokenType.Null ? 0 : reader.GetDouble();
						break;
					case "sum_of_squares":
						isExtended = true;
						sumOfSquares = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "variance":
						isExtended = true;
						variance = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "std_deviation":
						isExtended = true;
						stdDeviation = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "std_deviation_bounds":
						isExtended = true;
						stdDeviationBounds = JsonSerializer.Deserialize<StandardDeviationBounds>(ref reader, options);
						break;
					case "variance_population":
						isExtended = true;
						variancePopulation = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "variance_sampling":
						isExtended = true;
						varianceSampling = ReadNullableDoubleOrString(ref reader);
						break;
					case "std_deviation_population":
						isExtended = true;
						stdDeviationPopulation = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
						break;
					case "std_deviation_sampling":
						isExtended = true;
						stdDeviationSampling = ReadNullableDoubleOrString(ref reader);
						break;
					default:
						if (prop.EndsWith(AsStringSuffix))
							reader.Skip();
						else
							reader.Skip();
						break;
				}

				if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
					break;
			}

			if (isExtended)
			{
				return new ExtendedStatsAggregate
				{
					Count = count,
					Min = min,
					Max = max,
					Average = average,
					Sum = sum,
					SumOfSquares = sumOfSquares,
					Variance = variance,
					StdDeviation = stdDeviation,
					StdDeviationBounds = stdDeviationBounds,
					VariancePopulation = variancePopulation,
					VarianceSampling = varianceSampling,
					StdDeviationPopulation = stdDeviationPopulation,
					StdDeviationSampling = stdDeviationSampling,
					Meta = meta
				};
			}

			return new StatsAggregate
			{
				Count = count,
				Min = min,
				Max = max,
				Average = average,
				Sum = sum,
				Meta = meta
			};
		}

		private static double? ReadNullableDoubleOrString(ref Utf8JsonReader reader)
		{
			if (reader.TokenType == JsonTokenType.Null) return null;
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
			}
			return reader.GetDouble();
		}

		private IAggregate GetGeoBoundsAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var geoBounds = new GeoBoundsAggregate { Meta = meta };

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					var prop = reader.GetString();
					reader.Read();
					switch (prop)
					{
						case "top_left":
							geoBounds.Bounds.TopLeft = JsonSerializer.Deserialize<LatLon>(ref reader, options);
							break;
						case "bottom_right":
							geoBounds.Bounds.BottomRight = JsonSerializer.Deserialize<LatLon>(ref reader, options);
							break;
						default:
							reader.Skip();
							break;
					}
				}
			}
			else
				reader.Skip();

			return geoBounds;
		}

		private IAggregate GetGeoCentroidAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			var geoCentroid = new GeoCentroidAggregate
			{
				Location = JsonSerializer.Deserialize<GeoLocation>(ref reader, options),
				Meta = meta
			};

			// Check for "count" property next
			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				if (prop == "count")
					geoCentroid.Count = reader.GetInt64();
				else
					reader.Skip();
			}

			return geoCentroid;
		}

		private IAggregate GetTopHitsAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			// "hits" value is an object with total, max_score, hits
			TotalHits total = null;
			double? maxScore = null;

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					var prop = reader.GetString();
					reader.Read();
					switch (prop)
					{
						case "total":
							total = JsonSerializer.Deserialize<TotalHits>(ref reader, options);
							break;
						case "max_score":
							maxScore = reader.TokenType == JsonTokenType.Null ? null : reader.GetDouble();
							break;
						case "hits":
							// Skip hits array - they will be deserialized lazily
							reader.Skip();
							break;
						default:
							reader.Skip();
							break;
					}
				}
			}
			else
				reader.Skip();

			return new TopHitsAggregate
			{
				Total = total,
				MaxScore = maxScore,
				Meta = meta
			};
		}

		private IAggregate GetMatrixStatsAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta, long? docCount = null)
		{
			var fields = JsonSerializer.Deserialize<List<MatrixStatsField>>(ref reader, options);
			return new MatrixStatsAggregate
			{
				DocCount = docCount.GetValueOrDefault(),
				Fields = fields,
				Meta = meta
			};
		}

		private IAggregate GetGeoLineAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options, IReadOnlyDictionary<string, object> meta)
		{
			var geoLine = new GeoLineAggregate { Meta = meta };

			if (reader.TokenType == JsonTokenType.Null)
				return geoLine;

			// "min" is actually the "type" field for geo_line, read string
			geoLine.Type = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
			if (reader.TokenType != JsonTokenType.String)
				reader.Skip();

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				switch (prop)
				{
					case "geometry":
						geoLine.Geometry = JsonSerializer.Deserialize<LineStringGeoShape>(ref reader, options);
						break;
					case "properties":
						geoLine.Properties = JsonSerializer.Deserialize<GeoLineProperties>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return geoLine;
		}

		private IAggregate GetMultiBucketAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options,
			string initialProperty, IReadOnlyDictionary<string, object> meta)
		{
			var bucket = new BucketAggregate { Meta = meta };

			var propertyName = initialProperty;

			if (propertyName == "doc_count_error_upper_bound")
			{
				bucket.DocCountErrorUpperBound = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
				if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
					return bucket;
				propertyName = reader.GetString();
				reader.Read();
			}

			if (propertyName == "sum_other_doc_count")
			{
				bucket.SumOtherDocCount = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
				if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
					return bucket;
				propertyName = reader.GetString();
				reader.Read(); // Move to value of "buckets"
			}

			// Now we should be at the "buckets" value
			if (propertyName != "buckets")
			{
				reader.Skip();
				return bucket;
			}

			var items = new List<IBucket>();
			bucket.Items = items;

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				// Named buckets (filters aggregation with named filters)
				var filterAggregates = new Dictionary<string, IAggregate>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					var name = reader.GetString();
					reader.Read();
					var innerAgg = ReadAggregate(ref reader, options);
					filterAggregates[name] = innerAgg;
				}
				return new FiltersAggregate(filterAggregates) { Meta = meta };
			}

			if (reader.TokenType == JsonTokenType.StartArray)
			{
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					var item = ReadBucket(ref reader, options);
					if (item != null)
						items.Add(item);
				}
			}
			else
			{
				reader.Skip();
			}

			// Check for additional properties after buckets (e.g., "interval")
			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				if (prop == "interval")
				{
					// AutoInterval for auto_date_histogram
					bucket.AutoInterval = JsonSerializer.Deserialize<DateMathTime>(ref reader, options);
				}
				else
					reader.Skip();
			}

			return bucket;
		}

		private IBucket ReadBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
				return null;

			reader.Read(); // Move past StartObject
			if (reader.TokenType == JsonTokenType.EndObject)
				return null;

			var property = reader.GetString();
			reader.Read(); // Move to value

			switch (property)
			{
				case "key":
					return GetKeyedBucket(ref reader, options);
				case "from":
				case "to":
					return GetRangeBucket(ref reader, options, null, property);
				case "key_as_string":
					return GetDateHistogramBucket(ref reader, options);
				case "doc_count":
					return GetFiltersBucket(ref reader, options);
				case "min":
					return GetVariableWidthHistogramBucket(ref reader, options);
				default:
					// Skip unknown bucket shapes
					SkipToEndOfCurrentObject(ref reader);
					return null;
			}
		}

		private IBucket GetKeyedBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			// "key" value - could be string, number, array, or object (composite)
			if (reader.TokenType == JsonTokenType.StartObject)
				return GetCompositeBucket(ref reader, options);

			object key;
			if (reader.TokenType == JsonTokenType.String)
				key = reader.GetString();
			else if (reader.TokenType == JsonTokenType.StartArray)
			{
				// multi-terms bucket
				var keys = new List<object>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					if (reader.TokenType == JsonTokenType.String)
						keys.Add(reader.GetString());
					else if (reader.TokenType == JsonTokenType.Number)
					{
						if (reader.TryGetInt64(out var l)) keys.Add(l);
						else keys.Add(reader.GetDouble());
					}
					else reader.Skip();
				}
				key = keys;
			}
			else if (reader.TokenType == JsonTokenType.Number)
			{
				if (reader.TryGetInt64(out var l)) key = l;
				else key = reader.GetDouble();
			}
			else
			{
				key = null;
				reader.Skip();
			}

			// Read next property
			if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
				return new KeyedBucket<object>(null) { Key = key };

			var propertyName = reader.GetString();
			reader.Read();

			// Check if this is a range bucket
			if (propertyName == "from" || propertyName == "to")
			{
				var rangeKey = key is double d ? d.ToString("#.#") : key?.ToString();
				return GetRangeBucket(ref reader, options, rangeKey, propertyName);
			}

			string keyAsString = null;
			if (propertyName == "key_as_string")
			{
				keyAsString = reader.GetString();
				reader.Read(); // next property name
				propertyName = reader.GetString();
				reader.Read(); // "doc_count" value
			}

			// Should be at doc_count now
			long docCount = 0;
			if (propertyName == "doc_count")
				docCount = reader.TokenType == JsonTokenType.Null ? 0 : reader.GetInt64();

			Dictionary<string, IAggregate> subAggregates = null;
			long? docCountErrorUpperBound = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();

				switch (prop)
				{
					case "score":
						return GetSignificantTermsBucket(ref reader, options, key, docCount);
					case "doc_count_error_upper_bound":
						docCountErrorUpperBound = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
						break;
					default:
						if (subAggregates == null)
							subAggregates = new Dictionary<string, IAggregate>();
						subAggregates[prop] = ReadAggregate(ref reader, options);
						break;
				}
			}

			return new KeyedBucket<object>(subAggregates)
			{
				Key = key,
				KeyAsString = keyAsString,
				DocCount = docCount,
				DocCountErrorUpperBound = docCountErrorUpperBound
			};
		}

		private IBucket GetCompositeBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			var keyDict = JsonSerializer.Deserialize<IReadOnlyDictionary<string, object>>(ref reader, options);
			var key = new CompositeKey(keyDict ?? new Dictionary<string, object>());
			long? docCount = null;
			Dictionary<string, IAggregate> nestedAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				if (prop == "doc_count")
					docCount = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
				else
				{
					if (nestedAggregates == null)
						nestedAggregates = new Dictionary<string, IAggregate>();
					nestedAggregates[prop] = ReadAggregate(ref reader, options);
				}
			}

			return new CompositeBucket(nestedAggregates, key) { DocCount = docCount };
		}

		private IBucket GetRangeBucket(ref Utf8JsonReader reader, JsonSerializerOptions options, string key, string initialProperty)
		{
			string fromAsString = null, fromString = null;
			string toAsString = null, toString = null;
			long? docCount = null;
			double? toDouble = null, fromDouble = null;
			Dictionary<string, IAggregate> subAggregates = null;

			var propertyName = initialProperty;
			var reading = true;
			while (reading)
			{
				switch (propertyName)
				{
					case "from":
						if (reader.TokenType == JsonTokenType.Number)
							fromDouble = reader.GetDouble();
						else if (reader.TokenType == JsonTokenType.String)
							fromString = reader.GetString();
						else reader.Skip();
						break;
					case "to":
						if (reader.TokenType == JsonTokenType.Number)
							toDouble = reader.GetDouble();
						else if (reader.TokenType == JsonTokenType.String)
							toString = reader.GetString();
						else reader.Skip();
						break;
					case "key":
						key = reader.GetString();
						break;
					case "from_as_string":
						fromAsString = reader.GetString();
						break;
					case "to_as_string":
						toAsString = reader.GetString();
						break;
					case "doc_count":
						docCount = reader.TokenType == JsonTokenType.Null ? 0 : reader.GetInt64();
						break;
					default:
						// This is a sub-aggregation property
						if (subAggregates == null)
							subAggregates = new Dictionary<string, IAggregate>();
						subAggregates[propertyName] = ReadAggregate(ref reader, options);
						// Continue reading more sub-aggregates
						while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
						{
							var subProp = reader.GetString();
							reader.Read();
							subAggregates[subProp] = ReadAggregate(ref reader, options);
						}
						reading = false;
						continue;
				}

				if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
					break;
				if (reader.TokenType != JsonTokenType.PropertyName)
					break;
				propertyName = reader.GetString();
				reader.Read();
			}

			if (fromString != null || toString != null)
				return new IpRangeBucket(subAggregates)
				{
					Key = key,
					DocCount = docCount.GetValueOrDefault(),
					From = fromString,
					To = toString,
				};

			return new RangeBucket(subAggregates)
			{
				Key = key,
				From = fromDouble,
				To = toDouble,
				DocCount = docCount.GetValueOrDefault(),
				FromAsString = fromAsString,
				ToAsString = toAsString,
			};
		}

		private IBucket GetDateHistogramBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			// First property was "key_as_string", value is the current token
			var keyAsString = reader.GetString();

			// Read remaining properties
			double key = 0;
			long docCount = 0;
			Dictionary<string, IAggregate> subAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				switch (prop)
				{
					case "key":
						key = reader.GetDouble();
						break;
					case "doc_count":
						docCount = reader.GetInt64();
						break;
					default:
						if (subAggregates == null)
							subAggregates = new Dictionary<string, IAggregate>();
						subAggregates[prop] = ReadAggregate(ref reader, options);
						break;
				}
			}

			return new DateHistogramBucket(subAggregates)
			{
				Key = key,
				KeyAsString = keyAsString,
				DocCount = docCount,
			};
		}

		private IBucket GetVariableWidthHistogramBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			// First property was "min", value is the current token
			var min = reader.GetDouble();
			double key = 0, max = 0;
			long docCount = 0;
			Dictionary<string, IAggregate> subAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				switch (prop)
				{
					case "key": key = reader.GetDouble(); break;
					case "max": max = reader.GetDouble(); break;
					case "doc_count": docCount = reader.GetInt64(); break;
					default:
						if (subAggregates == null) subAggregates = new Dictionary<string, IAggregate>();
						subAggregates[prop] = ReadAggregate(ref reader, options);
						break;
				}
			}

			return new VariableWidthHistogramBucket(subAggregates)
			{
				Key = key,
				Minimum = min,
				Maximum = max,
				DocCount = docCount,
			};
		}

		private IBucket GetSignificantTermsBucket(ref Utf8JsonReader reader, JsonSerializerOptions options, object key, long docCount)
		{
			// Current value is the "score" value
			var score = reader.GetDouble();
			long bgCount = 0;
			Dictionary<string, IAggregate> subAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				switch (prop)
				{
					case "bg_count":
						bgCount = reader.GetInt64();
						break;
					default:
						if (subAggregates == null) subAggregates = new Dictionary<string, IAggregate>();
						subAggregates[prop] = ReadAggregate(ref reader, options);
						break;
				}
			}

			return new SignificantTermsBucket<object>(subAggregates)
			{
				Key = key,
				DocCount = docCount,
				BgCount = bgCount,
				Score = score
			};
		}

		private IBucket GetFiltersBucket(ref Utf8JsonReader reader, JsonSerializerOptions options)
		{
			// First property was "doc_count", value is current token
			var docCount = reader.TokenType == JsonTokenType.Null ? 0L : reader.GetInt64();
			Dictionary<string, IAggregate> subAggregates = null;

			while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
			{
				var prop = reader.GetString();
				reader.Read();
				if (subAggregates == null)
					subAggregates = new Dictionary<string, IAggregate>();
				subAggregates[prop] = ReadAggregate(ref reader, options);
			}

			return new FiltersBucketItem(subAggregates ?? EmptyReadOnly<string, IAggregate>.Dictionary)
			{
				DocCount = docCount
			};
		}

		private static void SkipToEndOfCurrentObject(ref Utf8JsonReader reader)
		{
			int depth = 1;
			while (depth > 0 && reader.Read())
			{
				if (reader.TokenType == JsonTokenType.StartObject) depth++;
				else if (reader.TokenType == JsonTokenType.EndObject) depth--;
			}
		}
	}
}
