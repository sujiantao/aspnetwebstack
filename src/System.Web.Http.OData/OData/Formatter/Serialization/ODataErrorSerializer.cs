﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Web.Http.OData.Properties;
using Microsoft.Data.OData;

namespace System.Web.Http.OData.Formatter.Serialization
{
    internal class ODataErrorSerializer : ODataSerializer
    {
        public ODataErrorSerializer()
            : base(ODataPayloadKind.Error)
        {
        }

        public override void WriteObject(object graph, ODataMessageWriter messageWriter, ODataSerializerContext writeContext)
        {
            if (graph == null)
            {
                throw Error.ArgumentNull("graph");
            }

            if (messageWriter == null)
            {
                throw Error.ArgumentNull("messageWriter");
            }

            ODataError oDataError = graph as ODataError;
            if (oDataError == null)
            {
                HttpError httpError = graph as HttpError;
                if (httpError == null)
                {
                    string message = Error.Format(SRResources.ErrorTypeMustBeODataErrorOrHttpError, graph.GetType().FullName);
                    throw new SerializationException(message);
                }
                else
                {
                    oDataError = httpError.ToODataError();
                }
            }

            bool includeDebugInformation = oDataError.InnerError != null;
            messageWriter.WriteError(oDataError, includeDebugInformation);
        }
    }
}
