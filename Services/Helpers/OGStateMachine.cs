using DiscordMusicBot.Client.Config;
using DiscordMusicBot.Services.DBModels;
using Newtonsoft.Json.Linq;
using System;
using System.Text;

namespace DiscordMusicBot.Services.Helpers
{
    public class OGStateMachine
    {
        private ulong _sequenceNumber;
        private DBToken _token;
        private SpotifyCreds _spotifyCreds;

        public event Action<string> UpdatedAccessToken;

        public OGStateMachine(ulong initialSequenceNumber, DBToken token, SpotifyCreds spotifyCreds)
        {
            _sequenceNumber = initialSequenceNumber;
            _token = token;
            _spotifyCreds = spotifyCreds;
        }

        public bool EmitBeforeTrackLoad(string deviceId, StateRef stateRef, SubState subState)
        {
            _sequenceNumber++;

            JObject payload = new JObject()
            {
                { "seq_num", _sequenceNumber },
                { "state_ref", new JObject(){
                    { "paused", stateRef.paused },
                    { "state_id", stateRef.state_id },
                    { "state_machine_id", stateRef.state_machine_id }
                } },
                { "sub_state", new JObject() {
                    { "stream_time", subState.stream_time },
                    { "position", subState.position },
                    { "playback_speed", subState.playback_speed },
                    { "duration", subState.duration }
                }},
                { "debug_source", "before_track_load" }
            };

            var results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

            if (!results.RequestSuccess)
            {
                if (!RefreshAccessToken())
                    return false;

                results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

                if (!results.RequestSuccess)
                    return false;
            }

            return true;
        }

        public bool EmitPositionChanged(string deviceId, ulong previousPosition, StateRef stateRef, SubState subState)
        {
            _sequenceNumber++;

            JObject payload = new JObject()
            {
                { "seq_num", _sequenceNumber },
                { "previous_position", previousPosition },
                { "state_ref", new JObject(){
                    { "paused", stateRef.paused },
                    { "state_id", stateRef.state_id },
                    { "state_machine_id", stateRef.state_machine_id }
                } },
                { "sub_state", new JObject() {
                    { "stream_time", subState.stream_time },
                    { "position", subState.position },
                    { "playback_speed", subState.playback_speed },
                    { "duration", subState.duration }
                }},
                { "debug_source", "position_changed" }
            };

            var results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

            if (!results.RequestSuccess)
            {
                if (!RefreshAccessToken())
                    return false;

                results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

                if (!results.RequestSuccess)
                    return false;
            }

            return true;
        }

        public bool ModifyCurrentState(string deviceId, ulong previousPosition, StateRef stateRef, SubState subState)
        {
            _sequenceNumber++;

            JObject payload = new JObject()
            {
                { "seq_num", _sequenceNumber },
                { "previous_position", previousPosition },
                { "state_ref", new JObject(){
                    { "paused", stateRef.paused },
                    { "state_id", stateRef.state_id },
                    { "state_machine_id", stateRef.state_machine_id }
                } },
                { "sub_state", new JObject() {
                    { "stream_time", subState.stream_time },
                    { "position", subState.position },
                    { "playback_speed", subState.playback_speed },
                    { "duration", subState.duration }
                }},
                { "debug_source", "modify_current_state" }
            };

            var results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

            if (!results.RequestSuccess)
            {
                if (!RefreshAccessToken())
                    return false;

                results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

                if (!results.RequestSuccess)
                    return false;
            }

            return true;
        }

        public bool TrackDataFinalized(string deviceId, ulong previousPosition, StateRef stateRef, SubState subState)
        {
            _sequenceNumber++;

            JObject payload = new JObject()
            {
                { "seq_num", _sequenceNumber },
                { "previous_position", previousPosition },
                { "playback_stats", new JObject() {
                    { "audiocodec", "mp4" },
                    { "key_system", "widevine" },
                    { "local_time_ms", DateTime.UtcNow.Millisecond },
                    { "max_ms_seek_rebuffering", 3820 },
                    { "max_ms_stalled", 0 },
                    { "ms_initial_buffering", 6372 },
                    { "ms_key_latency", 615 },
                    { "ms_latency", 6372 },
                    { "ms_manifest_latency", 151 },
                    { "ms_seek_rebuffering", 3938 },
                    { "ms_stalled", 0 },
                    { "ms_total_est", 155082 },
                    { "n_stalls", 0 },
                    { "start_offset_ms", 149333 },
                    { "time_weighted_bitrate", 0 },
                    { "total_bytes", 2118720 },
                } },
                { "state_ref", new JObject(){
                    { "paused", stateRef.paused },
                    { "state_id", stateRef.state_id },
                    { "state_machine_id", stateRef.state_machine_id }
                } },
                { "sub_state", new JObject() {
                    { "stream_time", subState.stream_time },
                    { "position", subState.position },
                    { "playback_speed", subState.playback_speed },
                    { "duration", subState.duration }
                }},
                { "debug_source", "track_data_finalized" }
            };

            var results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

            if (!results.RequestSuccess)
            {
                if (!RefreshAccessToken())
                    return false;

                results = SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{deviceId}/state", payload, _token.SpotifyAccessToken);

                if (!results.RequestSuccess)
                    return false;
            }

            return true;
        }

        private bool RefreshAccessToken()
        {
            var resp = SpotifyWebClient.Post("https://accounts.spotify.com/api/token",
                SpotifyWebPostData.Builder()
                .Insert("grant_type", "refresh_token")
                .Insert("refresh_token", _token.SpotifyRefreshToken)
                .Build(),
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_spotifyCreds.ClientID}:{_spotifyCreds.ClientSecret}")));

            if (!resp.RequestSuccess) return false;

            var TokenInfo = JToken.Parse(resp.ResponseBody);
            var AccessToken = TokenInfo["access_token"].ToString();

            UpdatedAccessToken?.Invoke(AccessToken);

            _token.SpotifyAccessToken = AccessToken;

            return true;
        }

        public struct StateRef
        {
            public string state_machine_id;
            public string state_id;
            public bool paused;
        }

        public struct SubState
        {
            public ulong playback_speed;
            public ulong position;
            public ulong duration;
            public ulong stream_time;
        }
    }
}
