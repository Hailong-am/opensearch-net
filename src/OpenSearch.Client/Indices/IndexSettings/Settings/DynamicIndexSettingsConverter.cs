/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using static OpenSearch.Client.FixedIndexSettings;
using static OpenSearch.Client.IndexSortSettings;
using static OpenSearch.Client.UpdatableIndexSettings;

namespace OpenSearch.Client
{
	/// <summary>
	/// Handles serialization/deserialization of <see cref="IDynamicIndexSettings"/>.
	/// Typed properties are flushed into the backing dictionary before the dictionary is serialized.
	/// </summary>
	internal class DynamicIndexSettingsConverter : JsonConverter<IDynamicIndexSettings>
	{
		public override bool CanConvert(Type typeToConvert) =>
			typeof(IDynamicIndexSettings).IsAssignableFrom(typeToConvert)
			&& !typeof(IIndexSettings).IsAssignableFrom(typeToConvert);

		public override IDynamicIndexSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var indexSettings = new IndexSettings();
			ReadKnownSettings(ref reader, indexSettings, options);
			return indexSettings;
		}

		public override void Write(Utf8JsonWriter writer, IDynamicIndexSettings value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			IDictionary<string, object> d = value;

			FlushDynamicProperties(value, d);

			WriteDictionary(writer, d, options);
		}

		/// <summary>
		/// Flushes the typed dynamic index setting properties into the backing dictionary.
		/// </summary>
		internal static void FlushDynamicProperties(IDynamicIndexSettings value, IDictionary<string, object> d)
		{
			Set(d, NumberOfReplicas, value.NumberOfReplicas);
			Set(d, RefreshInterval, value.RefreshInterval);
			Set(d, DefaultPipeline, value.DefaultPipeline);
			Set(d, FinalPipeline, value.FinalPipeline);
			Set(d, BlocksReadOnly, value.BlocksReadOnly);
			Set(d, BlocksRead, value.BlocksRead);
			Set(d, BlocksWrite, value.BlocksWrite);
			Set(d, BlocksMetadata, value.BlocksMetadata);
			Set(d, BlocksReadOnlyAllowDelete, value.BlocksReadOnlyAllowDelete);
			Set(d, Priority, value.Priority);
			Set(d, UpdatableIndexSettings.AutoExpandReplicas, value.AutoExpandReplicas);
			Set(d, UpdatableIndexSettings.RecoveryInitialShards, value.RecoveryInitialShards);
			Set(d, RequestsCacheEnable, value.RequestsCacheEnabled);
			Set(d, RoutingAllocationTotalShardsPerNode, value.RoutingAllocationTotalShardsPerNode);
			Set(d, UnassignedNodeLeftDelayedTimeout, value.UnassignedNodeLeftDelayedTimeout);

			var translog = value.Translog;
			Set(d, TranslogSyncInterval, translog?.SyncInterval);
			Set(d, UpdatableIndexSettings.TranslogDurability, translog?.Durability);

			var flush = value.Translog?.Flush;
			Set(d, TranslogFlushThresholdSize, flush?.ThresholdSize);
			Set(d, TranslogFlushThresholdPeriod, flush?.ThresholdPeriod);

			Set(d, MergePolicyExpungeDeletesAllowed, value.Merge?.Policy?.ExpungeDeletesAllowed);
			Set(d, MergePolicyFloorSegment, value.Merge?.Policy?.FloorSegment);
			Set(d, MergePolicyMaxMergeAtOnce, value.Merge?.Policy?.MaxMergeAtOnce);
			Set(d, MergePolicyMaxMergeAtOnceExplicit, value.Merge?.Policy?.MaxMergeAtOnceExplicit);
			Set(d, MergePolicyMaxMergedSegment, value.Merge?.Policy?.MaxMergedSegment);
			Set(d, MergePolicySegmentsPerTier, value.Merge?.Policy?.SegmentsPerTier);
			Set(d, MergePolicyReclaimDeletesWeight, value.Merge?.Policy?.ReclaimDeletesWeight);

			Set(d, MergeSchedulerMaxThreadCount, value.Merge?.Scheduler?.MaxThreadCount);
			Set(d, MergeSchedulerAutoThrottle, value.Merge?.Scheduler?.AutoThrottle);

			var log = value.SlowLog;
			var search = log?.Search;
			var indexing = log?.Indexing;

			Set(d, SlowlogSearchThresholdQueryWarn, search?.Query?.ThresholdWarn);
			Set(d, SlowlogSearchThresholdQueryInfo, search?.Query?.ThresholdInfo);
			Set(d, SlowlogSearchThresholdQueryDebug, search?.Query?.ThresholdDebug);
			Set(d, SlowlogSearchThresholdQueryTrace, search?.Query?.ThresholdTrace);

			Set(d, SlowlogSearchThresholdFetchWarn, search?.Fetch?.ThresholdWarn);
			Set(d, SlowlogSearchThresholdFetchInfo, search?.Fetch?.ThresholdInfo);
			Set(d, SlowlogSearchThresholdFetchDebug, search?.Fetch?.ThresholdDebug);
			Set(d, SlowlogSearchThresholdFetchTrace, search?.Fetch?.ThresholdTrace);
			Set(d, SlowlogSearchLevel, search?.LogLevel);

			Set(d, SlowlogIndexingThresholdFetchWarn, indexing?.ThresholdWarn);
			Set(d, SlowlogIndexingThresholdFetchInfo, indexing?.ThresholdInfo);
			Set(d, SlowlogIndexingThresholdFetchDebug, indexing?.ThresholdDebug);
			Set(d, SlowlogIndexingThresholdFetchTrace, indexing?.ThresholdTrace);
			Set(d, SlowlogIndexingLevel, indexing?.LogLevel);
			Set(d, SlowlogIndexingSource, indexing?.Source);

			Set(d, UpdatableIndexSettings.Analysis, value.Analysis);
			Set(d, Similarity, value.Similarity);
		}

		/// <summary>
		/// Flushes the typed fixed index setting properties into the backing dictionary.
		/// </summary>
		internal static void FlushFixedProperties(IIndexSettings indexSettings, IDictionary<string, object> d)
		{
			Set(d, StoreType, indexSettings.FileSystemStorageImplementation);
			Set(d, QueriesCacheEnabled, indexSettings.Queries?.Cache?.Enabled);
			Set(d, NumberOfShards, indexSettings.NumberOfShards);
			Set(d, NumberOfRoutingShards, indexSettings.NumberOfRoutingShards);
			Set(d, RoutingPartitionSize, indexSettings.RoutingPartitionSize);
			Set(d, Hidden, indexSettings.Hidden);

			if (indexSettings.SoftDeletes != null)
			{
				Set(d, SoftDeletesRetentionOperations, indexSettings.SoftDeletes.Retention?.Operations);
			}

			if (indexSettings.Sorting != null)
			{
				Set(d, IndexSortSettings.Fields, AsArrayOrSingleItem(indexSettings.Sorting.Fields));
				Set(d, Order, AsArrayOrSingleItem(indexSettings.Sorting.Order));
				Set(d, Mode, AsArrayOrSingleItem(indexSettings.Sorting.Mode));
				Set(d, IndexSortSettings.Missing, AsArrayOrSingleItem(indexSettings.Sorting.Missing));
			}
		}

		/// <summary>
		/// Writes the dictionary as a JSON object, skipping null values (except RefreshInterval).
		/// </summary>
		internal static void WriteDictionary(Utf8JsonWriter writer, IDictionary<string, object> d, JsonSerializerOptions options)
		{
			writer.WriteStartObject();

			foreach (var kvp in d)
			{
				// Skip null values except for RefreshInterval (which can legitimately be -1 serialized as a string)
				if (kvp.Value == null && kvp.Key != RefreshInterval)
					continue;

				writer.WritePropertyName(kvp.Key);
				JsonSerializer.Serialize(writer, kvp.Value, kvp.Value?.GetType() ?? typeof(object), options);
			}

			writer.WriteEndObject();
		}

		/// <summary>
		/// Reads a JSON object into an <see cref="IIndexSettings"/> instance, extracting known
		/// settings into typed properties and placing the rest in the backing dictionary.
		/// </summary>
		internal static void ReadKnownSettings(ref Utf8JsonReader reader, IIndexSettings s, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			// Read as a raw dictionary first, then flatten and extract known keys
			var raw = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
			if (raw == null) return;

			var settings = Flatten(raw);
			IDictionary<string, object> dict = s;

			// Extract known dynamic index settings
			SetValue<int?>(settings, NumberOfReplicas, v => s.NumberOfReplicas = v);
			SetValue<AutoExpandReplicas>(settings, UpdatableIndexSettings.AutoExpandReplicas, v => s.AutoExpandReplicas = v, options);
			SetValue<Time>(settings, RefreshInterval, v => s.RefreshInterval = v, options);
			SetValue<string>(settings, DefaultPipeline, v => s.DefaultPipeline = v);
			SetValue<string>(settings, FinalPipeline, v => s.FinalPipeline = v);
			SetValue<bool?>(settings, BlocksReadOnly, v => s.BlocksReadOnly = v);
			SetValue<bool?>(settings, BlocksRead, v => s.BlocksRead = v);
			SetValue<bool?>(settings, BlocksWrite, v => s.BlocksWrite = v);
			SetValue<bool?>(settings, BlocksMetadata, v => s.BlocksMetadata = v);
			SetValue<bool?>(settings, BlocksReadOnlyAllowDelete, v => s.BlocksReadOnlyAllowDelete = v);
			SetValue<int?>(settings, Priority, v => s.Priority = v);
			SetValue<bool?>(settings, RequestsCacheEnable, v => s.RequestsCacheEnabled = v);
			SetValue<int?>(settings, RoutingAllocationTotalShardsPerNode, v => s.RoutingAllocationTotalShardsPerNode = v);

			// Fixed index settings
			SetValue<int?>(settings, NumberOfShards, v => s.NumberOfShards = v);
			SetValue<int?>(settings, NumberOfRoutingShards, v => s.NumberOfRoutingShards = v);
			SetValue<int?>(settings, RoutingPartitionSize, v => s.RoutingPartitionSize = v);
			SetValue<bool?>(settings, Hidden, v => s.Hidden = v);
			SetValue<FileSystemStorageImplementation?>(settings, StoreType, v => s.FileSystemStorageImplementation = v, options);

			// Reconstruct the nested settings objects from their flattened dotted keys, mirroring the pre-STJ
			// IndexSettingsFormatter. The typed extraction is additive (SetValue keeps the entry in the flat
			// dictionary), so the raw keys still land in the backing dictionary below.
			var queries = new QueriesSettings();
			var queriesCache = new QueriesCacheSettings();
			var queriesCacheSet = false;
			SetValue<bool?>(settings, QueriesCacheEnabled, v => { queriesCache.Enabled = v; queriesCacheSet = true; });
			if (queriesCacheSet)
			{
				queries.Cache = queriesCache;
				s.Queries = queries;
			}

			var softDeletesRetention = new SoftDeleteRetentionSettings();
			var softDeletesSet = false;
			SetValue<long?>(settings, SoftDeletesEnabled, v => { softDeletesRetention.Operations = v; softDeletesSet = true; });
			if (softDeletesSet)
				s.SoftDeletes = new SoftDeleteSettings { Retention = softDeletesRetention };

			// Extract the analysis and similarity sub-objects into their typed properties. These are left
			// un-flattened by Flatten (see below) so they arrive as nested objects under either the bare or
			// the "index."-prefixed key. Remaining entries fall through to the backing dictionary.
			foreach (var kv in settings)
			{
				switch (kv.Key)
				{
					case UpdatableIndexSettings.Analysis:
					case "index.analysis":
						s.Analysis = ReserializeAndDeserialize<IAnalysis>(kv.Value, options);
						break;
					case Similarity:
					case "index.similarity":
						s.Similarity = ReserializeAndDeserialize<ISimilarities>(kv.Value, options);
						break;
					default:
						dict[kv.Key] = kv.Value;
						break;
				}
			}
		}

		/// <summary>
		/// Reserializes a value that was deserialized to a loosely-typed representation (e.g. a nested
		/// <see cref="Dictionary{TKey,TValue}"/> or <see cref="JsonElement"/>) and deserializes it into the
		/// strongly-typed <typeparamref name="T"/> using the same options. Mirrors the Utf8Json
		/// roundtrip used by the original IndexSettings formatter for the analysis/similarity sub-objects.
		/// </summary>
		private static T ReserializeAndDeserialize<T>(object value, JsonSerializerOptions options)
		{
			if (value == null)
				return default;

			var json = JsonSerializer.Serialize(value, value.GetType(), options);
			return JsonSerializer.Deserialize<T>(json, options);
		}

		private static void Set(IDictionary<string, object> d, string key, object value)
		{
			if (value != null) d[key] = value;
		}

		private static object AsArrayOrSingleItem<T>(IEnumerable<T> items)
		{
			if (items == null || !items.Any())
				return null;

			if (items.Count() == 1)
				return items.First();

			return items;
		}

		private static Dictionary<string, object> Flatten(Dictionary<string, object> original, string prefix = "",
			Dictionary<string, object> current = null)
		{
			current ??= new Dictionary<string, object>();
			foreach (var property in original)
			{
				// The analysis and similarity sub-objects must stay intact (they are extracted into typed
				// properties later); everything else is flattened into dotted keys (e.g. index.number_of_shards).
				var isPreserved = property.Key == UpdatableIndexSettings.Analysis
					|| property.Key == Similarity
					|| property.Key.EndsWith(".analysis")
					|| property.Key.EndsWith(".similarity");

				// The value may arrive either as a nested Dictionary<string, object> (from the base
				// DynamicDictionary/object handling) or as a JsonElement object, depending on the path.
				if (!isPreserved && TryGetNestedObject(property.Value, out var nested))
				{
					Flatten(nested, prefix + property.Key + ".", current);
				}
				else
				{
					current[prefix + property.Key] = property.Value;
				}
			}
			return current;
		}

		/// <summary>
		/// Extracts a nested JSON object as a <see cref="Dictionary{TKey,TValue}"/> when the value is either a
		/// <see cref="Dictionary{TKey,TValue}"/> (from the base object/DynamicDictionary handling) or a
		/// <see cref="JsonElement"/> of kind <see cref="JsonValueKind.Object"/>. Returns false otherwise.
		/// </summary>
		private static bool TryGetNestedObject(object value, out Dictionary<string, object> nested)
		{
			switch (value)
			{
				case Dictionary<string, object> dict:
					nested = dict;
					return true;
				case JsonElement element when element.ValueKind == JsonValueKind.Object:
					nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
					return nested != null;
				default:
					nested = null;
					return false;
			}
		}

		private static void SetValue<T>(Dictionary<string, object> settings, string key, Action<T> assign, JsonSerializerOptions settingsOptions = null)
		{
			if (!settings.TryGetValue(key, out var raw))
				return;

			try
			{
				T value;
				if (raw is JsonElement element)
				{
					value = element.Deserialize<T>();
				}
				else if (raw is T typed)
				{
					value = typed;
				}
				else if (raw != null && typeof(T) == typeof(int?))
				{
					value = (T)(object)Convert.ToInt32(raw);
				}
				else if (raw != null && typeof(T) == typeof(bool?))
				{
					value = (T)(object)Convert.ToBoolean(raw);
				}
				else if (raw != null && settingsOptions != null)
				{
					// Round-trip through the options so string/scalar wire values convert to a typed setting
					// via its registered converter (e.g. "0-all" -> AutoExpandReplicas, "1s" -> Time). STJ
					// cannot deserialize a bare string token to these types otherwise.
					var json = JsonSerializer.Serialize(raw, raw.GetType(), settingsOptions);
					value = JsonSerializer.Deserialize<T>(json, settingsOptions);
				}
				else
				{
					value = default;
				}

				assign(value);
				// Do NOT remove the key: the typed extraction is additive. The original wire entry must
				// remain in the flattened dictionary so it is copied into the settings backing dictionary
				// (IIsADictionary), matching the pre-STJ formatter (which both assigned the property and
				// kept the entry). Otherwise a template whose only setting is e.g. number_of_shards would
				// deserialize to an empty Settings dictionary.
			}
			catch
			{
				// If conversion fails, leave it in the dictionary
			}
		}
	}

	/// <summary>
	/// Handles serialization/deserialization of <see cref="IIndexSettings"/>.
	/// Delegates to <see cref="DynamicIndexSettingsConverter"/> for the shared properties,
	/// then adds the fixed index settings.
	/// </summary>
	internal class IndexSettingsConverter : JsonConverter<IIndexSettings>
	{
		public override IIndexSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var indexSettings = new IndexSettings();
			DynamicIndexSettingsConverter.ReadKnownSettings(ref reader, indexSettings, options);
			return indexSettings;
		}

		public override void Write(Utf8JsonWriter writer, IIndexSettings value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			IDictionary<string, object> d = value;

			// Flush dynamic index settings typed properties into the dictionary
			DynamicIndexSettingsConverter.FlushDynamicProperties(value, d);

			// Flush fixed index settings typed properties into the dictionary
			DynamicIndexSettingsConverter.FlushFixedProperties(value, d);

			// Write the full dictionary
			DynamicIndexSettingsConverter.WriteDictionary(writer, d, options);
		}
	}
}
