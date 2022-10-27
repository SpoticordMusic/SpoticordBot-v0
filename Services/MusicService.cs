using Discord;
using Discord.WebSocket;
using DiscordMusicBot.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;

namespace DiscordMusicBot.Services
{
    public class MusicService
    {
        private LavaNode _lavaNode;
        private LavaConfig _lavaConfig;
        private DiscordSocketClient _client;
        private Dictionary<ulong, GuildMusicInstance> _instances;

        public Dictionary<ulong, SpotifyInstance> SpotifyInstances { get; private set; }

        public MusicService(LavaNode lavaNode, LavaConfig lavaConfig, DiscordSocketClient client)
        {
            _client = client;
            _lavaNode = lavaNode;
            _lavaConfig = lavaConfig;
            _instances = new Dictionary<ulong, GuildMusicInstance>();
            SpotifyInstances = new Dictionary<ulong, SpotifyInstance>();
        }

        public Task InitializeAsync()
        {
            _client.Ready += ClientReadyAsync;
            _lavaNode.OnTrackEnded += TrackFinished;

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync()
        {
            foreach (var instance in SpotifyInstances)
            {
                try
                {
                    await instance.Value.Stop();
                } catch { }
            }

            foreach (var instance in _instances)
            {
                try
                {
                    if (instance.Value.Player is null) continue;

                    await LeaveAsync(instance.Value.Player.VoiceChannel.Guild);
                }
                catch { }
            }
        }

        /// <summary>
        /// Make sure a <see cref="GuildMusicInstance"/> is assigned to the given <paramref name="guildId"/>
        /// </summary>
        /// <param name="guildId"></param>
        private void MakeSureInstance(ulong guildId)
        {
            lock (_instances)
            {
                if (!_instances.ContainsKey(guildId)) _instances.Add(guildId, new GuildMusicInstance());
            }
        }

        public IVoiceChannel GetVoiceChannel(ulong guildId)
        {
            MakeSureInstance(guildId);
            if (_instances[guildId].Player is null) return null;

            return _instances[guildId].Player.VoiceChannel;
        }

        public bool IsConnected(ulong guildId)
        {
            MakeSureInstance(guildId);
            return !(_instances[guildId].Player is null);
        }

        /// <summary>
        /// Set the player host for a specified guild
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="instance"></param>
        public void SetHost(IGuild guild, SpotifyInstance instance)
        {
            MakeSureInstance(guild.Id);

            _instances[guild.Id].LeaveWaitHandle?.Set();
            _instances[guild.Id].Host = instance;

            _ = Analytics.HostAssign(instance.User);
        }

        /// <summary>
        /// Get the player host in the specified guild
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public SpotifyInstance GetHost(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            return _instances[guild.Id].Host;
        }

        /// <summary>
        /// Remove the player host from the specified guild
        /// </summary>
        /// <param name="guild"></param>
        public void ClearHost(IGuild guild, bool needsClose = false)
        {
            MakeSureInstance(guild.Id);

            if (!(_instances[guild.Id].Host is null))
            {
                _ = Analytics.HostRetract(_instances[guild.Id].Host.User);

                if (needsClose) _instances[guild.Id].Host.CloseSocket();
            }

            _instances[guild.Id].Host = null;

            if (_instances[guild.Id].Player is null) return;

            Task.Run(() => OnPlaybackStopped(guild));
        }

        /// <summary>
        /// Add a slave player to a guild instance
        /// </summary>
        /// <param name="guild">The target guild</param>
        /// <param name="instance">The slave instance</param>
        public Task AddSlave(IGuild guild, SpotifyInstance instance)
        {
            MakeSureInstance(guild.Id);

            if (!_instances[guild.Id].Slaves.Contains(instance))
                _instances[guild.Id].Slaves.Add(instance);

            var host = _instances[guild.Id].Host;

            instance.PlayTrack(host.SpotifyTrackMeta.TrackURI, host.CurrentTrackSpotifyDuration,
                host.CurrentTrackYouTubeDuration, host.SpotifyPosition);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Check wether a certain player slave is in a slaves guild registry
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        public bool HasSlave(IGuild guild, SpotifyInstance instance)
        {
            MakeSureInstance(guild.Id);            
            return _instances[guild.Id].Slaves.Contains(instance);
        }

        /// <summary>
        /// Remove a certain slave from the slaves guild registry
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="instance"></param>
        public void RemoveSlave(IGuild guild, SpotifyInstance instance)
        {
            MakeSureInstance(guild.Id);
            if (!_instances[guild.Id].Slaves.Contains(instance)) return;

            lock (_instances[guild.Id].Slaves)
                _instances[guild.Id].Slaves.Remove(instance);
        }

        /// <summary>
        /// Restart all slave instances
        /// </summary>
        /// <param name="guild"></param>
        public void ReleaseSlaves(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Slaves.Count == 0) return;

            lock(_instances[guild.Id].Slaves)
            {
                foreach(var slave in _instances[guild.Id].Slaves.ToArray())
                {
                    try
                    {
                        slave.CloseSocket();
                    }
                    catch {  }
                }

                _instances[guild.Id].Slaves.Clear();
            }
        }

        /// <summary>
        /// Destroy a slave guild registry
        /// </summary>
        /// <param name="guild"></param>
        public async Task DisposeSlaves(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Slaves.Count == 0) return;

            var copiedArray = new SpotifyInstance[_instances[guild.Id].Slaves.Count];
            lock(_instances[guild.Id].Slaves)
            {
                _instances[guild.Id].Slaves.CopyTo(copiedArray);

                _instances[guild.Id].Slaves.Clear();
            }

            foreach (var slave in copiedArray)
            {
                await slave.Stop();
            }
        }

        /// <summary>
        /// Get the <see cref="ITextChannel"/> from the <see cref="LavaPlayer"/> associated with the <paramref name="guild" />
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public ITextChannel GetTextChannel(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Player is null) return null;

            return _instances[guild.Id].Player.TextChannel;
        }

        public async Task ConnectAsync(SocketVoiceChannel voiceChannel, ITextChannel textChannel, bool ignoreAnalytics = false)
        {
            await _lavaNode.JoinAsync(voiceChannel, textChannel);

            if (!ignoreAnalytics)
                _ = Analytics.VCJoin(voiceChannel.Guild);

            MakeSureInstance(voiceChannel.Guild.Id);
            if (_instances[voiceChannel.Guild.Id].Player is null)
            {
                _instances[voiceChannel.Guild.Id].Player = _lavaNode.GetPlayer(voiceChannel.Guild);

                await SetVolumeAsync(voiceChannel.Guild, 20);

                _ = Task.Factory.StartNew(async () =>
                  {
                      await OnPlaybackStopped(voiceChannel.Guild);
                  });
            }
        }

        /// <summary>
        /// Leave a guilds assigned voice channel
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public async Task LeaveAsync(IGuild guild)
        {
            MakeSureInstance(guild.Id);

            var instance = _instances[guild.Id];

            if (instance.Player is null) return;

            try
            {
                await _lavaNode.LeaveAsync(_instances[guild.Id].Player.VoiceChannel);
            } catch (Exception ex) {
                Console.WriteLine("[LeaveAsync] ERROR: " + ex.Message);
            }

            _instances[guild.Id].Player = null;

            _instances[guild.Id].LeaveWaitHandle?.Set();

            _ = Analytics.VCLeave(guild);
        }

        /// <summary>
        /// Start playing a track, send play command to slaves
        /// </summary>
        /// <param name="spotifyUri">The spotify track URI</param>
        /// <param name="spotifyDuration">The duration of the Spotify track in milliseconds</param>
        /// <param name="youtubeDuration">The duration of the YouTube track in milliseconds</param>
        /// <param name="track">The YouTube Lavatrack object</param>
        /// <param name="guild">The guild in which to play the song</param>
        /// <param name="OnTrackFinished">Callback function that is executed whenever a track finishes playing</param>
        /// <returns></returns>
        public async Task PlayAsync(string spotifyUri, ulong spotifyDuration, ulong youtubeDuration, LavaTrack track, IGuild guild, Action<LavaTrack> OnTrackFinished = null)
        {
            MakeSureInstance(guild.Id);

            LavaPlayer player;
            if (_instances[guild.Id].Player is null) return;
            if (_instances[guild.Id].Host is null) return;
            

            if (_instances[guild.Id].Slaves.Count > 0)
            {
                lock (_instances[guild.Id].Slaves)
                {
                    foreach (var slave in _instances[guild.Id].Slaves)
                    {
                        try
                        {
                            slave.PlayTrack(spotifyUri, spotifyDuration, youtubeDuration);
                        }
                        catch { /* Catch if one of the slaves crashes, continue for all other slaves */ }
                    }
                }
            }

            player = _instances[guild.Id].Player;

            await player.PlayAsync(track);

            _ = Analytics.TrackPlay(guild, _instances[guild.Id].Host.User, track.Url, spotifyUri,
                _instances[guild.Id].Host.SpotifyTrackMeta.Author, _instances[guild.Id].Host.SpotifyTrackMeta.TrackName);
                 
            // Update the guild's assigned onTrackFinished callback
            if (OnTrackFinished != null)
            {
                _instances[guild.Id].OnTrackFinished = OnTrackFinished;
            }
        }

        public async Task<LavaTrack> FindYouTubeTrack(string query)
        {
            var results = await _lavaNode.SearchYouTubeAsync(query);
            if (results.LoadStatus == LoadStatus.NoMatches || results.LoadStatus == LoadStatus.LoadFailed)
                return null;

            return results.Tracks.FirstOrDefault();
        }

        public async Task<bool> StopAsync(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Player is null) return false;
            
            await _instances[guild.Id].Player.StopAsync();

            return true;
        }

        /// <summary>
        /// Pause player playback and send pause command to slaves
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public async Task<bool> PauseAsync(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Slaves.Count > 0)
            {
                lock (_instances[guild.Id].Slaves)
                {
                    foreach (var slave in _instances[guild.Id].Slaves)
                    {
                        try
                        {
                            slave.PausePlayback();
                        }
                        catch { /* Catch if one of the slaves crashes, continue for all other slaves */ }
                    }
                }
            }

            if (_instances[guild.Id].Player is null) 
                return false;

            if (_instances[guild.Id].Player.PlayerState == PlayerState.Paused) return true;

            await _instances[guild.Id].Player.PauseAsync();

            return true;
        }

        /// <summary>
        /// Resume player playback and send resume command to slaves
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public async Task<bool> ResumeAsync(IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Slaves.Count > 0)
            {
                lock (_instances[guild.Id].Slaves)
                {
                    foreach (var slave in _instances[guild.Id].Slaves)
                    {
                        try
                        {
                            slave.ResumePlayback();
                        }
                        catch { /* Catch if one of the slaves crashes, continue for all other slaves */ }
                    }
                }
            }

            if (_instances[guild.Id].Player is null)
                return false;

            if (_instances[guild.Id].Player.PlayerState != PlayerState.Paused) return true;

            await _instances[guild.Id].Player.ResumeAsync();

            return true;
        }

        /// <summary>
        /// Seek to certain point in playback and send seek command to slaves
        /// </summary>
        /// <param name="position"></param>
        /// <param name="guild"></param>
        /// <returns></returns>
        public async Task<bool> SeekToAsync(ulong position, IGuild guild)
        {
            MakeSureInstance(guild.Id);
            if (_instances[guild.Id].Slaves.Count > 0)
            {
                lock (_instances[guild.Id].Slaves)
                {
                    foreach (var slave in _instances[guild.Id].Slaves)
                    {
                        try
                        {
                            slave.SeekTo(position);
                        }
                        catch { /* Catch if one of the slaves crashes, continue for all other slaves */ }
                    }
                }
            }

            if (_instances[guild.Id].Player is null)
                return false;

            if (_instances[guild.Id].Player.PlayerState == PlayerState.Disconnected || _instances[guild.Id].Player.PlayerState == PlayerState.Stopped) return false;

            await _instances[guild.Id].Player.SeekAsync(TimeSpan.FromMilliseconds(position));

            return true;
        }

        public async Task SetVolumeAsync(IGuild guild, ushort vol)
        {
            MakeSureInstance(guild.Id);

            if (_instances[guild.Id].Player is null)
                return;

            if (vol > 150 || vol < 0) return;

            await _instances[guild.Id].Player.UpdateVolumeAsync(vol);
        }

        public LavaPlayer GetPlayer(IGuild guild)
        {
            MakeSureInstance(guild.Id);

            return _instances[guild.Id].Player;
        }

        public bool IsPlaying(IGuild guild)
        {
            MakeSureInstance(guild.Id);

            if (_instances[guild.Id].Player is null) return false;

            return !(_instances[guild.Id].Player.Track is null);
        }

        /// <summary>
        /// Toggles 24/7 mode in this guild
        /// </summary>
        /// <returns></returns>
        public bool Toggle247(IGuild guild)
        {
            MakeSureInstance(guild.Id);

            var instance = _instances[guild.Id];
            instance.TwentyfourSeven = !instance.TwentyfourSeven;

            if (instance.TwentyfourSeven)
                instance.LeaveWaitHandle?.Set();
            else
            {
                if (instance.Host is null)
                    Task.Run(() => OnPlaybackStopped(guild));
                else
                    instance.Host.OnDisabled247();
            }

            return instance.TwentyfourSeven;
        }

        /// <summary>
        /// Get whether this guild is in 24/7 mode or not
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        public bool Is247(IGuild guild)
        {
            MakeSureInstance(guild.Id);

            return _instances[guild.Id].TwentyfourSeven;
        }

        private async Task ClientReadyAsync()
        {
            Console.WriteLine("[MusicService] Client is ready: Connecting to Lavalink...");
            if (!_lavaNode.IsConnected) await _lavaNode.ConnectAsync();
        }

        private Task TrackFinished(Victoria.EventArgs.TrackEndedEventArgs arg)
        {
            if (arg.Player.VoiceChannel is null) return Task.CompletedTask;

            MakeSureInstance(arg.Player.VoiceChannel.GuildId);
            _instances[arg.Player.VoiceChannel.GuildId].OnTrackFinished?.Invoke(arg.Track);

            return Task.CompletedTask;
        }

        /// <summary>
        /// On playback stopped (paused or no more songs in queue)
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        private async Task OnPlaybackStopped(IGuild guild)
        {
            var instance = _instances[guild.Id];

            if (instance.TwentyfourSeven) return;

            instance.LeaveWaitHandle.Set();
            instance.LeaveWaitHandle.Reset();

            if (!instance.LeaveWaitHandle.WaitOne(1000 * 60 * 5 / 2))
            {
                await GetTextChannel(guild).SendMessageAsync(embed: new EmbedBuilder()
                    .WithDescription("I left the voice channel because of inactivity")
                    .WithAuthor("Left voice channel")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                await LeaveAsync(guild);
            }
        }
    }

    public class GuildMusicInstance
    {
        public LavaPlayer Player { get; set; }
        public SpotifyInstance Host { get; set; }
        public List<SpotifyInstance> Slaves { get; set; } = new List<SpotifyInstance>();
        public Action<LavaTrack> OnTrackFinished { get; set; }
        public EventWaitHandle LeaveWaitHandle { get; set; } = new EventWaitHandle(false, EventResetMode.AutoReset);
        public bool TwentyfourSeven { get; set; } = false;
    }
}
