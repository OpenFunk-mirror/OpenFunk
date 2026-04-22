// Adds support for data: URIs to WebRequest
// Based on https://github.com/gregjhogan/WebRequest-data-uri licensed under Apache License 2.0
// Changes made can be found in the git commits

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace DataUri
{
    public class DataWebRequestFactory : IWebRequestCreate
    {
        public static void Register()
        {
            WebRequest.RegisterPrefix("data", new DataWebRequestFactory());
        }

        public WebRequest Create(Uri uri)
        {
            return new DataWebRequest(uri);
        }
    }


    class DataWebRequest : WebRequest
    {
        private readonly Uri uri;

        public override Uri RequestUri => uri;

        public override string Method
        {
            get => "GET";
            set { if (value != "GET") throw new InvalidOperationException("Data URIs only support GET!"); }
        }

        public DataWebRequest(Uri uri)
        {
            this.uri = uri;
        }

        public override WebResponse GetResponse()
        {
            return new DataWebResponse(uri);
        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            DataUriAsyncResult result = new(state);
            callback.Invoke(result);
            return result;
        }

        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            return GetResponse();
        }
    }


    class DataWebResponse : WebResponse
    {
        private readonly Uri uri;
        private readonly string mediatype = "text/plain";
        private readonly Encoding charset = Encoding.ASCII;
        private readonly byte[] data;

        public DataWebResponse(Uri uri)
        {
            this.uri = uri;

            Match match = Regex.Match(
                uri.ToString(),
                "data:(?<mediatype>[^;,]+/[^;,]+)?(?:;charset=(?<charset>[^;,]+))?(?<base64>;base64)?,(?<data>.*)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            string mediatypeValue = match.Groups["mediatype"].Value;
            string charsetValue = match.Groups["charset"].Value;
            string base64Value = match.Groups["base64"].Value;
            string dataValue = match.Groups["data"].Value;

            if (!string.IsNullOrWhiteSpace(mediatypeValue))
                mediatype = mediatypeValue;

            if (!string.IsNullOrWhiteSpace(charsetValue))
                charset = Encoding.GetEncoding(charsetValue);

            if (!string.IsNullOrWhiteSpace(base64Value))
                data = Convert.FromBase64String(dataValue);
            else
                data = charset.GetBytes(dataValue);
        }

        public override Stream GetResponseStream()
        {
            return new MemoryStream(data);
        }

        public override long ContentLength => data.Length;
        public override string ContentType => mediatype;
        public override Uri ResponseUri => uri;
    }


    class DataUriAsyncResult : IAsyncResult
    {
        public DataUriAsyncResult(object asyncState)
        {
            AsyncState = asyncState;
        }

        public object AsyncState { get; }

        public WaitHandle AsyncWaitHandle => new AutoResetEvent(true);

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }
}