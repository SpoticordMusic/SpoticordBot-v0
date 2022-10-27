using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace DiscordMusicBot.Services.Helpers
{
    public class SpotifyWebClient
    {
        public static SpotifyWebResponse Post(string URL, JToken PostData, string Token = null, string UserAgent = "C# WServer Request", Dictionary<string, string> Headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            byte[] PostBytes = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(PostData));

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = PostBytes.Length;
            request.Timeout = 10000;
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = false;
            request.PreAuthenticate = true;

            var watch = Stopwatch.StartNew();

            if (!(Token is null)) request.Headers.Add("Authorization", "Bearer " + Token);

            if (Headers != null)
            {
                foreach (var kvp in Headers)
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            using (Stream s = request.GetRequestStream())
            {
                s.Write(PostBytes, 0, PostBytes.Length);
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();

                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = true;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;

                if (response is null) return new SpotifyWebResponse() { RequestSuccess = false };

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    return Post(URL, PostData, Token, UserAgent, Headers);
                }

                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = false;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
        }

        public static SpotifyWebResponse Post(string URL, string PostData, string Token = null, string UserAgent = "C# WServer Request", Dictionary<string, string> Headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            byte[] PostBytes = Encoding.ASCII.GetBytes(PostData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = PostBytes.Length;
            request.Timeout = 10000;
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = false;
            request.PreAuthenticate = true;

            var watch = Stopwatch.StartNew();

            if (!(Token is null)) request.Headers.Add("Authorization", "Basic " + Token);

            if (Headers != null)
            {
                foreach (var kvp in Headers)
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            using (Stream s = request.GetRequestStream())
            {
                s.Write(PostBytes, 0, PostBytes.Length);
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = true;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;

                if (response is null) return new SpotifyWebResponse() { RequestSuccess = false };

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    return Post(URL, PostData, Token, UserAgent, Headers);
                }

                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = false;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
        }

        public static SpotifyWebResponse Get(string URL, string Token = null, string UserAgent = "C# WServer Request", Dictionary<string, string> Headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            request.Method = "GET";
            request.Timeout = 10000;
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = false;
            request.PreAuthenticate = true;

            var watch = Stopwatch.StartNew();

            if (!(Token is null)) request.Headers.Add("Authorization", "Bearer " + Token);

            if (Headers != null)
            {
                foreach (var kvp in Headers)
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = true;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;

                if (response is null) return new SpotifyWebResponse() { RequestSuccess = false };

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    return Get(URL, Token, UserAgent, Headers);
                }

                try
                {
                    using (Stream s = response.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            string ResponseBody = sr.ReadToEnd();

                            watch.Stop();

                            SpotifyWebResponse webResponse = new SpotifyWebResponse();
                            webResponse.RequestSuccess = false;
                            webResponse.ResponseCode = response.StatusCode.ToString("d");
                            webResponse.ResponseMessage = response.StatusDescription;
                            webResponse.ResponseBody = ResponseBody;
                            webResponse.ResponseTime = watch.ElapsedMilliseconds;

                            return webResponse;
                        }
                    }
                } catch
                {
                    return new SpotifyWebResponse()
                    {
                        RequestSuccess = false
                    };
                }
            }
        }

        public static SpotifyWebResponse Put(string URL, string Token = null, string UserAgent = "C# WServer Request", Dictionary<string, string> Headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            request.Method = "PUT";
            request.Timeout = 10000;
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = false;
            request.PreAuthenticate = true;

            var watch = Stopwatch.StartNew();

            if (!(Token is null)) request.Headers.Add("Authorization", "Bearer " + Token);

            if (Headers != null)
            {
                foreach (var kvp in Headers)
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = true;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;

                if (response is null) return new SpotifyWebResponse() { RequestSuccess = false };

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    return Put(URL, Token, UserAgent, Headers);
                }

                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = false;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
        }

        public static SpotifyWebResponse Put(string URL, JToken PostData, string Token = null, string UserAgent = "C# WServer Request", Dictionary<string, string> Headers = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

            byte[] PostBytes = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(PostData));

            request.Method = "PUT";
            request.ContentType = "application/json";
            request.ContentLength = PostBytes.Length;
            request.Timeout = 10000;
            request.UserAgent = UserAgent;
            request.AllowAutoRedirect = false;
            request.PreAuthenticate = true;

            var watch = Stopwatch.StartNew();

            if (!(Token is null)) request.Headers.Add("Authorization", "Bearer " + Token);

            if (Headers != null)
            {
                foreach (var kvp in Headers)
                {
                    request.Headers.Add(kvp.Key, kvp.Value);
                }
            }

            using (Stream s = request.GetRequestStream())
            {
                s.Write(PostBytes, 0, PostBytes.Length);
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = true;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;

                if (response is null) return new SpotifyWebResponse() { RequestSuccess = false };

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    return Put(URL, PostData, Token, UserAgent, Headers);
                }

                using (Stream s = response.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(s))
                    {
                        string ResponseBody = sr.ReadToEnd();

                        watch.Stop();

                        SpotifyWebResponse webResponse = new SpotifyWebResponse();
                        webResponse.RequestSuccess = false;
                        webResponse.ResponseCode = response.StatusCode.ToString("d");
                        webResponse.ResponseMessage = response.StatusDescription;
                        webResponse.ResponseBody = ResponseBody;
                        webResponse.ResponseTime = watch.ElapsedMilliseconds;

                        return webResponse;
                    }
                }
            }
        }

        public struct SpotifyWebResponse
        {
            public bool RequestSuccess;

            public string ResponseCode;
            public string ResponseMessage;

            public string ResponseBody;

            public long ResponseTime;
        }
    }

    public class SpotifyWebPostData
    {
        private Dictionary<string, string> keyValuePairs;

        private SpotifyWebPostData()
        {
            keyValuePairs = new Dictionary<string, string>();
        }

        public SpotifyWebPostData Insert(string Key, string Value)
        {
            keyValuePairs.Add(Key, Value);

            return this;
        }

        public SpotifyWebPostData Remove(string Key)
        {
            keyValuePairs.Remove(Key);

            return this;
        }

        public string Build()
        {
            string ReturnData = "";

            foreach (var entry in keyValuePairs)
            {
                ReturnData += entry.Key + "=" + Uri.EscapeUriString(entry.Value) + "&";
            }

            return ReturnData.Substring(0, ReturnData.Length - 1);
        }

        public static SpotifyWebPostData Builder()
        {
            return new SpotifyWebPostData();
        }
    }
}
