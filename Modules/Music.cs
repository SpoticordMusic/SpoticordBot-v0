using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusicBot.Client;
using DiscordMusicBot.Client.Config;
using DiscordMusicBot.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordMusicBot.Modules
{
    public class Music : ModuleBase<SocketCommandContext>
    {
        private MusicService _musicService;
        private SpotifyService _spotifyService;
        private DatabaseService _databaseService;

        public Music(MusicService musicService, SpotifyService spotifyService, DatabaseService databaseService)
        {
            _musicService = musicService;
            _spotifyService = spotifyService;
            _databaseService = databaseService;
        }

        /// <summary>
        /// +join command, makes the bot join the VC if certain criteria are met
        /// </summary>
        /// <returns></returns>
        [Command("Join")]
        public async Task Join()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (user.VoiceChannel is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("You need to connect to a voice channel")
                    .WithAuthor("Cannot join voice channel", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            if (!_spotifyService.HasInstance(user.Id))
            {
                string prefix = BotConfig.GetFor(MusicBotClient.BOT_BUILD).GetCommandPrefix().ToString();

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"You need to link your Spotify account with the bot using the \"{prefix}link\" command")
                    .WithAuthor("Cannot join voice channel", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            // Criteria for +join working:
            //   Bot is not in VC, user is -> Bot will join the VC
            //   Bot is in VC, user is in same VC -> Spotify will be activated
            //   Bot is in VC, user is in different -> If bot has a host deny, else move the bot

            var botPerms = Context.Guild.GetUser(Context.Client.CurrentUser.Id).GetPermissions(user.VoiceChannel);
            if (!botPerms.Connect || !botPerms.Speak)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("I don't have the appropriate permissions to play music that channel")
                    .WithAuthor("Cannot join voice channel", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            // Criteria: Bot is not in VC
            if (_musicService.GetVoiceChannel(Context.Guild.Id) is null)
            {
                await _musicService.ConnectAsync(user.VoiceChannel, Context.Channel as ITextChannel);
                await _spotifyService.ActivateUserAsync(user);

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"Come listen along in <#{user.VoiceChannel.Id}>")
                    .WithAuthor("Connected to voice channel", "https://images.emojiterra.com/mozilla/512px/1f50a.png")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());

                return;
            }

            // Criteria: Bot and user are in the same VC
            if (_musicService.GetVoiceChannel(Context.Guild.Id) == user.VoiceChannel)
            {
                await _spotifyService.ActivateUserAsync(user);

                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"You have joined the listening party, check your Spotify!")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());
            }
            else
            {
                // Criteria: Bot is inactive and can be moved
                if (_musicService.GetHost(Context.Guild) is null)
                {
                    _spotifyService.MarkMoving(Context.Guild);

                    await _musicService.LeaveAsync(Context.Guild);

                    await _musicService.ConnectAsync(user.VoiceChannel, Context.Channel as ITextChannel);
                    await _spotifyService.ActivateUserAsync(user);

                    await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"Come listen along in <#{user.VoiceChannel.Id}>")
                    .WithAuthor("Connected to voice channel", "https://images.emojiterra.com/mozilla/512px/1f50a.png")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());
                }
                // Criteria not met
                else
                {
                    await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription("The bot is currently being used in another voice channel")
                        .WithAuthor("Cannot join voice channel", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                        .WithColor(0xd6, 0x15, 0x16)
                        .Build());
                }
            }
        }

        /// <summary>
        /// +leave command, make the bot leave the call
        /// </summary>
        /// <returns></returns>
        [Command("Leave")]
        [Alias("dc", "disconnect", "kick")]
        public async Task Leave()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (_musicService.GetVoiceChannel(Context.Guild.Id) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not connected to any voice channel")
                    .WithAuthor("Cannot disconnect bot", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            // Criteria: Bot is inactive and can be disconnected
            if (_musicService.GetHost(Context.Guild) is null)
            {
                await _musicService.LeaveAsync(Context.Guild);
            }
            // Criteria: Bot host is us
            else if (_musicService.GetHost(Context.Guild).User.Id == Context.User.Id)
            {
                await _musicService.LeaveAsync(Context.Guild);
            }
            // Criteria not met
            else
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently being managed by someone else")
                    .WithAuthor("Cannot disconnect bot", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
            }
        }

        [Command("playing")]
        [Alias("np")]
        public async Task Playing()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (_musicService.GetVoiceChannel(Context.Guild.Id) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not connected to any voice channel")
                    .WithAuthor("Cannot get bot info", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            // Criteria: Bot is inactive and can be disconnected
            if (_musicService.GetHost(Context.Guild) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not playing anything")
                    .WithAuthor("Cannot get bot info", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            if (_musicService.GetHost(Context.Guild).YouTubeTrackMeta is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not playing anything")
                    .WithAuthor("Cannot get bot info", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            var ytMeta = _musicService.GetHost(Context.Guild).YouTubeTrackMeta;
            var spMeta = _musicService.GetHost(Context.Guild).SpotifyTrackMeta;

            var host = _musicService.GetHost(Context.Guild).User;

            var prefix = _databaseService.GetPlan(host.Id.ToString()) == "donator" ? "⭐ " : "";            

            await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor($"{prefix}Currently Playing", "https://www.freepnglogos.com/uploads/spotify-logo-png/file-spotify-logo-png-4.png")
                .WithTitle($"{spMeta.Author} - {spMeta.TrackName}")
                .WithUrl(spMeta.TrackURL)
                .WithDescription($"Click **[here]({ytMeta.TrackURL})** for the YouTube version")
                .WithFooter($"{prefix}{host.Nickname ?? host.Username}", host.GetAvatarUrl())
                .WithColor(0x07, 0x73, 0xd6)
                .Build());
        }

        private static List<IUserMessage> LyricMessages;

        public async Task Lyrics()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (_musicService.GetVoiceChannel(Context.Guild.Id) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not connected to any voice channel")
                    .WithAuthor("Cannot get lyrics", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            // Criteria: Bot is inactive and can be disconnected
            if (_musicService.GetHost(Context.Guild) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not playing anything")
                    .WithAuthor("Cannot get lyrics", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            if (_musicService.GetHost(Context.Guild).YouTubeTrackMeta is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not playing anything")
                    .WithAuthor("Cannot get lyrics", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            /*var lyricMeta = _musicService.GetHost(Context.Guild).GeniusMeta;

            if (lyricMeta is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("No lyrics metadata was found for the current song")
                    .WithAuthor("Cannot get lyrics", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            if (lyricMeta.Lyrics.Length > 5000)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("Lyrics exceed 5000 characters")
                    .WithAuthor("Cannot get lyrics", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            if (!(LyricMessages is null))
            {
                try
                {
                    foreach (var msg in LyricMessages)
                    {
                        await msg.DeleteAsync();
                    }
                } catch { }

                LyricMessages = null;
            }

            var messages = new List<IUserMessage>();

            var spMeta = _musicService.GetHost(Context.Guild).SpotifyTrackMeta;

            var j = 0;

            for (var i = 0; ; i++)
            {
                var thisRoundContent = "";
                var totalLength = 0;
                var plsContinue = false;

                var splitLyrics = lyricMeta.Lyrics.Split('\n');

                for (; j < splitLyrics.Length; j++)
                {
                    totalLength += splitLyrics[j].Length;
                    if (totalLength > 1799)
                    {
                        if (i == 0)
                        messages.Add(await ReplyAsync(embed: new EmbedBuilder()
                            .WithAuthor($"Song Lyrics", "https://www.freepnglogos.com/uploads/spotify-logo-png/file-spotify-logo-png-4.png")
                            .WithTitle($"{spMeta.Author} - {spMeta.TrackName}")
                            .WithUrl(spMeta.TrackURL)
                            .WithDescription($"{thisRoundContent}")
                            .WithFooter($"🔽 More lyrics below")
                            .WithColor(0x07, 0x73, 0xd6)
                            .Build()));

                        else
                        messages.Add(await ReplyAsync(embed: new EmbedBuilder()
                            .WithDescription($"{thisRoundContent}")
                            .WithFooter($"🔽 More lyrics below")
                            .WithColor(0x07, 0x73, 0xd6)
                            .Build()));

                        plsContinue = true;
                        break;
                    }

                    thisRoundContent += splitLyrics[j] + "\n";
                }

                if (plsContinue) continue;

                if (i == 0)
                    messages.Add(await ReplyAsync(embed: new EmbedBuilder()
                        .WithAuthor($"Song Lyrics", "https://www.freepnglogos.com/uploads/spotify-logo-png/file-spotify-logo-png-4.png")
                        .WithTitle($"{spMeta.Author} - {spMeta.TrackName}")
                        .WithUrl(spMeta.TrackURL)
                        .WithDescription($"{thisRoundContent}")
                        .WithFooter($"⚡ Powered By: Genius")
                        .WithColor(0x07, 0x73, 0xd6)
                        .Build()));

                else
                    messages.Add(await ReplyAsync(embed: new EmbedBuilder()
                        .WithDescription($"{thisRoundContent}")
                        .WithFooter($"⚡ Powered By: Genius")
                        .WithColor(0x07, 0x73, 0xd6)
                        .Build()));

                break;
            }

            LyricMessages = messages;*/
        }

        /// <summary>
        /// +24/7 command, prevent the bot from automatically leaving
        /// </summary>
        /// <returns></returns>
        [Command("24/7")]
        [Alias("stay")]
        public async Task TwentyfourSeven()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (_databaseService.GetPlan(user.Id.ToString()) != "donator")
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"This command is only available for donators.")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());

                return;
            }

            if (_musicService.GetVoiceChannel(Context.Guild.Id) is null)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription("The bot is currently not connected to any voice channel")
                    .WithAuthor("Cannot disconnect bot", "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Forbidden_Symbol_Transparent.svg/1200px-Forbidden_Symbol_Transparent.svg.png")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            var isEnabled = _musicService.Toggle247(Context.Guild);

            await ReplyAsync(embed: new EmbedBuilder()
                .WithDescription($"__27/4__ mode has been **{(isEnabled ? "enabled" : "disabled")}**")
                .WithColor(0x07, 0x73, 0xd6)
                .Build());
        }
    }
}
