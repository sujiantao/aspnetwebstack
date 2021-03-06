﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Hosting;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Formatter.Deserialization;
using System.Web.Http.OData.Formatter.Serialization;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.TestCommon.Models;
using System.Web.Http.Routing;
using System.Xml.Linq;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Microsoft.TestCommon;
using Moq;

namespace System.Web.Http.OData.Formatter
{
    public class ODataMediaTypeFormatterTests : MediaTypeFormatterTestBase<ODataMediaTypeFormatter>
    {
        [Fact]
        public void WriteToStreamAsyncReturnsODataRepresentationForJsonLight()
        {
            WriteToStreamAsyncReturnsODataRepresentation(Resources.WorkItemEntryInJsonLight, true);
        }

        [Fact]
        public void WriteToStreamAsyncReturnsODataRepresentationForAtom()
        {
            WriteToStreamAsyncReturnsODataRepresentation(Resources.WorkItemEntryInAtom, false);
        }
        
        private static void WriteToStreamAsyncReturnsODataRepresentation(string expectedContent, bool json)
        {
            ODataConventionModelBuilder modelBuilder = new ODataConventionModelBuilder();
            modelBuilder.EntitySet<WorkItem>("WorkItems");
            IEdmModel model = modelBuilder.GetEdmModel();

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/WorkItems(10)");
            HttpConfiguration configuration = new HttpConfiguration();
            string routeName = "Route";
            configuration.Routes.MapODataRoute(routeName, null, model);
            request.SetConfiguration(configuration);
            request.SetEdmModel(model);
            IEdmEntitySet entitySet = model.EntityContainers().Single().EntitySets().Single();
            request.SetODataPath(new ODataPath(new EntitySetPathSegment(entitySet), new KeyValuePathSegment("10")));
            request.SetODataRouteName(routeName);

            ODataMediaTypeFormatter formatter;

            if (json)
            {
                formatter = CreateFormatterWithJson(model, request, ODataPayloadKind.Entry);
            }
            else
            {
                formatter = CreateFormatter(model, request, ODataPayloadKind.Entry);
            }

            ObjectContent<WorkItem> content = new ObjectContent<WorkItem>(
                (WorkItem)TypeInitializer.GetInstance(SupportedTypes.WorkItem), formatter);

            string actualContent = content.ReadAsStringAsync().Result;

            if (json)
            {
                JsonAssert.Equal(expectedContent, actualContent);
            }
            else
            {
                RegexReplacement replaceUpdateTime = new RegexReplacement(
                    "<updated>*.*</updated>", "<updated>UpdatedTime</updated>");
                Assert.Xml.Equal(expectedContent, actualContent, replaceUpdateTime);
            }
        }

        [Theory]
        // Slight inconsistency between direct link generation and Url.Link adds a "/" at the end when the OData path is empty
        // Tracked by Work Item 793
        [InlineData("prefix", "http://localhost/prefix", "http://localhost/prefix/")]
        [InlineData("{a}", "http://localhost/prefix", "http://localhost/prefix")]
        [InlineData("{a}/{b}", "http://localhost/prefix/prefix2", "http://localhost/prefix/prefix2")]
        public void WriteToStreamAsync_ReturnsCorrectBaseUri(string routePrefix, string requestUri, string expectedBaseUri)
        {
            IEdmModel model = new ODataConventionModelBuilder().GetEdmModel();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            HttpConfiguration configuration = new HttpConfiguration();
            string routeName = "Route";
            configuration.Routes.MapODataRoute(routeName, routePrefix, model);
            request.SetConfiguration(configuration);
            request.SetEdmModel(model);
            request.SetODataPath(new ODataPath());
            request.SetODataRouteName(routeName);
            HttpRouteData routeData = new HttpRouteData(new HttpRoute());
            routeData.Values.Add("a", "prefix");
            routeData.Values.Add("b", "prefix2");
            request.Properties[HttpPropertyKeys.HttpRouteDataKey] = routeData;

            ODataMediaTypeFormatter formatter = CreateFormatter(model, request, ODataPayloadKind.ServiceDocument);
            var content = new ObjectContent<ODataWorkspace>(new ODataWorkspace(), formatter);

            string actualContent = content.ReadAsStringAsync().Result;
            Assert.Contains("xml:base=\"" + expectedBaseUri + "\"", actualContent);
        }

        [Fact]
        public void WriteToStreamAsync_Throws_WhenBaseUriCannotBeGenerated()
        {
            IEdmModel model = new ODataConventionModelBuilder().GetEdmModel();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            HttpConfiguration configuration = new HttpConfiguration();
            configuration.Routes.MapHttpRoute("OData", "{param}");
            request.SetConfiguration(configuration);
            request.SetEdmModel(model);
            request.SetODataPath(new ODataPath());
            request.SetODataRouteName("OData");

            ODataMediaTypeFormatter formatter = CreateFormatter(model, request, ODataPayloadKind.ServiceDocument);
            var content = new ObjectContent<ODataWorkspace>(new ODataWorkspace(), formatter);

            Assert.Throws<SerializationException>(
                () => content.ReadAsStringAsync().Result,
                "The ODataMediaTypeFormatter was unable to determine the base URI for the request. The request must be processed by an OData route for the OData formatter to serialize the response.");
        }

        [Theory]
        [InlineData(null, null, "3.0")]
        [InlineData("1.0", null, "1.0")]
        [InlineData("2.0", null, "2.0")]
        [InlineData("3.0", null, "3.0")]
        [InlineData(null, "1.0", "1.0")]
        [InlineData(null, "2.0", "2.0")]
        [InlineData(null, "3.0", "3.0")]
        [InlineData("1.0", "1.0", "1.0")]
        [InlineData("1.0", "2.0", "2.0")]
        [InlineData("1.0", "3.0", "3.0")]
        public void SetDefaultContentHeaders_SetsRightODataServiceVersion(string requestDataServiceVersion, string requestMaxDataServiceVersion, string expectedDataServiceVersion)
        {
            HttpRequestMessage request = new HttpRequestMessage();
            if (requestDataServiceVersion != null)
            {
                request.Headers.TryAddWithoutValidation("DataServiceVersion", requestDataServiceVersion);
            }
            if (requestMaxDataServiceVersion != null)
            {
                request.Headers.TryAddWithoutValidation("MaxDataServiceVersion", requestMaxDataServiceVersion);
            }

            HttpContentHeaders contentHeaders = new StringContent("").Headers;

            CreateFormatterWithoutRequest()
            .GetPerRequestFormatterInstance(typeof(int), request, MediaTypeHeaderValue.Parse("application/xml"))
            .SetDefaultContentHeaders(typeof(int), contentHeaders, MediaTypeHeaderValue.Parse("application/xml"));

            IEnumerable<string> headervalues;
            Assert.True(contentHeaders.TryGetValues("DataServiceVersion", out headervalues));
            Assert.Equal(new string[] { expectedDataServiceVersion }, headervalues);
        }

        [Fact]
        public void TryGetInnerTypeForDelta_ChangesRefToGenericParameter_ForDeltas()
        {
            Type type = typeof(Delta<Customer>);

            bool success = ODataMediaTypeFormatter.TryGetInnerTypeForDelta(ref type);

            Assert.Same(typeof(Customer), type);
            Assert.True(success);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(List<string>))]
        public void TryGetInnerTypeForDelta_ReturnsFalse_ForNonDeltas(Type originalType)
        {
            Type type = originalType;

            bool success = ODataMediaTypeFormatter.TryGetInnerTypeForDelta(ref type);

            Assert.Same(originalType, type);
            Assert.False(success);
        }

        [Fact]
        public override Task WriteToStreamAsync_WhenObjectIsNull_WritesDataButDoesNotCloseStream()
        {
            // Arrange
            ODataMediaTypeFormatter formatter = CreateFormatterWithRequest();
            Mock<Stream> mockStream = new Mock<Stream>();
            mockStream.Setup(s => s.CanWrite).Returns(true);
            HttpContent content = new StringContent(String.Empty);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/atom+xml");

            // Act 
            return formatter.WriteToStreamAsync(typeof(SampleType), null, mockStream.Object, content, null).ContinueWith(
                writeTask =>
                {
                    // Assert (OData formatter doesn't support writing nulls)
                    Assert.Equal(TaskStatus.Faulted, writeTask.Status);
                    Assert.Throws<SerializationException>(() => writeTask.ThrowIfFaulted(), "Cannot serialize a null 'entry'.");
                    mockStream.Verify(s => s.Close(), Times.Never());
                    mockStream.Verify(s => s.BeginWrite(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<AsyncCallback>(), It.IsAny<object>()), Times.Never());
                });
        }

        [Theory]
        [InlineData("Test content", "utf-8", true)]
        [InlineData("Test content", "utf-16", true)]
        public override Task ReadFromStreamAsync_UsesCorrectCharacterEncoding(string content, string encoding, bool isDefaultEncoding)
        {
            // Arrange
            MediaTypeFormatter formatter = CreateFormatterWithRequest();
            formatter.SupportedEncodings.Add(CreateEncoding(encoding));
            string formattedContent = CreateFormattedContent(content);
            string mediaType = string.Format("application/json; odata=minimalmetadata; charset={0}", encoding);

            // Act & assert
            return ReadContentUsingCorrectCharacterEncodingHelper(
                formatter, content, formattedContent, mediaType, encoding, isDefaultEncoding);
        }

        [Theory]
        [InlineData("Test content", "utf-8", true)]
        [InlineData("Test content", "utf-16", true)]
        public override Task WriteToStreamAsync_UsesCorrectCharacterEncoding(string content, string encoding, bool isDefaultEncoding)
        {
            // Arrange
            MediaTypeFormatter formatter = CreateFormatterWithRequest();
            formatter.SupportedEncodings.Add(CreateEncoding(encoding));
            string formattedContent = CreateFormattedContent(content);
            string mediaType = string.Format("application/json; odata=minimalmetadata; charset={0}", encoding);

            // Act & assert
            return WriteContentUsingCorrectCharacterEncodingHelper(
                formatter, content, formattedContent, mediaType, encoding, isDefaultEncoding);
        }

        [Fact]
        public void ReadFromStreamAsync_ThrowsInvalidOperation_WithoutRequest()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            var formatter = CreateFormatter(builder.GetEdmModel());

            Assert.Throws<InvalidOperationException>(
                () => formatter.ReadFromStreamAsync(typeof(Customer), new MemoryStream(), content: null, formatterLogger: null),
                "The OData formatter requires an attached request in order to deserialize. Controller classes must derive from ODataController or be marked with ODataFormattingAttribute. Custom parameter bindings must call GetPerRequestFormatterInstance on each formatter and use these per-request instances.");
        }

        [Fact]
        public void WriteToStreamAsync_ThrowsInvalidOperation_WithoutRequest()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            var formatter = CreateFormatter(builder.GetEdmModel());

            Assert.Throws<InvalidOperationException>(
                () => formatter.WriteToStreamAsync(typeof(Customer), new Customer(), new MemoryStream(), content: null, transportContext: null),
                "The OData formatter does not support writing client requests. This formatter instance must have an associated request.");
        }

        [Fact]
        public void WriteToStreamAsync_Passes_MetadataLevelToSerializerContext()
        {
            // Arrange
            var model = CreateModel();

            SpyODataSerializer spy = new SpyODataSerializer(ODataPayloadKind.Property);

            ODataSerializerProvider serializerProvider = new FakeODataSerializerProvider(spy);

            ODataDeserializerProvider deserializerProvider = new DefaultODataDeserializerProvider();

            var formatter = new ODataMediaTypeFormatter(deserializerProvider, serializerProvider, Enumerable.Empty<ODataPayloadKind>(), ODataVersion.V3, CreateFakeODataRequest(model));
            HttpContent content = new StringContent("42");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;odata=fullmetadata");

            // Act
            formatter.WriteToStreamAsync(typeof(int), 42, new MemoryStream(), content, transportContext: null);

            // Assert
            Assert.Equal(ODataMetadataLevel.FullMetadata, spy.MetadataLevel);
        }

        [Fact]
        public void MessageReaderQuotas_Property_RoundTrip()
        {
            var formatter = CreateFormatter();
            formatter.MessageReaderQuotas.MaxNestingDepth = 42;

            Assert.Equal(42, formatter.MessageReaderQuotas.MaxNestingDepth);
        }

        [Fact]
        public void MessageWriterQuotas_Property_RoundTrip()
        {
            var formatter = CreateFormatter();
            formatter.MessageWriterQuotas.MaxNestingDepth = 42;

            Assert.Equal(42, formatter.MessageWriterQuotas.MaxNestingDepth);
        }

        [Fact]
        public void Default_ReceiveMessageSize_Is_MaxedOut()
        {
            var formatter = CreateFormatter();
            Assert.Equal(Int64.MaxValue, formatter.MessageReaderQuotas.MaxReceivedMessageSize);
        }

        [Fact]
        public void MessageReaderQuotas_Is_Passed_To_ODataLib()
        {
            ODataMediaTypeFormatter formatter = CreateFormatter();
            formatter.MessageReaderQuotas.MaxReceivedMessageSize = 1;

            HttpContent content = new StringContent("{ 'Number' : '42' }");
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            Assert.Throws<ODataException>(
                () => formatter.ReadFromStreamAsync(typeof(int), content.ReadAsStreamAsync().Result, content, formatterLogger: null).Result,
                "The maximum number of bytes allowed to be read from the stream has been exceeded. After the last read operation, a total of 19 bytes has been read from the stream; however a maximum of 1 bytes is allowed.");
        }

        private static Encoding CreateEncoding(string name)
        {
            if (name == "utf-8")
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            }
            else if (name == "utf-16")
            {
                return new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
            }
            else
            {
                throw new ArgumentException("name");
            }
        }

        private static string CreateFormattedContent(string value)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{{\r\n  \"odata.metadata\":\"http://dummy/#Edm.String\",\"value\":\"{0}\"\r\n}}", value);
        }

        protected override ODataMediaTypeFormatter CreateFormatter()
        {
            return CreateFormatterWithRequest();
        }

        protected override MediaTypeHeaderValue CreateSupportedMediaType()
        {
            return new MediaTypeHeaderValue("application/atom+xml");
        }

        private static ODataMediaTypeFormatter CreateFormatter(IEdmModel model)
        {
            return new ODataMediaTypeFormatter(new ODataPayloadKind[0]);
        }

        private static ODataMediaTypeFormatter CreateFormatter(IEdmModel model, HttpRequestMessage request,
            params ODataPayloadKind[] payloadKinds)
        {
            return new ODataMediaTypeFormatter(payloadKinds, request);
        }

        private static ODataMediaTypeFormatter CreateFormatterWithoutRequest()
        {
            return CreateFormatter(CreateModel());
        }

        private static ODataMediaTypeFormatter CreateFormatterWithJson(IEdmModel model, HttpRequestMessage request,
            params ODataPayloadKind[] payloadKinds)
        {
            ODataMediaTypeFormatter formatter = CreateFormatter(model, request, payloadKinds);
            formatter.SupportedMediaTypes.Add(ODataMediaTypes.ApplicationJsonODataMinimalMetadata);
            return formatter;
        }

        private static ODataMediaTypeFormatter CreateFormatterWithRequest()
        {
            var model = CreateModel();
            var request = CreateFakeODataRequest(model);
            return CreateFormatter(model, request);
        }

        private static HttpRequestMessage CreateFakeODataRequest(IEdmModel model)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://dummy/");
            request.SetEdmModel(model);
            HttpConfiguration configuration = new HttpConfiguration();
            configuration.Routes.MapFakeODataRoute();
            request.SetConfiguration(configuration);
            request.SetODataPath(new ODataPath(new EntitySetPathSegment(
                model.EntityContainers().Single().EntitySets().Single())));
            request.SetFakeODataRouteName();
            return request;
        }

        private static IEdmModel CreateModel()
        {
            ODataConventionModelBuilder model = new ODataConventionModelBuilder();
            model.Entity<SampleType>();
            model.EntitySet<SampleType>("sampleTypes");
            return model.GetEdmModel();
        }

        public override IEnumerable<MediaTypeHeaderValue> ExpectedSupportedMediaTypes
        {
            get
            {
                return new MediaTypeHeaderValue[0];
            }
        }

        public override IEnumerable<Encoding> ExpectedSupportedEncodings
        {
            get
            {
                return new Encoding[0];
            }
        }

        public override byte[] ExpectedSampleTypeByteRepresentation
        {
            get
            {
                return Encoding.UTF8.GetBytes(
                  @"<entry xml:base=""http://localhost/"" xmlns=""http://www.w3.org/2005/Atom"" xmlns:d=""http://schemas.microsoft.com/ado/2007/08/dataservices"" xmlns:m=""http://schemas.microsoft.com/ado/2007/08/dataservices/metadata"" xmlns:georss=""http://www.georss.org/georss"" xmlns:gml=""http://www.opengis.net/gml"">
                      <category term=""System.Net.Http.Formatting.SampleType"" scheme=""http://schemas.microsoft.com/ado/2007/08/dataservices/scheme"" />
                      <id />
                      <title />
                      <updated>2012-08-17T00:16:14Z</updated>
                      <author>
                        <name />
                      </author>
                      <content type=""application/xml"">
                        <m:properties>
                          <d:Number m:type=""Edm.Int32"">42</d:Number>
                        </m:properties>
                      </content>
                    </entry>"
                );
            }
        }

        private class SpyODataSerializer : ODataSerializer
        {
            public SpyODataSerializer(ODataPayloadKind payloadKind)
                : base(payloadKind)
            {
            }

            public ODataMetadataLevel MetadataLevel { get; private set; }

            public override void WriteObject(object graph, ODataMessageWriter messageWriter,
                ODataSerializerContext writeContext)
            {
                MetadataLevel = writeContext.MetadataLevel;
            }
        }
    }
}
