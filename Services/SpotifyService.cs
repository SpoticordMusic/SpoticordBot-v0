using AngleSharp.Html.Parser;
using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Client;
using DiscordMusicBot.Client.Config;
using DiscordMusicBot.Services.DBModels;
using DiscordMusicBot.Services.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Victoria;
using Websocket.Client;

namespace DiscordMusicBot.Services
{
    public class SpotifyService
    {
        private MusicService _musicService;
        private DatabaseService _databaseService;
        private DiscordSocketClient _discordClient;

        private List<ulong> _movingList;

        public SpotifyService(MusicService musicService, DatabaseService databaseService, DiscordSocketClient discordClient)
        {
            _musicService = musicService;
            _databaseService = databaseService;
            _discordClient = discordClient;

            _movingList = new List<ulong>();
        }

        /// <summary>
        /// Check if a Discord user has their spotify linked
        /// </summary>
        /// <param name="discordId"></param>
        /// <returns></returns>
        public bool HasInstance(ulong discordId)
        {
            if (_musicService.SpotifyInstances.ContainsKey(discordId)) return true;

            var __token = _databaseService.GetToken(discordId);
            if (!(__token is null))
            {
                _musicService.SpotifyInstances.Add(discordId, new SpotifyInstance(_databaseService, _musicService, this, __token, 
                    BotConfig.GetFor(MusicBotClient.BOT_BUILD).GetSpotifyCreds()));

                return true;
            }

            return false;
        }

        /// <summary>
        /// Unlink the linked Spotify account from a Discord user
        /// </summary>
        /// <param name="discordId"></param>
        /// <returns></returns>
        public async Task UnlinkInstance(ulong discordId)
        {
            if (_musicService.SpotifyInstances.ContainsKey(discordId))
            {
                await _musicService.SpotifyInstances[discordId].Stop();
                _musicService.SpotifyInstances.Remove(discordId);
            }

            _databaseService.DeleteToken(discordId);
        }

        /// <summary>
        /// Initialize the SpotifyService class
        /// </summary>
        /// <returns></returns>
        public Task InitializeAsync()
        {
            _discordClient.UserVoiceStateUpdated += UserVoiceStateUpdated;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Force a user their spotify instance to start if they have any
        /// </summary>
        /// <param name="user">The user in question</param>
        /// <returns></returns>
        public async Task ActivateUserAsync(SocketGuildUser user)
        {
            if (!_musicService.SpotifyInstances.ContainsKey(user.Id)) return;

            try
            {
                await _musicService.SpotifyInstances[user.Id].Start(user);
            } catch { }
        }

        /// <summary>
        /// A SocketUser changed voice state in a guild
        /// </summary>
        /// <param name="user">The user that has changed their voice state</param>
        /// <param name="previousState">Their previous state</param>
        /// <param name="currentState">Their current state</param>
        /// <returns></returns>
        private async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState currentState)
        {
            // Bot changed state
            if (user.Id == _discordClient.CurrentUser.Id)
            {
                // Moved from null to null?
                if (currentState.VoiceChannel is null && previousState.VoiceChannel is null) return;

                SocketGuild guild = null;

                if (!(previousState.VoiceChannel is null))
                {
                    guild = previousState.VoiceChannel.Guild;
                    var txtChannel = _musicService.GetTextChannel(guild);

                    var oldUsers = _musicService.SpotifyInstances.Where((instance) =>
                    {
                        return instance.Value.User?.VoiceChannel?.Id == previousState.VoiceChannel.Id;
                    });

                    if (!(_musicService.GetHost(guild) is null))
                    {
                        _musicService.ClearHost(guild);
                        await _musicService.DisposeSlaves(guild);
                    }

                    foreach (var oldUser in oldUsers) await oldUser.Value?.Stop();

                    if (!(currentState.VoiceChannel is null))
                    {
                        guild = currentState.VoiceChannel.Guild;
                        await _musicService.ConnectAsync(currentState.VoiceChannel, txtChannel, true);

                        foreach (var vcUser in currentState.VoiceChannel.Users)
                        {
                            if (vcUser.Id != _discordClient.CurrentUser.Id)
                            {
                                if (HasInstance(vcUser.Id)) await ActivateUserAsync(vcUser);
                            }
                        }
                    }
                    else
                    {
                        if (!_movingList.Contains(guild.Id))
                            await _musicService.LeaveAsync(guild);
                        else
                            _movingList.Remove(guild.Id);
                    }
                }

                return;
            }

            // User moved away from the Bot VC
            if (currentState.VoiceChannel is null || currentState.VoiceChannel != _musicService.GetVoiceChannel(currentState.VoiceChannel.Guild.Id))
            {
                if (_musicService.SpotifyInstances.ContainsKey(user.Id))
                {
                    await _musicService.SpotifyInstances[user.Id].Stop();
                    return;
                }
            }
            // User moved into Bot VC
            else
            {
                if (_musicService.SpotifyInstances.ContainsKey(user.Id))
                {
                    await _musicService.SpotifyInstances[user.Id].Start(currentState.VoiceChannel.GetUser(user.Id));
                    return;
                }
            }
        }
    
        /// <summary>
        /// Mark a specified guild as moving, meaning that a Join command is trying to move the bot
        /// to another VC
        /// </summary>
        /// <param name="guild"></param>
        public void MarkMoving(IGuild guild)
        {
            _movingList.Add(guild.Id);
        }
    }

    /// <summary>
    /// A Spotify Instance per spotify/discord user
    /// </summary>
    public class SpotifyInstance
    {
        private DatabaseService _databaseService;
        private MusicService _musicService;
        private SpotifyService _spotifyService;

        private DBToken _token;
        private SpotifyCreds _spotifyCreds;

        private WebsocketClient _socket;
        private string _connectionId;
        private string _deviceId;
        private string _deviceName = "Spoticord";
        private bool _needsConnectionId;
        private EventWaitHandle _pingFurfilledEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        private bool _preInitialized = false;
        private bool _initialized = false;
        private bool _shouldRestart = true;
        private bool _isPlayerHost;

        private bool _isPaused;
        private bool _preventNextTrack;
        private ulong _spotifyDuration;
        private ulong _pausedPosition;

        private SpotifyStateManager _stateManager;

        public SocketGuildUser User { get; 
            private set; }

        public YouTubeTrackMeta YouTubeTrackMeta { get; private set; }
        public SpotifyTrackMeta SpotifyTrackMeta { get; private set; }

        /// <summary>
        /// Get the track position expressed in Spotify time
        /// </summary>
        public ulong SpotifyPosition
        {
            get
            {
                try
                {
                    var player = _musicService.GetPlayer(User.Guild);

                    if (player.Track is null) throw new Exception();

                    return YouTubeTimeToSpotifyTime((ulong)player.Track.Position.TotalMilliseconds);
                } catch { }
                return 0;
            }
        }

        /// <summary>
        /// Get the track position expressed in YouTube time
        /// </summary>
        public ulong YouTubePosition
        {
            get
            {
                try
                {
                    var player = _musicService.GetPlayer(User.Guild);

                    if (player.Track is null) throw new Exception();

                    return (ulong)player.Track.Position.TotalMilliseconds;
                } catch { }
                return 0;
            }
        }

        /// <summary>
        /// Get the song length for the current song on Spotify
        /// </summary>
        public ulong CurrentTrackSpotifyDuration
        {
            get
            {
                return _spotifyDuration;
            }
        }

        /// <summary>
        /// Get the song length for the current song on YouTube
        /// </summary>
        public ulong CurrentTrackYouTubeDuration { get; private set; }

        /// <summary>
        /// Get wether playback has been paused or not
        /// </summary>
        public bool Paused
        {
            get
            {
                return _isPaused;
            }
        }

        public SpotifyInstance(DatabaseService databaseService, MusicService musicService, SpotifyService spotifyService, DBToken token, SpotifyCreds spotifyCreds)
        {
            _databaseService = databaseService;
            _musicService = musicService;
            _spotifyService = spotifyService;
            _token = token;
            _spotifyCreds = spotifyCreds;

            // Create random device id
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            _deviceId = new string(Enumerable.Repeat(chars, 40)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Start the spotify instance with a guild user
        /// </summary>
        /// <param name="user">Target user</param>
        /// <returns></returns>
        public async Task Start(SocketGuildUser user)
        {
            // Check if already initialized
            if (_initialized) return;

            Console.WriteLine($"[SpotifyInstance.Start][INFO] Called by {user.Username}#{user.Discriminator}");

            // Setup variables
            User = user;

            // Check if we have the required scopes
            var response = SpotifyWebClient.Get("https://api.spotify.com/v1/melody/v1/check_scope?scope=web-playback", _token.SpotifyAccessToken);

            if (!response.RequestSuccess)
            {
                if (!RefreshAccessToken())
                {
                    Console.WriteLine($"[SpotifyInstance.Start][ERROR] RefreshAccessToken failed for {User.Username}#{User.Discriminator}");

                    _spotifyService.UnlinkInstance(user.Id);
                    return;
                }

                response = SpotifyWebClient.Get("https://api.spotify.com/v1/melody/v1/check_scope?scope=web-playback", _token.SpotifyAccessToken);

                if (!response.RequestSuccess)
                {
                    Console.WriteLine($"[SpotifyInstance.Start][ERROR] check_scope failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");
                    return;
                }
            }

            // Create a WebSocket relationship with spotify
            _socket = new WebsocketClient(new Uri($"wss://gew-dealer.spotify.com/?access_token={_token.SpotifyAccessToken}"));

            _socket.ErrorReconnectTimeout = null;
            _socket.ReconnectTimeout = null;

            _socket.MessageReceived.Subscribe(async (e) =>
            {
                try
                {
                    await Socket_OnMessage(e);
                } catch { }
            });

            _socket.DisconnectionHappened.Subscribe(async(e) =>
            {
                await Socket_OnClose(e);
            });

            User = user;
            _needsConnectionId = true;
            _isPlayerHost = false;
            _deviceName = _databaseService.GetBotDisplayName(User.Id.ToString());

            try
            {
                _preInitialized = true;

                await _socket.StartOrFail();

                _initialized = true;
                _shouldRestart = true;
            } catch (Exception ex)
            {
                _shouldRestart = false;

                Console.WriteLine($"[SpotifyInstance.Start][ERROR] _socket.StartOrFail failed for {User.Username}#{User.Discriminator}: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return;
            }

            Socket_OnOpen();
        }

        /// <summary>
        /// Stop the spotify instance, removing the bot in their spotify connect
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
        {
            if (!_initialized) return;

            _shouldRestart = false;

            try
            {
                if (_isPlayerHost)
                {
                    await _musicService.StopAsync(User.Guild);
                    _musicService.ClearHost(User.Guild);

                    _musicService.ReleaseSlaves(User.Guild);

                    _preventNextTrack = true;
                }

                _socket?.Dispose();
            }
            catch { }

            Console.WriteLine($"User {User?.Username ?? "UNKNOWN"} performed Stop");
        }

        /// <summary>
        /// Play a spotify track on this instance
        /// </summary>
        /// <param name="spotifyUri"></param>
        public void PlayTrack(string spotifyUri, ulong spotifyDuration, ulong youtubeDuration, ulong msOffset = 0)
        {
            _spotifyDuration = spotifyDuration;
            CurrentTrackYouTubeDuration = youtubeDuration;

            msOffset = YouTubeTimeToSpotifyTime(msOffset);

            JObject payload = new JObject()
            {
                { "uris", new JArray()
                {
                    spotifyUri
                } },
                { "position_ms", msOffset }
            };

            var response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/play", payload, _token.SpotifyAccessToken);
        
            if (!response.RequestSuccess)
            {
                if (response.ResponseCode == "404")
                {
                    _socket.Dispose();
                    return;
                }

                if (!RefreshAccessToken())
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] RefreshAccessToken failed for {User.Username}#{User.Discriminator}");

                    _spotifyService.UnlinkInstance(User.Id);
                    return;
                }

                response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/play", payload, _token.SpotifyAccessToken);

                if (!response.RequestSuccess)
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] /play failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");

                    _socket.Dispose();
                }
            }
        }

        /// <summary>
        /// Seek to a certain point in the currently playing track
        /// </summary>
        /// <param name="msOffset"></param>
        public void SeekTo(ulong msOffset)
        {
            msOffset = YouTubeTimeToSpotifyTime(msOffset);

            var response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/seek?position_ms=" + msOffset.ToString(), _token.SpotifyAccessToken);

            if (!response.RequestSuccess)
            {
                if (response.ResponseCode == "404")
                {
                    _socket.Dispose();
                    return;
                }

                if (!RefreshAccessToken())
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] RefreshAccessToken failed for {User.Username}#{User.Discriminator}");

                    _spotifyService.UnlinkInstance(User.Id);
                    return;
                }

                response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/seek?position_ms=" + msOffset.ToString(), _token.SpotifyAccessToken);

                if (!response.RequestSuccess)
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] /seek failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");

                    _socket.Dispose();
                }
            }
        }

        /// <summary>
        /// Pause the currently playing spotify track
        /// </summary>
        public void PausePlayback()
        {
            var response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/pause", _token.SpotifyAccessToken);

            if (!response.RequestSuccess)
            {
                if (response.ResponseCode == "404")
                {
                    _socket.Dispose();
                    return;
                }

                if (!RefreshAccessToken())
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] RefreshAccessToken failed for {User.Username}#{User.Discriminator}");

                    _spotifyService.UnlinkInstance(User.Id);
                    return;
                }

                response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/pause", _token.SpotifyAccessToken);

                if (!response.RequestSuccess)
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] Pause failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");

                    _socket.Dispose();
                }
            }
        }

        /// <summary>
        /// Resume the currently playing spotify track
        /// </summary>
        /// <returns></returns>
        public void ResumePlayback()
        {            
            var response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/play", _token.SpotifyAccessToken);

            if (!response.RequestSuccess)
            {
                if (response.ResponseCode == "404")
                {
                    _socket.Dispose();
                    return;
                }

                if (!RefreshAccessToken())
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] RefreshAccessToken failed for {User.Username}#{User.Discriminator}");

                    _spotifyService.UnlinkInstance(User.Id);
                    return;
                }

                response = SpotifyWebClient.Put("https://api.spotify.com/v1/me/player/pause", _token.SpotifyAccessToken);

                if (!response.RequestSuccess)
                {
                    Console.WriteLine($"[SpotifyInstance.PlayTrack][ERROR] Resume failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");

                    _socket.Dispose();
                    return;
                }
            }
        }

        /// <summary>
        /// On WebSocket disconnect
        /// </summary>
        /// <param name="e"></param>
        private async Task Socket_OnClose(DisconnectionInfo e)
        {
            // Ignore errors
            if (e.Type == DisconnectionType.Error) return;

            _pauseWaitHandle.Set();

            _initialized = false;
            _preInitialized = false;

            if (_isPlayerHost)
            {
                _preventNextTrack = true;

                _musicService.ClearHost(User.Guild);
                await _musicService.StopAsync(User.Guild);

                _musicService.ReleaseSlaves(User.Guild);
            }
            else
            {
                if (_musicService.HasSlave(User.Guild, this))
                {
                    _preventNextTrack = true;

                    _musicService.RemoveSlave(User.Guild, this);
                }
            }

            if (_shouldRestart)
            {
                await Task.Delay(1000);
                await Start(User);
            }
        }

        /// <summary>
        /// Refresh the access token by using the user their refresh token
        /// </summary>
        /// <returns></returns>
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

            _databaseService.UpdateAccessToken(_token.DiscordID, AccessToken);
            _token.SpotifyAccessToken = AccessToken;

            return true;
        }

        /// <summary>
        /// On WebSocket message received
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private async Task Socket_OnMessage(ResponseMessage e)
        {
            if (!_initialized && !_preInitialized)
            {
                Console.WriteLine("LOL, not initialized!?");
                return;
            }

            var data = JToken.Parse(e.Text);

            // Handle Ping-Pong communication
            if ((string)data["type"] == "pong")
            {
                _pingFurfilledEvent.Set();
                return;
            }

            // Establish Spotify-Connect Relationship if not done yet
            if (_needsConnectionId)
            {
                if ((string)data["type"] == "message" && (string)data["method"] == "PUT" && data["headers"]["Spotify-Connection-Id"] != null)
                {
                    _connectionId = (string)data["headers"]["Spotify-Connection-Id"];
                    _needsConnectionId = false;

                    var @params = SpotifyWebPostData.Builder()
                        .Insert("connection_id", _connectionId)
                        .Build();

                    // Subscribe to WebSocket notifications
                    var notifResponse = SpotifyWebClient.Put($"https://api.spotify.com/v1/me/notifications/user?{@params}", _token.SpotifyAccessToken);

                    if (!notifResponse.RequestSuccess)
                    {
                        Console.WriteLine($"[SpotifyInstance.$.Socket_OnMessage][ERROR] /notifications/user failed for {User.Username}#{User.Discriminator} ({notifResponse.ResponseCode})");

                        await Stop();
                        return;
                    }

                    // Test for notification flags
                    var flagTestResponse = SpotifyWebClient.Get("https://api.spotify.com/v1/me/feature-flags?tests=tps_send_all_state_updates", _token.SpotifyAccessToken);

                    if (!flagTestResponse.RequestSuccess)
                    {
                        Console.WriteLine($"[SpotifyInstance.$.Socket_OnMessage][ERROR] /feature-flags failed for {User.Username}#{User.Discriminator} ({flagTestResponse.ResponseCode})");

                        await Stop();
                        return;
                    }

                    var postData = new JObject() {
                            { "client_version", "harmony:3.19.1-441cc8f" },
                            { "connection_id", _connectionId },
                            { "device", new JObject() {
                                { "brand", "public_js-sdk" },
                                { "capabilities", new JObject() {
                                    { "audio_podcasts", true},
                                    { "change_volume", true},
                                    { "disable_connect", false },
                                    { "enable_play_token", true},
                                    { "manifest_formats", new JArray() { "file_urls_mp3", "file_urls_external", "file_ids_mp4", "file_ids_mp4_dual" } },
                                    { "play_token_lost_behavior", "pause"},
                                } },
                                { "device_id", _deviceId },
                                { "device_type", "speaker"},
                                { "metadata", new JObject() { } },
                                { "model", "harmony-chrome.86-windows" },
                                { "name", (MusicBotClient.BOT_BUILD == BotConfig.BotBuild.Livedev ? "[Livedev] " : "") + _deviceName }
                            } },
                            { "previous_session_state", null },
                            { "volume", 65535 }
                        };

                    // Create a Spotify-Connect device for the user
                    SpotifyWebClient.SpotifyWebResponse response = SpotifyWebClient.Post("https://api.spotify.com/v1/track-playback/v1/devices", postData, _token.SpotifyAccessToken);

                    if (!response.RequestSuccess)
                    {
                        Console.WriteLine($"[SpotifyInstance.$.Socket_OnMessage][ERROR] Device creation failed for {User.Username}#{User.Discriminator} ({response.ResponseCode})");

                        await Stop();
                        return;
                    }

                    var deviceData = JObject.Parse(response.ResponseBody);

                    // Create the State Machine for sending callbacks to the Spotify servers
                    _stateManager = new SpotifyStateManager(_deviceId, (ulong)deviceData["initial_seq_num"], _token, _spotifyCreds);

                    postData = new JObject()
                        {
                            { "seq_num", null },
                            { "command_id", "" },
                            { "volume", 65535 }
                        };

                    // Set volume to max cuz we are like that n stuff
                    SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{_deviceId}/volume", postData, _token.SpotifyAccessToken);
                }
            }

            // Check if WebSocket message is a playback command
            // We can ignore all other commands (e.g. playlist updated)
            if ((string)data["uri"] == "hm://track-playback/v1/command")
            {
                // Loop through payloads if there are multiple
                foreach (JObject cmd in data["payloads"])
                {
                    if ((string)cmd["type"] == "replace_state")
                    {
                        _stateManager.ReplaceState(cmd.ToString());

                        // Check if our playback has been revoked (spotify switched to other device)
                        if (!_stateManager.IsPlayingDevice)
                        {
                            _stateManager.ResetStoredStateID();

                            // Stop the music if we are the host
                            if (_isPlayerHost)
                            {
                                _musicService.ClearHost(User.Guild);
                                await _musicService.StopAsync(User.Guild);

                                _musicService.ReleaseSlaves(User.Guild);

                                _preventNextTrack = true;
                                _socket.Dispose();
                            }
                            else
                            {
                                if (_musicService.HasSlave(User.Guild, this))
                                {
                                    _musicService.RemoveSlave(User.Guild, this);

                                    _preventNextTrack = true;
                                    _socket.Dispose();
                                }
                            }

                            return;
                        }
                        else
                        {
                            if (_musicService.GetHost(User.Guild) is null)
                            {
                                _isPlayerHost = true;
                                _musicService.SetHost(User.Guild, this);
                            }
                            else if (_musicService.GetHost(User.Guild).User.Id == User.Id)
                            {
                                _isPlayerHost = true;
                            }
                            else
                            {
                                if (!_musicService.HasSlave(User.Guild, this))
                                    await _musicService.AddSlave(User.Guild, this);

                                _isPlayerHost = false;
                            }

                            // If requested track is the same as the current playing track
                            //  we need to handle play/pause and seek commands
                            if (_stateManager.StatesMatch())
                            {
                                // If not playing, ignore
                                if (!_musicService.IsPlaying(User.Guild)) return;

                                bool emitHappened = false;

                                // Check if pause state changed
                                if (_stateManager.Paused != _isPaused)
                                {
                                    _isPaused = _stateManager.Paused;
                                    if (_isPaused)
                                    {
                                        // The player was paused

                                        var pos = (ulong)_musicService.GetPlayer(User.Guild).Track.Position.TotalMilliseconds;

                                        if (_isPlayerHost)
                                        {
                                            await _musicService.PauseAsync(User.Guild);

                                            _ = Task.Factory.StartNew(OnPlaybackSilenced);
                                        }

                                        _pausedPosition = pos;
                                        _stateManager.EmitPaused(YouTubeTimeToSpotifyTime(pos), true);
                                    }
                                    else
                                    {
                                        // The player was resumed

                                        var pos = _pausedPosition;

                                        if (_isPlayerHost)
                                        {
                                            await _musicService.ResumeAsync(User.Guild);

                                            _pauseWaitHandle.Set();
                                        }

                                        _stateManager.EmitPaused(YouTubeTimeToSpotifyTime(pos), false);
                                    }

                                    emitHappened = true;
                                }

                                // Check if user is seeking through song
                                if (_stateManager.IsSeeking)
                                {
                                    // Update Spotify & YouTube stream position

                                    var prevPosition = (ulong)_musicService.GetPlayer(User.Guild).Track.Position.TotalMilliseconds;

                                    if (_isPlayerHost)
                                    {
                                        await _musicService.SeekToAsync(SpotifyTimeToYouTubeTime((ulong)_stateManager.SeekTo.Value), User.Guild);
                                    }

                                    _pausedPosition = SpotifyTimeToYouTubeTime((ulong)_stateManager.SeekTo.Value);

                                    _stateManager.EmitPositionChanged((ulong)_stateManager.SeekTo.Value, YouTubeTimeToSpotifyTime(prevPosition));

                                    emitHappened = true;
                                }

                                if (!emitHappened)
                                {
                                    var pos = (ulong)_musicService.GetPlayer(User.Guild).Track.Position.TotalMilliseconds;

                                    _stateManager.EmitModify(YouTubeTimeToSpotifyTime(pos));
                                }

                                return;
                            }

                            // Update our current state
                            _stateManager.ApplyCurrentStateID();
                            
                            _isPaused = _stateManager.Paused;

                            _stateManager.EmitBeforeTrackLoad();

                            // Play track on the LavaPlayer if we are host
                            if (_isPlayerHost)
                            {
                                // Play the track in Discord
                                await HostPlayCurrentStateTrack();
                            }

                            // If Spotify's seek is not 0 make sure to update on the go

                            ulong position = 0;
                            if (_stateManager.IsSeeking)
                            {
                                position = (ulong)_stateManager.SeekTo.Value;
                            }

                            if (_isPlayerHost && position > 0)
                            {
                                await _musicService.SeekToAsync(SpotifyTimeToYouTubeTime(position), User.Guild);

                                _pauseWaitHandle.Set();
                            }

                            _pausedPosition = SpotifyTimeToYouTubeTime(position);

                            // Notify all Spotify players to seek (if necessary) and start playback
                            _stateManager.EmitPositionChanged(position, 0);

                            // Check if the player was initialized with pause = true
                            // Make sure YouTube is also paused if this is the case

                            if (_isPaused)
                            {
                                if (_isPlayerHost) await _musicService.PauseAsync(User.Guild);

                                _stateManager.EmitPaused(_pausedPosition, true);

                                _ = Task.Factory.StartNew(OnPlaybackSilenced);
                            }
                            else
                                _pauseWaitHandle.Set();
                        }
                    }
                    else if ((string)cmd["type"] == "set_volume")
                    {
                        // We only update the Spotify volume and not the bot volume
                        //  because lavalink will sound really glitchy while updating the volume
                        //  (and listeners can just change the volume of the bot themselves 😉)

                        float volume = (float)cmd["volume"];

                        SpotifyWebClient.Put($"https://api.spotify.com/v1/track-playback/v1/devices/{_deviceId}/volume", new JObject()
                            {
                                { "seq_num", null },
                                { "command_id", "" },
                                { "volume", volume }
                            }, _token.SpotifyAccessToken);

                        if (_isPlayerHost)
                            await _musicService.SetVolumeAsync(User.Guild, (ushort)(volume / 65535 * 20));
                    }
                    else
                    {
                        // Unknown command, dump JSON to console
                        Console.WriteLine(cmd.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Play a track as the host
        /// </summary>
        /// <returns></returns>
        private async Task HostPlayCurrentStateTrack()
        {
            var query = "";
            var spotifyOnlineUrl = "";

            try
            {
                if (_stateManager.CurrentTrack is null) return;

                query = SpotifyHelper.CreateYoutubeQuery(_stateManager.CurrentTrack.metadata);

                var success = false;
                LavaTrack ltrack = null;

                for (var i = 0; i < 3; i++)
                {
                    ltrack = await _musicService.FindYouTubeTrack(query);
                    if (!(ltrack is null))
                    {
                        success = true;
                        break;
                    }
                }

                if (!success)
                {
                    throw new Exception($"Failed to find YouTube track with query {query} after 3 tries");
                }

                YouTubeTrackMeta = new YouTubeTrackMeta()
                {
                    TrackName = ltrack.Title,
                    TrackURL = ltrack.Url,
                    Uploader = ltrack.Author
                };

                spotifyOnlineUrl = SpotifyHelper.GetTrackURL(_stateManager.CurrentTrack.metadata.uri, _token.SpotifyAccessToken);

                SpotifyTrackMeta = new SpotifyTrackMeta()
                {
                    Author = string.Join(", ", _stateManager.CurrentTrack.metadata.authors.Select((a) => a.name)),
                    TrackName = _stateManager.CurrentTrack.metadata.name,
                    TrackURL = spotifyOnlineUrl,
                    TrackURI = _stateManager.CurrentTrack.metadata.uri
                };

                _spotifyDuration = _stateManager.CurrentTrack.metadata.duration;
                CurrentTrackYouTubeDuration = (ulong)ltrack.Duration.TotalMilliseconds;

                if (User == null)
                {
                    Console.WriteLine($"User variable was NULL");
                    return;
                }

                if (_musicService.IsPlaying(User.Guild))
                {
                    // Make sure the TrackFinished callback is ignored if we replace the current track
                    _preventNextTrack = true;
                }

                // Play the track
                await _musicService.PlayAsync(_stateManager.CurrentTrack.metadata.uri, _spotifyDuration, CurrentTrackYouTubeDuration, ltrack, User.Guild, async (finishedTrack) =>
                {
                    if (_initialized)
                        _ = Task.Factory.StartNew(OnPlaybackSilenced);

                    // Track playback is finished or skipped
                    if (_preventNextTrack || !_initialized)
                    {
                        _preventNextTrack = false;
                        return;
                    }

                    YouTubeTrackMeta = null;
                    SpotifyTrackMeta = null;

                    if (!_stateManager.AdvanceTrack() || User == null) return;

                    _stateManager.ApplyCurrentStateID();
                    _stateManager.EmitBeforeTrackLoad();

                    _isPaused = _stateManager.Paused;

                    await HostPlayCurrentStateTrack();

                    _pausedPosition = 0;

                // Notify all Spotify players to seek (if necessary) and start playback
                _stateManager.EmitPositionChanged(0, 0);

                // Check if the player was initialized with pause = true
                // Make sure YouTube is also paused if this is the case

                if (_stateManager.Paused)
                    {
                        await _musicService.PauseAsync(User.Guild);

                        _stateManager.EmitPaused(_pausedPosition, true);

                        _ = Task.Run(() => OnPlaybackSilenced());
                    }
                    else
                        _pauseWaitHandle.Set();
                });

                _pauseWaitHandle.Set();
            } catch (Exception ex)
            {
                Console.WriteLine("[SpotifyService.HostPlayCurrentStateTrack][ERROR] " + ex.Message);

                if (User != null)
                {
                    Console.WriteLine("[User] = " + User.Username);
                }

                Console.WriteLine($"query = \"{query}\"");
                Console.WriteLine($"metadata = \"{(_stateManager.CurrentTrack.metadata == null ? "null" : "Object[metadata]")}\"");
                Console.WriteLine($"spotifyOnlineUrl = \"{spotifyOnlineUrl}\"");
                Console.WriteLine($"YoutubeTrackMeta = \"{(YouTubeTrackMeta == null ? "null" : "Object[YoutubeTrackMeta]")}\"");
                Console.WriteLine($"SpotifyTrackMeta = \"{(SpotifyTrackMeta == null ? "null" : "Object[YoutubeTrackMeta]")}\"");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private EventWaitHandle _pauseWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        /// <summary>
        /// Fires when the host pauses their Spotify playback or stops playing (but retaining host)
        /// </summary>
        /// <returns></returns>
        private void OnPlaybackSilenced()
        {
            if (_musicService.Is247(User.Guild) && ((SocketVoiceChannel)_musicService.GetVoiceChannel(User.Guild.Id)).Users.Count == 2) return;

            _pauseWaitHandle.Set();
            _pauseWaitHandle.Reset();

            if (!_pauseWaitHandle.WaitOne(1000 * 60 * 5 / 2))
            {
                if (_musicService.Is247(User.Guild) && ((SocketVoiceChannel)_musicService.GetVoiceChannel(User.Guild.Id)).Users.Count == 2) return;

                _socket.Dispose();
            }
        }

        /// <summary>
        /// Gets called when 24/7 mode has been disabled
        /// </summary>
        public void OnDisabled247()
        {
            if (!_isPlayerHost) return;

            if (Paused)
            {
                _ = Task.Run(() => OnPlaybackSilenced());
            }
        }

        /// <summary>
        /// Call the _socket.Dispose function
        /// </summary>
        public void CloseSocket()
        {
            _socket.Dispose();
        }

        /// <summary>
        /// Convert Spotify song playback time to YouTube song playback time
        /// </summary>
        /// <param name="time">The time elapsed in Spotify</param>
        /// <returns></returns>
        private ulong SpotifyTimeToYouTubeTime(ulong time)
        {
            return (ulong)(((float)time / _spotifyDuration) * CurrentTrackYouTubeDuration);
        }

        /// <summary>
        /// Convert YouTube song playback time to Spotify song playback time
        /// </summary>
        /// <param name="time">The time elapsed in YouTube</param>
        /// <returns></returns>
        private ulong YouTubeTimeToSpotifyTime(ulong time)
        {
            return (ulong)((float)time / CurrentTrackYouTubeDuration * _spotifyDuration);
        }

        /// <summary>
        /// WebSocket connection was opened
        /// </summary>
        private void Socket_OnOpen()
        {
            // Create the pinger thread
            new Thread(() =>
            {
                while (true)
                {
                    Thread.Sleep(30000);

                    if (!_socket.IsRunning) return;

                    Socket_Ping();
                }
            }).Start();
        }

        /// <summary>
        /// Send ping to Spotify server
        /// </summary>
        private void Socket_Ping()
        {
            _socket.Send(JsonConvert.SerializeObject(new
            {
                type = "ping"
            }));

            // If pong is not received within 5 seconds, kill the WebSocket
            if (!_pingFurfilledEvent.WaitOne(5000))
            {
                _socket.Dispose();
            }
        }
    }

    public class SpotifyTrackMeta
    {
        public string Author { get; set; }
        public string TrackName { get; set; }
        public string TrackURL { get; set; }
        public string TrackURI { get; set; }
    }

    public class YouTubeTrackMeta
    {
        public string Uploader { get; set; }
        public string TrackName { get; set; }
        public string TrackURL { get; set; }
    }

    public class GeniusMeta
    {
        public int Status { get; set; }
        public string Lyrics { get; set; }
    }
}
