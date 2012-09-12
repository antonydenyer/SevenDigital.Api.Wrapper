﻿using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Xml;
using FakeItEasy;
using NUnit.Framework;
using SevenDigital.Api.Wrapper.EndpointResolution;
using SevenDigital.Api.Wrapper.EndpointResolution.OAuth;
using SevenDigital.Api.Wrapper.Http;

namespace SevenDigital.Api.Wrapper.Unit.Tests.Http
{
	[TestFixture]
	public class HttpRequestorTests
	{
		private const string API_URL = "http://api.7digital.com/1.2";
		private const string SERVICE_STATUS = "<response status=\"ok\" version=\"1.2\" ><serviceStatus><serverTime>2011-03-04T08:10:29Z</serverTime></serviceStatus></response>";

		private readonly string _consumerKey = new AppSettingsCredentials().ConsumerKey;
		private IHttpClientWrapper _httpClient;
		private IHttpRequestor _requestCoordinator;
		private IUrlSigner _urlSigner;

		[SetUp]
		public void Setup()
		{
			_httpClient = A.Fake<IHttpClientWrapper>();
			_urlSigner = A.Fake<IUrlSigner>();
			_requestCoordinator = new HttpRequestor(_httpClient, _urlSigner, EssentialDependencyCheck<IOAuthCredentials>.Instance, EssentialDependencyCheck<IApiUri>.Instance);
		}

		[Test]
		public void Should_fire_resolve_with_correct_values()
		{
			_httpClient.MockGetAsync(new Response(HttpStatusCode.OK, SERVICE_STATUS));

			HttpMethod expectedMethod = HttpMethod.Get;
			var expectedHeaders = new Dictionary<string, string>();
			var expected = string.Format("{0}/test?oauth_consumer_key={1}", API_URL, _consumerKey);

			var endPointState = new RequestData 
				{ 
						UriPath = "test", 
						HttpMethod = expectedMethod, 
						Headers = expectedHeaders 
				};

			_requestCoordinator.GetDataAsync(endPointState);

			_httpClient.GetAsyncOnUrlMustHaveHappened(expected);
		}

		[Test]
		public void Should_fire_resolve_with_url_encoded_parameters()
		{
			_httpClient.MockGetAsync(new Response(HttpStatusCode.OK, SERVICE_STATUS));
			const string unEncodedParameterValue = "Alive & Amplified";

			const string expectedParameterValue = "Alive%20%26%20Amplified";
			var expectedHeaders = new Dictionary<string, string>();
			var testParameters = new Dictionary<string, string> { { "q", unEncodedParameterValue } };
			var expected = string.Format("{0}/test?oauth_consumer_key={1}&q={2}", API_URL, _consumerKey, expectedParameterValue);

			var endPointState = new RequestData 
				{ 
						UriPath = "test", 
						HttpMethod = HttpMethod.Get, 
						Headers = expectedHeaders, 
						Parameters = testParameters };

			_requestCoordinator.GetDataAsync(endPointState);

			_httpClient.GetAsyncOnUrlMustHaveHappened(expected);
		}

		[Test]
		public void Should_not_care_how_many_times_you_create_an_endpoint()
		{
			var endPointState = new RequestData
				{
					UriPath = "{slug}", 
					HttpMethod = HttpMethod.Get, 
					Parameters = new Dictionary<string, string> { { "slug", "something" } }
				};
			var result = _requestCoordinator.EndpointUrl(endPointState);

			Assert.That(result, Is.EqualTo(_requestCoordinator.EndpointUrl(endPointState)));
		}

		[Test]
		public async void Should_return_xmlnode_if_valid_xml_received()
		{
			Given_a_urlresolver_that_returns_valid_xml();

			var response = await _requestCoordinator.GetDataAsync(new RequestData());
			var hitEndpoint = new XmlDocument();
			hitEndpoint.LoadXml(response.Body);
			Assert.That(hitEndpoint.HasChildNodes);
			Assert.That(hitEndpoint.SelectSingleNode("//serverTime"), Is.Not.Null);
		}


		[Test]
		public async void Should_return_xmlnode_if_valid_xml_received_using_async()
		{
			var fakeClient = new FakeHttpClientWrapper(new Response(HttpStatusCode.OK, SERVICE_STATUS));

			var endpointResolver = new HttpRequestor(fakeClient, _urlSigner, EssentialDependencyCheck<IOAuthCredentials>.Instance, EssentialDependencyCheck<IApiUri>.Instance);

			var responseData = await  endpointResolver.GetDataAsync(new RequestData());
			var response = responseData.Body;

			var payload = new XmlDocument();
			payload.LoadXml(response);

			Assert.That(payload.HasChildNodes);
			Assert.That(payload.SelectSingleNode("//serverTime"), Is.Not.Null);
		}

		[Test]
		public void Should_use_api_uri_provided_by_IApiUri_interface()
		{
			const string expectedApiUri = "http://api.7dizzle";

			Given_a_urlresolver_that_returns_valid_xml();

			var apiUri = A.Fake<IApiUri>();

			A.CallTo(() => apiUri.Uri).Returns(expectedApiUri);

			IOAuthCredentials oAuthCredentials = EssentialDependencyCheck<IOAuthCredentials>.Instance;
			var endpointResolver = new HttpRequestor(_httpClient, _urlSigner, oAuthCredentials, apiUri);

			var endPointState = new RequestData
				{
					UriPath = "test", 
					HttpMethod = HttpMethod.Get, 
					Headers = new Dictionary<string, string>()
				};

			endpointResolver.GetDataAsync(endPointState);

			A.CallTo(() => apiUri.Uri).MustHaveHappened(Repeated.Exactly.Once);

			_httpClient.GetAsyncOnUrlContainingMustHaveHappenedOnce(expectedApiUri);
		}

		[Test]
		public void Construct_url_should_combine_url_and_query_params_for_get_requests()
		{
			const string uriPath = "something";
			var result = _requestCoordinator.EndpointUrl(new RequestData { UriPath = uriPath });

			Assert.That(result, Is.EqualTo(API_URL + "/" + uriPath + "?oauth_consumer_key=" + _consumerKey));
		}

		[Test]
		public void Construct_url_should_combine_url_and_not_query_params_for_post_requests()
		{
			const string uriPath = "something";
			var request = new RequestData
				{
					UriPath = uriPath,
					HttpMethod = HttpMethod.Post
				};
			var result = _requestCoordinator.EndpointUrl(request);

			Assert.That(result, Is.EqualTo(API_URL + "/" + uriPath ));
		}

		private void Given_a_urlresolver_that_returns_valid_xml()
		{
			_httpClient.MockGetAsync(new Response( HttpStatusCode.OK, SERVICE_STATUS));
		}
	}
}