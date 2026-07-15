/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> that adds the field-name wrapping to
	/// field-name queries (term, match, prefix, wildcard, fuzzy, regexp, span_term,
	/// intervals, knn, neural, terms_set, etc.).
	/// <para>
	/// The OpenSearch JSON shape for these queries is:
	/// <code>{ "&lt;resolved-field&gt;": { ...body... } }</code>
	/// The body itself is produced by the standard <see cref="InterfaceDataContractModifier"/>
	/// (the <c>Field</c> property is <c>[IgnoreDataMember]</c> so it is excluded from the body);
	/// this converter only adds the outer <c>{ field: {body} }</c> wrapping.
	/// </para>
	/// <para>
	/// This is the System.Text.Json equivalent of the Utf8Json <c>FieldNameQueryFormatter</c>.
	/// It targets the query <em>interfaces</em> (e.g. <see cref="ITermQuery"/>), because
	/// System.Text.Json resolves the contract from the static (interface) type used by
	/// <see cref="QueryContainerConverter"/> and never dispatches to a concrete-type converter
	/// in that scenario. Interfaces that already carry a dedicated <see cref="JsonConverterAttribute"/>
	/// (range, terms, geo_shape, geo_distance, etc.) are excluded so their converters keep precedence.
	/// </para>
	/// </summary>
	internal sealed class FieldNameQueryConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, Type> ConcreteTypeCache = new();

		private readonly IConnectionSettingsValues _settings;

		public FieldNameQueryConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => ResolveConcreteType(typeToConvert) != null;

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var concreteType = ResolveConcreteType(typeToConvert);
			var converterType = typeof(FieldNameQueryConverter<,>).MakeGenericType(typeToConvert, concreteType);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}

		/// <summary>
		/// Resolves the concrete implementation type for a field-name query interface, or
		/// <c>null</c> if <paramref name="typeToConvert"/> is not a field-name query interface
		/// this factory should handle.
		/// </summary>
		private static Type ResolveConcreteType(Type typeToConvert) =>
			ConcreteTypeCache.GetOrAdd(typeToConvert, static type =>
			{
				// Only handle interfaces — see class remarks. Concrete types are (de)serialized
				// directly by the dedicated converters or the InterfaceDataContractModifier.
				if (!type.IsInterface)
					return null;

				if (!typeof(IFieldNameQuery).IsAssignableFrom(type) || type == typeof(IFieldNameQuery))
					return null;

				// Interfaces with a dedicated converter (range, terms, geo_shape, geo_distance,
				// geo_polygon, geo_bounding_box, distance_feature, rank_feature, shape) already
				// handle field-name wrapping themselves.
				if (type.GetCustomAttribute<JsonConverterAttribute>() != null)
					return null;

				// Convention: interface "IXxxQuery" maps to concrete "XxxQuery" in the same namespace.
				var name = type.Name;
				if (name.Length < 2 || name[0] != 'I')
					return null;

				var concreteName = (type.Namespace == null ? "" : type.Namespace + ".") + name.Substring(1);
				var concrete = type.Assembly.GetType(concreteName);

				if (concrete == null || concrete.IsAbstract || concrete.IsInterface)
					return null;

				if (!type.IsAssignableFrom(concrete))
					return null;

				return concrete;
			});
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for a field-name query interface <typeparamref name="TInterface"/>
	/// backed by the concrete implementation <typeparamref name="TConcrete"/>.
	/// Handles serialization (adding the <c>{ field: {body} }</c> wrapping) and deserialization
	/// (reading the field-name property and the body, including scalar shorthand forms).
	/// </summary>
	internal sealed class FieldNameQueryConverter<TInterface, TConcrete> : JsonConverter<TInterface>
		where TInterface : class, IFieldNameQuery
		where TConcrete : class, TInterface, new()
	{
		// Cache of "body" options (a clone of the original options without the
		// FieldNameQueryConverterFactory), keyed by the original options instance,
		// so the body can be (de)serialized without re-entering this converter.
		private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> BodyOptionsCache = new();

		private readonly IConnectionSettingsValues _settings;

		public FieldNameQueryConverter(IConnectionSettingsValues settings) => _settings = settings;

		private static JsonSerializerOptions GetBodyOptions(JsonSerializerOptions options) =>
			BodyOptionsCache.GetValue(options, static o =>
			{
				var clone = new JsonSerializerOptions(o);
				for (var i = clone.Converters.Count - 1; i >= 0; i--)
				{
					if (clone.Converters[i] is FieldNameQueryConverterFactory)
						clone.Converters.RemoveAt(i);
				}
				return clone;
			});

		public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var field = value.Field;
			var resolvedField = field == null ? null : _settings.Inferrer.Field(field);
			if (string.IsNullOrEmpty(resolvedField))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(resolvedField);

			// Serialize the body using the actual runtime type so that all body properties are
			// emitted (works for both concrete queries and descriptors), but with the body options
			// that exclude this converter (avoids recursion).
			JsonSerializer.Serialize(writer, value, value.GetType(), GetBodyOptions(options));

			writer.WriteEndObject();
		}

		public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			reader.Read(); // move to first token inside the object

			if (reader.TokenType == JsonTokenType.EndObject)
				return null;

			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				reader.Skip();
				return null;
			}

			var fieldName = reader.GetString();
			reader.Read(); // move to the value

			TConcrete query;

			if (reader.TokenType == JsonTokenType.StartObject)
			{
				query = JsonSerializer.Deserialize<TConcrete>(ref reader, GetBodyOptions(options));
			}
			else if (reader.TokenType == JsonTokenType.Null)
			{
				query = null;
			}
			else
			{
				// Scalar shorthand: the body is a bare scalar rather than an object.
				query = new TConcrete();
				AssignScalarShorthand(query, ref reader);
			}

			// Consume the closing EndObject of the outer wrapper.
			reader.Read();

			if (query == null)
				return null;

			query.Field = fieldName;
			return query;
		}

		private static void AssignScalarShorthand(TConcrete query, ref Utf8JsonReader reader)
		{
			object scalar = reader.TokenType switch
			{
				JsonTokenType.String => reader.GetString(),
				JsonTokenType.Number => reader.TryGetInt64(out var l) ? l : reader.GetDouble(),
				JsonTokenType.True => true,
				JsonTokenType.False => false,
				_ => null
			};

			switch (query)
			{
				case ITermQuery termQuery:
					termQuery.Value = scalar;
					break;
				case IMatchQuery matchQuery:
					matchQuery.Query = scalar?.ToString();
					break;
				case IMatchPhraseQuery matchPhraseQuery:
					matchPhraseQuery.Query = scalar?.ToString();
					break;
				case IMatchPhrasePrefixQuery matchPhrasePrefixQuery:
					matchPhrasePrefixQuery.Query = scalar?.ToString();
					break;
				case IMatchBoolPrefixQuery matchBoolPrefixQuery:
					matchBoolPrefixQuery.Query = scalar?.ToString();
					break;
			}
		}
	}
}
