﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web.Http.OData.Builder;
using System.Web.Http.OData.Routing;
using System.Web.Http.Routing;
using Microsoft.Data.Edm;
using Microsoft.TestCommon;

namespace System.Web.Http.OData.Formatter
{
    public class ODataTestUtil
    {
        private static IEdmModel _model;

        public const string Version1NumberString = "1.0";
        public const string Version2NumberString = "2.0";
        public const string Version3NumberString = "3.0";
        public static MediaTypeHeaderValue ApplicationJsonMediaType = MediaTypeHeaderValue.Parse("application/json;odata=verbose");
        public static MediaTypeHeaderValue ApplicationAtomMediaType = MediaTypeHeaderValue.Parse("application/atom+xml");
        public static MediaTypeWithQualityHeaderValue ApplicationJsonMediaTypeWithQuality = MediaTypeWithQualityHeaderValue.Parse("application/json;odata=verbose");
        public static MediaTypeWithQualityHeaderValue ApplicationAtomMediaTypeWithQuality = MediaTypeWithQualityHeaderValue.Parse("application/atom+xml");

        public static void VerifyResponse(HttpContent responseContent, string expected)
        {
            string response = responseContent.ReadAsStringAsync().Result;
            Regex updatedRegEx = new Regex("<updated>*.*</updated>");
            response = updatedRegEx.Replace(response, "<updated>UpdatedTime</updated>");
            Assert.Xml.Equal(expected, response);
        }

        public static void VerifyJsonResponse(HttpContent actualContent, string expected)
        {
            string actual = actualContent.ReadAsStringAsync().Result;
            JsonAssert.Equal(expected, actual);
        }

        public static HttpRequestMessage GenerateRequestMessage(Uri address, bool isAtom)
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, address);
            MediaTypeWithQualityHeaderValue mediaType = isAtom ? ApplicationAtomMediaTypeWithQuality : ApplicationJsonMediaTypeWithQuality;
            requestMessage.Headers.Accept.Add(mediaType);
            requestMessage.Headers.Add("DataServiceVersion", "2.0");
            requestMessage.Headers.Add("MaxDataServiceVersion", "3.0");
            return requestMessage;
        }

        public static string GetDataServiceVersion(HttpContentHeaders headers)
        {
            string dataServiceVersion = null;
            IEnumerable<string> values;
            if (headers.TryGetValues("DataServiceVersion", out values))
            {
                dataServiceVersion = values.FirstOrDefault();
            }
            return dataServiceVersion;
        }

        public static IEdmModel GetEdmModel()
        {
            if (_model == null)
            {
                ODataModelBuilder model = new ODataModelBuilder();

                var people = model.EntitySet<FormatterPerson>("People");
                people.HasFeedSelfLink(context => new Uri(context.Url.ODataLink(new EntitySetPathSegment(
                    context.EntitySet))));
                people.HasIdLink(context =>
                    {
                        return context.Url.ODataLink(new EntitySetPathSegment(context.EntitySet),
                            new KeyValuePathSegment((context.EntityInstance as FormatterPerson).PerId.ToString()));
                    },
                    followsConventions: false);

                var person = people.EntityType;
                person.HasKey(p => p.PerId);
                person.Property(p => p.Age);
                person.Property(p => p.MyGuid);
                person.Property(p => p.Name);
                person.ComplexProperty<FormatterOrder>(p => p.Order);

                var order = model.ComplexType<FormatterOrder>();
                order.Property(o => o.OrderAmount);
                order.Property(o => o.OrderName);

                _model = model.GetEdmModel();
            }

            return _model;
        }
    }

    public class FormatterPerson
    {
        public int Age { get; set; }
        public Guid MyGuid { get; set; }
        public string Name { get; set; }
        public FormatterOrder Order { get; set; }
        [Key]
        public int PerId { get; set; }
    }

    public class FormatterOrder
    {
        public int OrderAmount { get; set; }
        public string OrderName { get; set; }
    }

    public class PersonContext : DbContext
    {
        DbSet<FormatterPerson> Persons { get; set; }
    }
}
