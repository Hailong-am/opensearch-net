/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using FluentAssertions;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;
using Tests.Core.Client;
using Tests.Domain;

namespace Tests.QueryDsl.Specialized.Knn;

/// <summary>
/// Unit coverage for the <c>rescore</c> union on <see cref="IKnnQuery"/>, which the OpenSearch API
/// specification models as either a boolean (enable/disable) or an object with an
/// <c>oversample_factor</c>. The object form is covered end-to-end by <c>KnnQueryUsageTests</c>;
/// these tests pin the boolean form.
/// </summary>
public class KnnQueryRescoreTests
{
	[U]
	public void FluentRescoreBooleanSerializes()
	{
		var query = new QueryContainerDescriptor<Project>()
			.Knn(k => k
				.Field(f => f.Vector)
				.Vector(1.5f, -2.6f)
				.K(30)
				.Rescore(false));

		var json = TestClient.DisabledStreaming.RequestResponseSerializer.SerializeToString(query);

		json.Should().Contain("\"rescore\":false");
	}

	[U]
	public void InitializerRescoreBooleanSerializes()
	{
		var query = new KnnQuery
		{
			Field = "vector",
			Vector = new[] { 1.5f, -2.6f },
			K = 30,
			Rescore = true,
		};

		var json = TestClient.DisabledStreaming.RequestResponseSerializer.SerializeToString((QueryContainer)query);

		json.Should().Contain("\"rescore\":true");
	}
}
