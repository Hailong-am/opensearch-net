// SPDX-License-Identifier: Apache-2.0
//
// Bridge shim for DynamicObjectResolver when USE_STJ_BRIDGE is active.
// Replaces IL-emitted POCO formatters with STJ JsonSerializer-backed formatters.
// All existing call sites (DynamicObjectResolver.ExcludeNullCamelCase.GetFormatter<T>() etc.)
// compile unchanged -- only the implementation changes.

#if USE_STJ_BRIDGE

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;

namespace OpenSearch.Net.Utf8Json
{
	/// <summary>
	/// Bridge replacement for DynamicObjectResolver.
	/// Uses System.Text.Json for POCO serialization instead of IL emit.
	/// </summary>
	internal static class DynamicObjectResolver
	{
		public static readonly IJsonFormatterResolver Default = new StjBackedResolver(new JsonSerializerOptions
		{
			PropertyNamingPolicy = null, // Original case
			DefaultIgnoreCondition = JsonIgnoreCondition.Never
		});

		public static readonly IJsonFormatterResolver ExcludeNullCamelCase = new StjBackedResolver(new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		});

		public static readonly IJsonFormatterResolver AllowPrivateExcludeNullCamelCase = new StjBackedResolver(new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			IncludeFields = true
		});

		/// <summary>
		/// Create a custom resolver (replaces the propertyMapper + mutator + excludeNull signature).
		/// In bridge mode, propertyMapper is ignored (STJ uses attributes instead).
		/// </summary>
		public static IJsonFormatterResolver Create(
			Func<MemberInfo, JsonProperty> propertyMapper,
			Lazy<Func<string, string>> mutator,
			bool excludeNull)
		{
			return new StjBackedResolver(new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = excludeNull
					? JsonIgnoreCondition.WhenWritingNull
					: JsonIgnoreCondition.Never
			});
		}

		private sealed class StjBackedResolver : IJsonFormatterResolver
		{
			private readonly JsonSerializerOptions _options;

			public StjBackedResolver(JsonSerializerOptions options)
			{
				_options = options;
			}

			public IJsonFormatter<T> GetFormatter<T>()
			{
				return new StjPocoFormatter<T>(_options);
			}
		}
	}

	/// <summary>
	/// Generic POCO formatter that delegates to STJ JsonSerializer.
	/// Used as fallback when no specialized IJsonFormatter&lt;T&gt; is registered.
	/// </summary>
	internal sealed class StjPocoFormatter<T> : IJsonFormatter<T>
	{
		private readonly JsonSerializerOptions _options;

		public StjPocoFormatter(JsonSerializerOptions options)
		{
			_options = options;
		}

		public void Serialize(ref JsonWriter writer, T value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}
			// Serialize via STJ to bytes, then inject as raw
			var json = System.Text.Json.JsonSerializer.Serialize(value, _options);
			var bytes = Encoding.UTF8.GetBytes(json);
			writer.WriteRaw(bytes);
		}

		public T Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			// Read the next block as raw bytes, then deserialize via STJ
			var segment = reader.ReadNextBlockSegment();
			if (segment.Count == 0) return default;
			var json = Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
			return System.Text.Json.JsonSerializer.Deserialize<T>(json, _options);
		}
	}

	/// <summary>
	/// Bridge replacement for DynamicObjectTypeFallbackFormatter.
	/// Handles untyped object serialization via STJ.
	/// </summary>
	internal sealed class DynamicObjectTypeFallbackFormatter : IJsonFormatter<object>
	{
		private readonly IJsonFormatterResolver _resolver;

		public DynamicObjectTypeFallbackFormatter(IJsonFormatterResolver resolver)
		{
			_resolver = resolver;
		}

		public void Serialize(ref JsonWriter writer, object value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}
			var json = System.Text.Json.JsonSerializer.Serialize(value, new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
			});
			var bytes = Encoding.UTF8.GetBytes(json);
			writer.WriteRaw(bytes);
		}

		public object Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var segment = reader.ReadNextBlockSegment();
			if (segment.Count == 0) return null;
			var json = Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
			return System.Text.Json.JsonSerializer.Deserialize<object>(json);
		}
	}
}

#endif
