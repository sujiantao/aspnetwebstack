﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Text;
using Microsoft.Data.OData;

namespace System.Web.Http
{
    /// <summary>
    /// Provides extension methods for the <see cref="HttpError"/> class.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ODataHttpErrorExtensions
    {
        private const string MessageKey = "Message";
        private const string MessageDetailKey = "MessageDetail";
        private const string MessageLanguageKey = "MessageLanguage";
        private const string ErrorCodeKey = "ErrorCode";
        private const string ExceptionMessageKey = "ExceptionMessage";
        private const string ExceptionTypeKey = "ExceptionType";
        private const string StackTraceKey = "StackTrace";
        private const string InnerExceptionKey = "InnerException";
        private const string ModelStateKey = "ModelState";

        /// <summary>
        /// Converts the <paramref name="httpError"/> to an <see cref="ODataError"/>.
        /// </summary>
        /// <param name="httpError">The <see cref="HttpError"/> instance to convert.</param>
        /// <returns>The converted <see cref="ODataError"/></returns>
        public static ODataError ToODataError(this HttpError httpError)
        {
            if (httpError == null)
            {
                throw Error.ArgumentNull("httpError");
            }

            return new ODataError()
            {
                Message = httpError.GetPropertyValue<string>(MessageKey),
                MessageLanguage = httpError.GetPropertyValue<string>(MessageLanguageKey),
                ErrorCode = httpError.GetPropertyValue<string>(ErrorCodeKey),
                InnerError = httpError.ToODataInnerError()
            };
        }

        private static ODataInnerError ToODataInnerError(this HttpError httpError)
        {
            string innerErrorMessage = httpError.GetPropertyValue<string>(ExceptionMessageKey);
            if (innerErrorMessage == null)
            {
                string messageDetail = httpError.GetPropertyValue<string>(MessageDetailKey);
                if (messageDetail == null)
                {
                    HttpError modelStateError = httpError.GetPropertyValue<HttpError>(ModelStateKey);
                    return modelStateError == null ? null : new ODataInnerError { Message = ConvertModelStateErrors(modelStateError) };
                }
                else
                {
                    return new ODataInnerError() { Message = messageDetail };
                }
            }
            else
            {
                ODataInnerError innerError = new ODataInnerError();
                innerError.Message = innerErrorMessage;
                innerError.TypeName = httpError.GetPropertyValue<string>(ExceptionTypeKey);
                innerError.StackTrace = httpError.GetPropertyValue<string>(StackTraceKey);
                HttpError innerExceptionError = httpError.GetPropertyValue<HttpError>(InnerExceptionKey);
                if (innerExceptionError != null)
                {
                    innerError.InnerError = innerExceptionError.ToODataInnerError();
                }
                return innerError;
            }
        }

        // Convert the model state errors in to a string (for debugging only).
        // This should be improved once ODataError allows more details.
        private static string ConvertModelStateErrors(HttpError error)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, object> modelStateError in error)
            {
                if (modelStateError.Value != null)
                {
                    builder.Append(modelStateError.Key);
                    builder.Append(" : ");

                    IEnumerable<string> errorMessages = modelStateError.Value as IEnumerable<string>;
                    if (errorMessages != null)
                    {
                        foreach (string errorMessage in errorMessages)
                        {
                            builder.AppendLine(errorMessage);
                        }
                    }
                    else
                    {
                        builder.AppendLine(modelStateError.Value.ToString());
                    }
                }
            }

            return builder.ToString();
        }

        private static TValue GetPropertyValue<TValue>(this HttpError httpError, string key)
        {
            Contract.Assert(httpError != null);

            object value;
            if (httpError.TryGetValue(key, out value))
            {
                if (value is TValue)
                {
                    return (TValue)value;
                }
            }
            return default(TValue);
        }
    }
}
