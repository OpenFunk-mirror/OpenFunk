// Adds support for data: URIs to WebRequest
// Based on https://github.com/gregjhogan/WebRequest-data-uri licensed under Apache License 2.0
// Changes made can be found in the git commits

namespace DataUri
{
    using System;
    using System.Net;

    public sealed class DataWebRequestFactory : IWebRequestCreate
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
}

namespace DataUri
{
    using System;
    using System.Net;

    internal sealed class DataWebRequest : WebRequest
    {
        private readonly Uri uri;

        public DataWebRequest(Uri uri)
        {
            this.uri = uri;
        }

        public override WebResponse GetResponse()
        {
            return new DataWebResponse(this.uri);
        }

        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var result = new DataUriAsyncResult(state);
            callback.Invoke(result);
            return result;
        }

        public override WebResponse EndGetResponse(IAsyncResult asyncResult)
        {
            return this.GetResponse();
        }
    }
}

namespace DataUri
{
    using System;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;

    internal sealed class DataWebResponse : WebResponse
    {
        private readonly string mediatype = "text/plain";
        private readonly Encoding charset = Encoding.ASCII;
        private readonly byte[] data;

        public DataWebResponse(Uri uri)
        {
            var match = Regex.Match(uri.ToString(), "data:(?<mediatype>[^;,]+/[^;,]+)?(?:;charset=(?<charset>[^;,]+))?(?<base64>;base64)?,(?<data>.*)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!string.IsNullOrWhiteSpace(match.Groups["mediatype"].Value))
            {
                this.mediatype = match.Groups["mediatype"].Value;
            }

            if (!string.IsNullOrWhiteSpace(match.Groups["charset"].Value))
            {
                this.charset = Encoding.GetEncoding(match.Groups["charset"].Value);
            }

            if (!string.IsNullOrWhiteSpace(match.Groups["base64"].Value))
            {
                this.data = Convert.FromBase64String(match.Groups["data"].Value);
            }
            else
            {
                this.data = this.charset.GetBytes(match.Groups["data"].Value);
            }
        }

        public override System.IO.Stream GetResponseStream()
        {
            return new System.IO.MemoryStream(this.data);
        }

        public override long ContentLength => this.data.Length;

        public override string ContentType => this.mediatype;
    }
}

namespace DataUri
{
    using System;
    using System.Threading;

    internal sealed class DataUriAsyncResult : IAsyncResult
    {
        public DataUriAsyncResult(object asyncState)
        {
            this.AsyncState = asyncState;
        }

        public object AsyncState { get; }

        public WaitHandle AsyncWaitHandle => new AutoResetEvent(true);

        public bool CompletedSynchronously => true;

        public bool IsCompleted => true;
    }
}