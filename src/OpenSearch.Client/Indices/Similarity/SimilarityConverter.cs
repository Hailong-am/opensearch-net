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
	/// Deserializes <see cref="ISimilarity"/> by dispatching on its <c>type</c> discriminator to the
	/// matching concrete similarity, falling back to <see cref="CustomSimilarity"/>. Serializes via the
	/// runtime type so the concrete <see cref="InterfaceDataContractAttribute"/> contract is emitted.
	/// Replaces the Utf8Json <c>SimilarityFormatter</c>.
	/// </summary>
	internal sealed class SimilarityConverter : JsonConverter<ISimilarity>
	{
		private static readonly Dictionary<string, Type> SimilarityTypes = new()
		{
			["BM25"] = typeof(BM25Similarity),
			["LMDirichlet"] = typeof(LMDirichletSimilarity),
			["DFR"] = typeof(DFRSimilarity),
			["DFI"] = typeof(DFISimilarity),
			["IB"] = typeof(IBSimilarity),
			["LMJelinekMercer"] = typeof(LMJelinekMercerSimilarity),
			["scripted"] = typeof(ScriptedSimilarity),
		};

		public override ISimilarity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			var targetType = typeof(CustomSimilarity);
			if (root.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
			{
				var type = typeProp.GetString();
				if (type != null && SimilarityTypes.TryGetValue(type, out var mapped))
					targetType = mapped;
			}

			return (ISimilarity)JsonSerializer.Deserialize(root.GetRawText(), targetType, options);
		}

		public override void Write(Utf8JsonWriter writer, ISimilarity value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
