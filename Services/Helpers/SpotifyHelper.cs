using Newtonsoft.Json.Linq;
using System;

namespace DiscordMusicBot.Services.Helpers
{
    public class SpotifyHelper
    {
        public static string CreateYoutubeQuery(SpotifyStateManager.TrackMetadata track)
        {
            string query = "";

            foreach (var author in track.authors)
                query += author.name + ", ";

            query = query.Substring(0, query.Length - 2) + " - ";

            query += track.name;// + " - Lyrics";

            return query;
        }

        public static string GetTrackURL(string uri, string token)
        {
            var res = SpotifyWebClient.Get($"https://api.spotify.com/v1/tracks/{uri.Split(':')[2]}", token);

            if (!res.RequestSuccess)
            {
                Console.WriteLine($"[SpotifyHelper.GetTrackURL] Failed to get track url for {uri}: {res.ResponseCode}");
                return null;
            }

            var jObj = JObject.Parse(res.ResponseBody);

            try
            {
                return (string)jObj["external_urls"]["spotify"];
            } catch (Exception ex)
            {
                Console.WriteLine($"[SpotifyHelper.GetTrackURL] Failed to get track url for {uri}: {ex.Message}\n{res.ResponseBody}");
                return null;
            }
        }
    }
}
