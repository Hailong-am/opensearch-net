/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using FluentAssertions;
using OpenSearch.Client;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;
using Tests.Auth.AwsSigV4.Utils;
using Tests.Core.Connection.Http;
using Xunit;

namespace Tests.Auth.AwsSigV4;

public class AwsSigV4HttpConnectionTests
{
	private static readonly BasicAWSCredentials TestCredentials = new("test-access-key", "test-secret-key");
	private static readonly RegionEndpoint TestRegion = RegionEndpoint.APSoutheast2;
	private static readonly DateTime TestSigningTime = new(2023, 01, 13, 16, 08, 37, DateTimeKind.Utc);

	[TU]
	[InlineData("es", "9da9efb70bdaab38357b041a7233e3ec8be63cc2d11a186528fceac4e38a1b09")]
	[InlineData("aoss", "43e1b912e0eb6d1ed6c0537bf5948295bf8c832ecc9fa2b4253c8acea028d089")]
	[InlineData("arbitrary", "4f8ca2f6feae5494531bd3e77c520687ad09debe577fa3447ef628409f79768a")]
	public async Task SignsRequestCorrectly(string service, string expectedSignature)
	{
		var response = new HttpResponseMessage(HttpStatusCode.OK);
		response.Content = new StringContent(@"{
	""acknowledged"": true,
	""shards_acknowledged"": true,
    ""index"": ""sample-index1""
}", Encoding.UTF8, "application/json");

		HttpRequestMessage sentRequest = null;

		var client = CreateClient(r =>
		{
			sentRequest = r;
			return response;
		}, $"https://aaabbbcccddd111222333.ap-southeast-2.{service}.amazonaws.com", service);

		await client.Indices.CreateAsync("sample-index1", d =>
			d.Settings(s =>
					s.NumberOfShards(2).NumberOfReplicas(1))
				.Map(t =>
					t.Properties(p =>
						p.Number(n =>
							n.Name("age").Type(NumberType.Integer))))
				.Aliases(a => a.Alias("sample-alias1")));

		sentRequest.ShouldHaveHeader("x-amz-date", "20230113T160837Z");
		sentRequest.ShouldHaveHeader("x-amz-content-sha256", "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a");
		sentRequest.ShouldHaveHeader("Authorization", $"AWS4-HMAC-SHA256 Credential=test-access-key/20230113/ap-southeast-2/{service}/aws4_request, SignedHeaders=accept;content-type;host;x-amz-content-sha256;x-amz-date, Signature={expectedSignature}");
	}

	private static OpenSearchClient CreateClient(MockHttpMessageHandler.Handler handler, string uri, string service)
	{
		var connection =
			new TestableAwsSigV4HttpConnection(TestCredentials, TestRegion, service, new FixedDateTimeProvider(TestSigningTime), handler);
		var settings = new ConnectionSettings(new Uri(uri), connection);
		settings.DisableMetaHeader(); // Make headers & signature stable across platforms for testing
		return new OpenSearchClient(settings);
	}
}
