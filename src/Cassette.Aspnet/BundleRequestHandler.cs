﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Cassette.Utilities;
using Trace = Cassette.Diagnostics.Trace;

namespace Cassette.Aspnet
{
    class BundleRequestHandler<T> : ICassetteRequestHandler
        where T : Bundle
    {
        readonly BundleCollection bundles;
        readonly HttpResponseBase response;
        readonly HttpRequestBase request;
        readonly HttpContextBase httpContext;

        public BundleRequestHandler(BundleCollection bundles, RequestContext requestContext)
        {
            this.bundles = bundles;

            response = requestContext.HttpContext.Response;
            request = requestContext.HttpContext.Request;
            httpContext = requestContext.HttpContext;
        }

        public void ProcessRequest(string path)
        {
            httpContext.DisableHtmlRewriting();
            using (bundles.GetReadLock())
            {
                var bundle = FindBundle(path);
                if (bundle == null)
                {
                    Trace.Source.TraceInformation("Bundle not found \"{0}\".", path);
                    response.StatusCode = 404;
                }
                else
                {
                    var actualETag = "\"" + bundle.Hash.ToHexString() + "\"";
                    var givenETag = request.Headers["If-None-Match"];
                    if (givenETag == actualETag)
                    {
                        SendNotModified(actualETag);
                    }
                    else
                    {
                        SendBundle(bundle, actualETag);
                    }
                }
            }
        }

        Bundle FindBundle(string path)
        {
            Trace.Source.TraceInformation("Handling bundle request for \"{0}\".", path);
            return bundles.FindBundlesContainingPath(path).OfType<T>().FirstOrDefault();
        }

        void SendNotModified(string actualETag)
        {
            CacheLongTime(actualETag); // Some browsers seem to require a reminder to keep caching?!
            response.StatusCode = 304; // Not Modified
            response.SuppressContent = true;
        }

        void SendBundle(Bundle bundle, string actualETag)
        {
            response.ContentType = bundle.ContentType;
            CacheLongTime(actualETag);

            var encoding = request.Headers["Accept-Encoding"];
            response.Filter = EncodeStreamAndAppendResponseHeaders(response.Filter, encoding);
            
            using (var assetStream = bundle.OpenStream())
            {
                assetStream.CopyTo(response.OutputStream);
            }
        }

        void CacheLongTime(string actualETag)
        {
            response.Cache.SetCacheability(HttpCacheability.Public);
            response.Cache.SetExpires(DateTime.UtcNow.AddYears(1));
            response.Cache.SetETag(actualETag);
        }

        Stream EncodeStreamAndAppendResponseHeaders(Stream stream, string encoding)
        {
            if (encoding == null)
            {
                return stream;
            }

            foreach (var type in encoding.Split(',')) {
                var typeComponents = type.Split(';');
                var typeEncoding = typeComponents[0].Trim();
                float qvalue = 1.0f;  // TODO: Prioritize qvalue and implement remaining RFC 2616, section 14.3
                if (typeComponents.Length > 1  && !float.TryParse(typeComponents[1], out qvalue)) {
                    qvalue = 1.0f;
                }
                if (typeEncoding.Equals("deflate", StringComparison.OrdinalIgnoreCase)) {
                    response.AppendHeader("Content-Encoding", "deflate");
                    response.AppendHeader("Vary", "Accept-Encoding");
                    return new DeflateStream(stream, CompressionMode.Compress, true);
                }
                else if (typeEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase)) {
                    response.AppendHeader("Content-Encoding", "gzip");
                    response.AppendHeader("Vary", "Accept-Encoding");
                    return new GZipStream(stream, CompressionMode.Compress, true);
                }

            }
            return stream;
        }
    }
}
