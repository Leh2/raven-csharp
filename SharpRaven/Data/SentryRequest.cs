﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json;

namespace SharpRaven.Data
{
    /// <summary>
    /// The Request information is stored in the Http interface. Two arguments are required: url and method.
    /// </summary>
    public class SentryRequest
    {
        private readonly dynamic httpContext;


        private SentryRequest()
        {
            // NOTE: We're using dynamic to not require a reference to System.Web.
            this.httpContext = GetHttpContext();

            if (!HasHttpContext)
                return;

            Url = this.httpContext.Request.Url.ToString();
            Method = this.httpContext.Request.HttpMethod;
            Environment = Convert(x => x.Request.ServerVariables);
            Headers = Convert(x => x.Request.Headers);
            Cookies = Convert(x => x.Request.Cookies);
            Data = Convert(x => x.Request.Form);
            QueryString = this.httpContext.Request.QueryString.ToString();
        }


        [JsonIgnore]
        private bool HasHttpContext
        {
            get { return this.httpContext != null; }
        }


        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }

        [JsonProperty(PropertyName = "method", NullValueHandling = NullValueHandling.Ignore)]
        public string Method { get; set; }

        /// <summary>
        /// The data variable should only contain the request body (not the query string). It can either be a dictionary (for standard HTTP requests) or a raw request body.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        [JsonProperty(PropertyName = "query_string", NullValueHandling = NullValueHandling.Ignore)]
        public string QueryString { get; set; }

        [JsonProperty(PropertyName = "cookies", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Cookies { get; set; }

        [JsonProperty(PropertyName = "headers", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The env variable is a compounded dictionary of HTTP headers as well as environment information passed from the webserver.
        /// Sentry will explicitly look for REMOTE_ADDR in env for things which require an IP address.
        /// </summary>
        /// <value>
        /// The environment.
        /// </value>
        [JsonProperty(PropertyName = "env", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Environment { get; set; }


        public static SentryRequest GetRequest()
        {
            var request = new SentryRequest();
            return request.HasHttpContext ? request : null;
        }


        public SentryUser GetUser()
        {
            if (!HasHttpContext)
                return null;
            
            return new SentryUser(this.httpContext.User)
            {
                IpAddress = this.httpContext.Request.UserHostAddress
            };
        }


        private IDictionary<string, string> Convert(Func<dynamic, NameValueCollection> collectionGetter)
        {
            if (this.httpContext == null)
                return null;

            IDictionary<string, string> dictionary = new Dictionary<string, string>();

            try
            {
                NameValueCollection collection = collectionGetter.Invoke(this.httpContext);
                var keys = collection.AllKeys.ToArray();

                foreach (var key in keys)
                {
                    // NOTE: Ignore these keys as they just add duplicate information. [asbjornu]
                    if (key.StartsWith("ALL_") || key.StartsWith("HTTP_"))
                        continue;

                    var value = collection[key];
                    dictionary.Add(key, value);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return dictionary;
        }


        private static dynamic GetHttpContext()
        {
            var systemWeb = AppDomain.CurrentDomain
                                     .GetAssemblies()
                                     .FirstOrDefault(assembly => assembly.FullName.StartsWith("System.Web"));

            if (systemWeb == null)
                return null;

            var httpContextType = systemWeb.GetExportedTypes()
                                           .FirstOrDefault(type => type.Name == "HttpContext");

            if (httpContextType == null)
                return null;

            var currentHttpContextProperty = httpContextType.GetProperty("Current",
                                                                         BindingFlags.Static | BindingFlags.Public);

            if (currentHttpContextProperty == null)
                return null;

            return currentHttpContextProperty.GetValue(null, null);
        }
    }
}