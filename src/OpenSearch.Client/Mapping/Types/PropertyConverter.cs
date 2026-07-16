/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IProperty"/> that handles
	/// the field-type discriminator pattern used by OpenSearch mappings.
	/// Each property is serialized as a JSON object with a <c>"type"</c> field
	/// that determines the concrete property type.
	/// </summary>
	internal sealed class PropertyConverter : JsonConverter<IProperty>
	{
		// Pre-computed property names for performance (Requirement 8.4)
		private static readonly JsonEncodedText TypeProp = JsonEncodedText.Encode("type");
		private static readonly JsonEncodedText PropertiesProp = JsonEncodedText.Encode("properties");

		/// <summary>
		/// Maps type string values from JSON to their corresponding concrete C# types.
		/// </summary>
		private static readonly Dictionary<string, Type> TypeMapping = new(StringComparer.OrdinalIgnoreCase)
		{
			["text"] = typeof(TextProperty),
			["keyword"] = typeof(KeywordProperty),
			["search_as_you_type"] = typeof(SearchAsYouTypeProperty),
			["float"] = typeof(NumberProperty),
			["double"] = typeof(NumberProperty),
			["byte"] = typeof(NumberProperty),
			["short"] = typeof(NumberProperty),
			["integer"] = typeof(NumberProperty),
			["long"] = typeof(NumberProperty),
			["scaled_float"] = typeof(NumberProperty),
			["half_float"] = typeof(NumberProperty),
			["date"] = typeof(DateProperty),
			["date_nanos"] = typeof(DateNanosProperty),
			["boolean"] = typeof(BooleanProperty),
			["binary"] = typeof(BinaryProperty),
			["object"] = typeof(ObjectProperty),
			["nested"] = typeof(NestedProperty),
			["ip"] = typeof(IpProperty),
			["geo_point"] = typeof(GeoPointProperty),
			["geo_shape"] = typeof(GeoShapeProperty),
			["completion"] = typeof(CompletionProperty),
			["token_count"] = typeof(TokenCountProperty),
			["murmur3"] = typeof(Murmur3HashProperty),
			["percolator"] = typeof(PercolatorProperty),
			["date_range"] = typeof(DateRangeProperty),
			["double_range"] = typeof(DoubleRangeProperty),
			["float_range"] = typeof(FloatRangeProperty),
			["integer_range"] = typeof(IntegerRangeProperty),
			["long_range"] = typeof(LongRangeProperty),
			["ip_range"] = typeof(IpRangeProperty),
			["join"] = typeof(JoinProperty),
			["alias"] = typeof(FieldAliasProperty),
			["rank_feature"] = typeof(RankFeatureProperty),
			["rank_features"] = typeof(RankFeaturesProperty),
			["knn_vector"] = typeof(KnnVectorProperty),
		};

		/// <summary>
		/// Set of numeric type strings that map to <see cref="NumberProperty"/> and need
		/// their <c>Type</c> property explicitly set to the original type string.
		/// </summary>
		private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			"float", "double", "byte", "short", "integer", "long", "scaled_float", "half_float"
		};

		public override IProperty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			// Buffer the entire JSON object so we can peek at the "type" property
			// and then deserialize the full object as the correct concrete type.
			using var document = JsonDocument.ParseValue(ref reader);
			var root = document.RootElement;

			string typeString = null;

			// Check for explicit "type" property
			if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
			{
				typeString = typeProp.GetString();
			}
			// If no "type" property but "properties" exists, infer as Object type
			else if (root.TryGetProperty("properties", out _))
			{
				typeString = "object";
			}

			// Determine concrete type from type string
			Type concreteType;
			if (typeString != null && TypeMapping.TryGetValue(typeString, out var mappedType))
			{
				concreteType = mappedType;
			}
			else
			{
				// Default to ObjectProperty for unknown or missing type
				concreteType = typeof(ObjectProperty);
			}

			// Deserialize the buffered JSON as the concrete type
			var rawJson = root.GetRawText();
			var property = (IProperty)JsonSerializer.Deserialize(rawJson, concreteType, options);

			// For NumberProperty, ensure the type string is preserved exactly
			// (e.g., "integer", "float", etc.) since multiple type strings map to NumberProperty
			if (property != null && typeString != null && NumericTypes.Contains(typeString))
			{
				property.Type = typeString;
			}

			return property;
		}

		public override void Write(Utf8JsonWriter writer, IProperty value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Serialize using the actual runtime type to ensure all properties are written
			// including the "type" discriminator property
			switch (value)
			{
				case ITextProperty textProperty:
					JsonSerializer.Serialize(writer, textProperty, options);
					break;
				case IKeywordProperty keywordProperty:
					JsonSerializer.Serialize(writer, keywordProperty, options);
					break;
				case INumberProperty numberProperty:
					JsonSerializer.Serialize(writer, numberProperty, options);
					break;
				case IDateProperty dateProperty:
					JsonSerializer.Serialize(writer, dateProperty, options);
					break;
				case IBooleanProperty booleanProperty:
					JsonSerializer.Serialize(writer, booleanProperty, options);
					break;
				case INestedProperty nestedProperty:
					JsonSerializer.Serialize(writer, nestedProperty, options);
					break;
				case IObjectProperty objectProperty:
					JsonSerializer.Serialize(writer, objectProperty, options);
					break;
				case ISearchAsYouTypeProperty searchAsYouTypeProperty:
					JsonSerializer.Serialize(writer, searchAsYouTypeProperty, options);
					break;
				case IDateNanosProperty dateNanosProperty:
					JsonSerializer.Serialize(writer, dateNanosProperty, options);
					break;
				case IBinaryProperty binaryProperty:
					JsonSerializer.Serialize(writer, binaryProperty, options);
					break;
				case IIpProperty ipProperty:
					JsonSerializer.Serialize(writer, ipProperty, options);
					break;
				case IGeoPointProperty geoPointProperty:
					JsonSerializer.Serialize(writer, geoPointProperty, options);
					break;
				case IGeoShapeProperty geoShapeProperty:
					JsonSerializer.Serialize(writer, geoShapeProperty, options);
					break;
				case ICompletionProperty completionProperty:
					JsonSerializer.Serialize(writer, completionProperty, options);
					break;
				case ITokenCountProperty tokenCountProperty:
					JsonSerializer.Serialize(writer, tokenCountProperty, options);
					break;
				case IMurmur3HashProperty murmur3HashProperty:
					JsonSerializer.Serialize(writer, murmur3HashProperty, options);
					break;
				case IPercolatorProperty percolatorProperty:
					JsonSerializer.Serialize(writer, percolatorProperty, options);
					break;
				case IDateRangeProperty dateRangeProperty:
					JsonSerializer.Serialize(writer, dateRangeProperty, options);
					break;
				case IDoubleRangeProperty doubleRangeProperty:
					JsonSerializer.Serialize(writer, doubleRangeProperty, options);
					break;
				case IFloatRangeProperty floatRangeProperty:
					JsonSerializer.Serialize(writer, floatRangeProperty, options);
					break;
				case IIntegerRangeProperty integerRangeProperty:
					JsonSerializer.Serialize(writer, integerRangeProperty, options);
					break;
				case ILongRangeProperty longRangeProperty:
					JsonSerializer.Serialize(writer, longRangeProperty, options);
					break;
				case IIpRangeProperty ipRangeProperty:
					JsonSerializer.Serialize(writer, ipRangeProperty, options);
					break;
				case IJoinProperty joinProperty:
					JsonSerializer.Serialize(writer, joinProperty, options);
					break;
				case IFieldAliasProperty fieldAliasProperty:
					JsonSerializer.Serialize(writer, fieldAliasProperty, options);
					break;
				case IRankFeatureProperty rankFeatureProperty:
					JsonSerializer.Serialize(writer, rankFeatureProperty, options);
					break;
				case IRankFeaturesProperty rankFeaturesProperty:
					JsonSerializer.Serialize(writer, rankFeaturesProperty, options);
					break;
				case IKnnVectorProperty knnVectorProperty:
					JsonSerializer.Serialize(writer, knnVectorProperty, options);
					break;
				case IGenericProperty genericProperty:
					JsonSerializer.Serialize(writer, genericProperty, options);
					break;
				default:
					// Fallback: serialize using the actual runtime type
					JsonSerializer.Serialize(writer, value, value.GetType(), options);
					break;
			}
		}
	}
}
