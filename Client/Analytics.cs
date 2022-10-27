using Discord;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DiscordMusicBot.Client
{
    public class Analytics
    {
        public static async Task<bool> CommandExecute(IGuild guild, IUser user, string command)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL($"/bot/cmd_exec")
                    .WithGuild(guild).WithUser(user).WithParam("command", command)
                    .Send();
            } catch { return false; }
        }

        public static async Task<bool> VCJoin(IGuild guild)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL("/bot/voicecall")
                    .WithGuild(guild).WithParam("op", "join")
                    .Send();
            } catch { return false; }
        }

        public static async Task<bool> VCLeave(IGuild guild)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL("/bot/voicecall")
                    .WithGuild(guild).WithParam("op", "leave")
                    .Send();
            } catch { return false; }
        }

        public static async Task<bool> HostAssign(IUser user)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL("/bot/hoststate")
                    .WithUser(user).WithParam("op", "assign")
                    .Send();
            }
            catch { return false; }
        }

        public static async Task<bool> HostRetract(IUser user)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL("/bot/hoststate")
                    .WithUser(user).WithParam("op", "retract")
                    .Send();
            }
            catch { return false; }
        }

        public static async Task<bool> TrackPlay(IGuild guild, IUser user, string ytURL, string spURI, string AuthorString, string TrackName)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL("/bot/trackplay")
                    .WithGuild(guild).WithUser(user)
                    .WithParam("youtube_url", ytURL).WithParam("spotify_uri", spURI)
                    .WithParam("author", AuthorString).WithParam("track_name", TrackName)
                    .Send();
            } catch { return false; }
        }

        public static async Task<bool> GuildJoined(IGuild guild)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL($"/discord/{guild.Id}/destroy")
                    .WithGuild(guild).Send();
            }
            catch { return false; }
        }

        public static async Task<bool> GuildLeft(IGuild guild)
        {
            try
            {
                return await AnalyticsHTTP.Builder().WithURL($"/discord/{guild.Id}/create")
                    .WithGuild(guild).Send();
            }
            catch { return false; }
        }

        private class AnalyticsHTTP
        {
            private const string BASE_URL = "http://localhost:3095";
            
            private string URL;
            private IGuild Guild;
            private IUser User;
            private Dictionary<string, string> Params;

            private AnalyticsHTTP() {
                Params = new Dictionary<string, string>();
            }

            public static AnalyticsHTTP Builder()
            {
                return new AnalyticsHTTP();
            }

            public AnalyticsHTTP WithURL(string URL)
            {
                this.URL = URL;
                return this;
            }

            public AnalyticsHTTP WithGuild(IGuild Guild)
            {
                this.Guild = Guild;
                return this;
            }

            public AnalyticsHTTP WithUser(IUser User)
            {
                this.User = User;
                return this;
            }

            public AnalyticsHTTP WithParam(string paramName, string paramValue)
            {
                if (paramName == "guild" || paramName == "user")
                    throw new ArgumentException($"Parameter may not be {paramName}", "paramName");

                if (Params.ContainsKey(paramName)) Params[paramName] = paramValue;
                else Params.Add(paramName, paramValue);

                return this;
            }

            public async Task<bool> Send()
            {
                if (MusicBotClient.BOT_BUILD == Config.BotConfig.BotBuild.Secondary)
                    return true;

                return (await Post(BASE_URL + URL, FabricateParams())).RequestSuccess;
            }

            private string FabricateParams()
            {
                JObject obj = new JObject();

                if (!(Guild is null))
                    obj["guild"] = Guild.Id.ToString();

                if (!(User is null))
                    obj["user"] = User.Id.ToString();

                foreach (var param in Params)
                {
                    obj[param.Key] = param.Value;
                }

                return obj.ToString();
            }

            private static async Task<WebResponse> Post(string URL, string PostData, Dictionary<string, string> Headers = null)
            {
                return await Task.Run(() =>
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);

                    byte[] PostBytes = Encoding.ASCII.GetBytes(PostData);

                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.ContentLength = PostBytes.Length;
                    request.Timeout = 10000;
                    request.AllowAutoRedirect = false;

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

                                WebResponse webResponse = new WebResponse();
                                webResponse.RequestSuccess = true;
                                webResponse.ResponseCode = response.StatusCode.ToString("d");
                                webResponse.ResponseMessage = response.StatusDescription;
                                webResponse.ResponseBody = ResponseBody;

                                return webResponse;
                            }
                        }
                    }
                    catch (WebException ex)
                    {
                        var response = (HttpWebResponse)ex.Response;

                        if (response is null) return new WebResponse() { RequestSuccess = false };

                        using (Stream s = response.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(s))
                            {
                                string ResponseBody = sr.ReadToEnd();

                                WebResponse webResponse = new WebResponse();
                                webResponse.RequestSuccess = false;
                                webResponse.ResponseCode = response.StatusCode.ToString("d");
                                webResponse.ResponseMessage = response.StatusDescription;
                                webResponse.ResponseBody = ResponseBody;

                                return webResponse;
                            }
                        }
                    }
                });
            }

            public struct WebResponse
            {
                public bool RequestSuccess;

                public string ResponseCode;
                public string ResponseMessage;

                public string ResponseBody;
            }
        }
    }
}
