﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Linq;
using System.Runtime.Serialization;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Routing;
using Microsoft.Data.Edm;
using Microsoft.Data.OData;
using Microsoft.TestCommon;

namespace System.Web.Http.OData.Formatter.Deserialization
{
    public class ODataEntityReferenceLinkDeserializerTests
    {
        [Fact]
        public void Constructor()
        {
            var deserializer = new ODataEntityReferenceLinkDeserializer();

            Assert.Equal(deserializer.ODataPayloadKind, ODataPayloadKind.EntityReferenceLink);
        }

        [Fact]
        public void Read()
        {
            // Arrange
            var deserializer = new ODataEntityReferenceLinkDeserializer();
            MockODataRequestMessage requestMessage = new MockODataRequestMessage();
            ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage);
            messageWriter.WriteEntityReferenceLink(new ODataEntityReferenceLink { Url = new Uri("http://localhost/samplelink") });
            ODataMessageReader messageReader = new ODataMessageReader(new MockODataRequestMessage(requestMessage));
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Path = new ODataPath(new NavigationPathSegment(GetNavigationProperty(CreateModel())))
            };

            // Act
            Uri uri = deserializer.Read(messageReader, context) as Uri;

            // Assert
            Assert.NotNull(uri);
            Assert.Equal("http://localhost/samplelink", uri.AbsoluteUri);
        }

        [Fact]
        public void ReadJsonLight()
        {
            // Arrange
            var deserializer = new ODataEntityReferenceLinkDeserializer();
            MockODataRequestMessage requestMessage = new MockODataRequestMessage();
            ODataMessageWriterSettings writerSettings = new ODataMessageWriterSettings();
            writerSettings.SetContentType(ODataFormat.Json);
            IEdmModel model = CreateModel();
            ODataMessageWriter messageWriter = new ODataMessageWriter(requestMessage, writerSettings, model);
            messageWriter.WriteEntityReferenceLink(new ODataEntityReferenceLink { Url = new Uri("http://localhost/samplelink") });
            ODataMessageReader messageReader = new ODataMessageReader(new MockODataRequestMessage(requestMessage),
                new ODataMessageReaderSettings(), model);

            IEdmNavigationProperty navigationProperty = GetNavigationProperty(model);

            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Path = new ODataPath(new NavigationPathSegment(navigationProperty))
            };

            // Act
            Uri uri = deserializer.Read(messageReader, context) as Uri;

            // Assert
            Assert.NotNull(uri);
            Assert.Equal("http://localhost/samplelink", uri.AbsoluteUri);
        }

        [Fact]
        public void ReadThrowsWhenPathIsMissing()
        {
            // Arrange
            var deserializer = new ODataEntityReferenceLinkDeserializer();
            ODataMessageReader reader = new ODataMessageReader(new MockODataRequestMessage());
            ODataDeserializerContext context = new ODataDeserializerContext();

            // Act & Assert
            Assert.Throws<SerializationException>(() => deserializer.Read(reader, context),
                "The operation cannot be completed because no ODataPath is available for the request.");
        }

        [Fact]
        public void ReadThrowsWhenNavigationPropertyIsMissing()
        {
            // Arrange
            var deserializer = new ODataEntityReferenceLinkDeserializer();
            ODataMessageReader reader = new ODataMessageReader(new MockODataRequestMessage());
            ODataDeserializerContext context = new ODataDeserializerContext
            {
                Path = new ODataPath()
            };

            // Act & Assert
            Assert.Throws<SerializationException>(() => deserializer.Read(reader, context),
                "The related navigation property could not be found from the OData path. The related navigation property is required to deserialize the payload.");
        }

        private static IEdmModel CreateModel()
        {
            ODataModelBuilder builder = new ODataModelBuilder();
            EntitySetConfiguration<Entity> entities = builder.EntitySet<Entity>("entities");
            builder.EntitySet<RelatedEntity>("related");
            NavigationPropertyConfiguration entityToRelated =
                entities.EntityType.HasOptional<RelatedEntity>((e) => e.Related);
            entities.HasNavigationPropertyLink(entityToRelated, (a, b) => new Uri("aa:b"), false);

            return builder.GetEdmModel();
        }

        private static IEdmNavigationProperty GetNavigationProperty(IEdmModel model)
        {
            return
                model.EntityContainers().Single().EntitySets().First().NavigationTargets.Single().NavigationProperty;
        }

        private class Entity
        {
            public RelatedEntity Related { get; set; }
        }

        private class RelatedEntity
        {
        }
    }
}
