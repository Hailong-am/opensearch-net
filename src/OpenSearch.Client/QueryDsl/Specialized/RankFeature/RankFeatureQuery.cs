/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
/*
* Modifications Copyright OpenSearch Contributors. See
* GitHub history for details.
*
*  Licensed to Elasticsearch B.V. under one or more contributor
*  license agreements. See the NOTICE file distributed with
*  this work for additional information regarding copyright
*  ownership. Elasticsearch B.V. licenses this file to you under
*  the Apache License, Version 2.0 (the "License"); you may
*  not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
* 	http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing,
*  software distributed under the License is distributed on an
*  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
*  KIND, either express or implied.  See the License for the
*  specific language governing permissions and limitations
*  under the License.
*/

using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A query that only works on rank_feature fields and rank_features fields. Its goal is to boost the score of documents
	/// based on the values of numeric features. It is typically put in a should clause of a bool query so that its score
	/// is added to the score of the query.
	///
	/// Compared to using function_score or other ways to modify the score, this query has the benefit of being able to efficiently
	/// skip non-competitive hits when track_total_hits is not set to true. Speedups may be spectacular.
	/// </summary>
	[JsonConverter(typeof(RankFeatureQueryConverter))]
	[InterfaceDataContract]
	public interface IRankFeatureQuery : IFieldNameQuery
	{
		/// <inheritdoc cref="IRankFeatureFunction"/>
		IRankFeatureFunction Function { get; set; }
	}

	public class RankFeatureQuery : FieldNameQueryBase, IRankFeatureQuery
	{
		protected override bool Conditionless => IsConditionless(this);

		internal static bool IsConditionless(IRankFeatureQuery q) => q.Field.IsConditionless();

		internal override void InternalWrapInContainer(IQueryContainer container) => container.RankFeature = this;

		/// <inheritdoc />
		public IRankFeatureFunction Function { get; set; }
	}

	public class RankFeatureQueryDescriptor<T>
		: FieldNameQueryDescriptorBase<RankFeatureQueryDescriptor<T>, IRankFeatureQuery, T>
			, IRankFeatureQuery where T : class
	{
		IRankFeatureFunction IRankFeatureQuery.Function { get; set; }

		protected override bool Conditionless => RankFeatureQuery.IsConditionless(this);

		/// <inheritdoc cref="IRankFeatureSaturationFunction"/>
		public RankFeatureQueryDescriptor<T> Saturation(Func<RankFeatureSaturationFunctionDescriptor, IRankFeatureSaturationFunction> selector = null) =>
			Assign(selector, (a, v) => a.Function = v.InvokeOrDefault(new RankFeatureSaturationFunctionDescriptor()));

		/// <inheritdoc cref="IRankFeatureLogarithmFunction"/>
		public RankFeatureQueryDescriptor<T> Logarithm(Func<RankFeatureLogarithmFunctionDescriptor, IRankFeatureLogarithmFunction> selector) =>
			Assign(selector, (a, v) => a.Function = v?.Invoke(new RankFeatureLogarithmFunctionDescriptor()));

		/// <inheritdoc cref="IRankFeatureSigmoidFunction"/>
		public RankFeatureQueryDescriptor<T> Sigmoid(Func<RankFeatureSigmoidFunctionDescriptor, IRankFeatureSigmoidFunction> selector) =>
			Assign(selector, (a, v) => a.Function = v?.Invoke(new RankFeatureSigmoidFunctionDescriptor()));

		/// <inheritdoc cref="IRankFeatureLinearFunction"/>
		public RankFeatureQueryDescriptor<T> Linear()
		{
			Self.Function = new RankFeatureLinearFunction();
			return this;
		}
	}

	/// <summary>
	/// A function to boost scores in a rank_feature query, using the values of rank features.
	/// </summary>
	public interface IRankFeatureFunction { }

	/// <summary>
	/// Gives a score that is equal to log(scaling_factor + S) where S is the value of the rank feature and scaling_factor is a configurable
	/// scaling factor. Scores are unbounded.
	/// This function only supports rank features that have a positive score impact.
	/// </summary>
	public interface IRankFeatureLogarithmFunction : IRankFeatureFunction
	{
		/// <summary>
		/// The scaling factor
		/// </summary>
		[DataMember(Name = "scaling_factor")]
		float ScalingFactor { get; set; }
	}

	/// <inheritdoc />
	public class RankFeatureLogarithmFunction : IRankFeatureLogarithmFunction
	{
		/// <inheritdoc />
		public float ScalingFactor { get; set; }
	}

	/// <inheritdoc cref="IRankFeatureLogarithmFunction" />
	public class RankFeatureLogarithmFunctionDescriptor
		: DescriptorBase<RankFeatureLogarithmFunctionDescriptor, IRankFeatureLogarithmFunction>, IRankFeatureLogarithmFunction
	{
		float IRankFeatureLogarithmFunction.ScalingFactor { get; set; }

		/// <inheritdoc cref="IRankFeatureLogarithmFunction.ScalingFactor" />
		public RankFeatureLogarithmFunctionDescriptor ScalingFactor(float scalingFactor) =>
			Assign(scalingFactor, (a, v) => a.ScalingFactor = v);
	}

	/// <summary>
	/// Gives a score that is equal to S / (S + pivot) where S is the value of the rank feature and pivot is a configurable pivot value
	/// so that the result will be less than 0.5 if S is less than pivot and greater than 0.5 otherwise. Scores are always is (0, 1).
	/// If the rank feature has a negative score impact then the function will be computed as pivot / (S + pivot), which decreases when S increases.
	/// </summary>
	public interface IRankFeatureSaturationFunction : IRankFeatureFunction
	{
		[DataMember(Name = "pivot")]
		float? Pivot { get; set; }
	}

	/// <inheritdoc />
	public class RankFeatureSaturationFunction : IRankFeatureSaturationFunction
	{
		public float? Pivot { get; set; }
	}

	/// <inheritdoc cref="IRankFeatureSaturationFunction" />
	public class RankFeatureSaturationFunctionDescriptor
		: DescriptorBase<RankFeatureSaturationFunctionDescriptor, IRankFeatureSaturationFunction>, IRankFeatureSaturationFunction
	{
		float? IRankFeatureSaturationFunction.Pivot { get; set; }

		/// <inheritdoc cref="IRankFeatureSaturationFunction.Pivot" />
		public RankFeatureSaturationFunctionDescriptor Pivot(float? pivot) => Assign(pivot, (a, v) => a.Pivot = v);
	}

	/// <summary>
	/// is an extension of saturation which adds a configurable exponent. Scores are computed as S^exp^ / (S^exp^ + pivot^exp^).
	/// Like for the saturation function, pivot is the value of S that gives a score of 0.5 and scores are in (0, 1).
	///
	/// exponent must be positive, but is typically in [0.5, 1]. A good value should be computed via training. If you don’t have the opportunity
	/// to do so, we recommend that you stick to the saturation function instead.
	/// </summary>
	public interface IRankFeatureSigmoidFunction : IRankFeatureFunction
	{
		[DataMember(Name = "pivot")]
		float Pivot { get; set; }

		/// <summary>
		/// The exponent. Must be positive
		/// </summary>
		[DataMember(Name = "exponent")]
		float Exponent { get; set; }
	}

	/// <inheritdoc cref="IRankFeatureSigmoidFunction" />
	public class RankFeatureSigmoidFunction : IRankFeatureSigmoidFunction
	{
		public float Pivot { get; set; }

		/// <inheritdoc cref="IRankFeatureSigmoidFunction.Exponent" />
		public float Exponent { get; set; }
	}

	/// <inheritdoc cref="IRankFeatureSigmoidFunction" />
	public class RankFeatureSigmoidFunctionDescriptor
		: DescriptorBase<RankFeatureSigmoidFunctionDescriptor, IRankFeatureSigmoidFunction>, IRankFeatureSigmoidFunction
	{
		float IRankFeatureSigmoidFunction.Exponent { get; set; }
		float IRankFeatureSigmoidFunction.Pivot { get; set; }

		/// <inheritdoc cref="IRankFeatureSigmoidFunction.Exponent" />
		public RankFeatureSigmoidFunctionDescriptor Exponent(float exponent) => Assign(exponent, (a, v) => a.Exponent = v);

		/// <inheritdoc cref="IRankFeatureSigmoidFunction.Pivot" />
		public RankFeatureSigmoidFunctionDescriptor Pivot(float pivot) => Assign(pivot, (a, v) => a.Pivot = v);
	}

	/// <summary>
	/// Gives a score equal to the indexed value of S, where S is the value of the rank feature field.
	///
	/// If a rank feature field is indexed with "positive_score_impact": true, its indexed value is equal to S and rounded to preserve
	/// only 9 significant bits for the precision.
	///
	/// If a rank feature field is indexed with "positive_score_impact": false, its indexed value is equal to 1/S and rounded to
	/// preserve only 9 significant bits for the precision.
	/// </summary>
	public interface IRankFeatureLinearFunction : IRankFeatureFunction
	{
	}

	/// <inheritdoc cref="IRankFeatureLinearFunction" />
	public class RankFeatureLinearFunction : IRankFeatureLinearFunction
	{
	}

}
