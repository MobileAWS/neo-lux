using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System;
using System.Net;

namespace Neo.Lux.Utils
{
    public enum RequestType
    {
        GET,
        POST
    }

    public static class RequestUtils
    {
        public static DataNode Request(RequestType kind, string url, DataNode data = null)
        {
            string contents;

            try
            {
                switch (kind)
                {
                    case RequestType.GET:
                        {
                            contents = GetWebRequest(url); break;
                        }
                    case RequestType.POST:
                        {
                            var paramData = data != null ? JSONWriter.WriteToString(data) : "{}";
                            contents = PostWebRequest(url, paramData);
                            break;
                        }
                    default: return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(contents);
            return root;
        }

        public static string GetWebRequest(string url)
        {
            using (var  client = new TimeoutWebClient { Encoding = System.Text.Encoding.UTF8 })
            {
                return client.DownloadString(url);
            }
        }

        public static string PostWebRequest(string url, string paramData)
        {
            using (var client = new TimeoutWebClient { Encoding = System.Text.Encoding.UTF8 })
            {
                return client.UploadString(url, paramData);
            }
        }
    }
    
    public class TimeoutWebClient : WebClient
    {
        public int Timeout { get; set; }

        public TimeoutWebClient() : this(5000) { }

        public TimeoutWebClient(int timeout)
        {
            this.Timeout = timeout;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request != null)
            {
                request.Timeout = this.Timeout;
            }
            return request;
        }
    }
}
