using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusicBot.Client;
using DiscordMusicBot.Client.Config;
using DiscordMusicBot.Services;
using DiscordMusicBot.Services.Helpers;
using System.Threading.Tasks;

namespace DiscordMusicBot.Modules
{
    public class Core : ModuleBase<SocketCommandContext>
    {
        private MusicService _musicService;
        private SpotifyService _spotifyService;
        private DatabaseService _databaseService;
        private MusicBotClient _botClient;

        public Core(MusicBotClient botClient, MusicService musicService, SpotifyService spotifyService, DatabaseService databaseService)
        {
            _botClient = botClient;
            _musicService = musicService;
            _spotifyService = spotifyService;
            _databaseService = databaseService;
        }

        /// <summary>
        /// +link command, connect a Discord ID with a Spotify account
        /// </summary>
        /// <returns></returns>
        [Command("Link")]
        public async Task Link()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (_spotifyService.HasInstance(Context.User.Id))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                   .WithDescription($"You have already linked your Spotify account.")
                   .WithColor(0x07, 0x73, 0xd6)
                   .Build());
                return;
            }

            var linkId = _databaseService.InitializeLink(Context.User.Id);

            var DMChannel = await Context.User.GetOrCreateDMChannelAsync();

            try
            {
                await DMChannel.SendMessageAsync(embed: new EmbedBuilder()
                    .WithAuthor("Link your Spotify account", "https://spoticord.com/img/spotify-logo.png")
                    .WithDescription($"Go to [this link](https://spoticord.com/link/{linkId}) to connect your Spotify account.")
                    .WithFooter($"This message was requested by the {BotConfig.GetFor(MusicBotClient.BOT_BUILD).GetCommandPrefix()}link command")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());
            } catch
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"You must allow direct messages from server members to use this command\n(You can disable it afterwards)")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }
        }

        /// <summary>
        /// +unlink command, disconnect Spotify account from Discord user
        /// </summary>
        /// <returns></returns>
        [Command("Unlink")]
        public async Task Unlink()
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            if (!_spotifyService.HasInstance(Context.User.Id))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"You must link your Spotify account before you can use this command")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());
                return;
            }

            await _spotifyService.UnlinkInstance(Context.User.Id);

            await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"Successfully unlinked your Spotify account")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());
        }

        /// <summary>
        /// +dash command, send a link to the dashboard page of this server and user
        /// </summary>
        /// <returns></returns>
        [Command("dash")]
        public async Task Dash()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithTitle("Spoticord Dashboard")
                .WithUrl("https://dashboard.spoticord.com")
                .WithDescription($"Head on over [here](https://dashboard.spoticord.com/me) to see and manage your profile's settings and analytics.\n\n" +
                                 $"If you are the owner of this server you can check out [this link](https://dashboard.spoticord.com/servers/{Context.Guild.Id}) and see how your server is using Spoticord.")
                .WithColor(0x07, 0x73, 0xd6)
                .WithFooter("And no, you can't see the server data if y'aint the owner. Sorry about that.")
                .Build());
        }

        /// <summary>
        /// +version command, print version and build info
        /// </summary>
        /// <returns></returns>
        [Command("Version")]
        [Alias("v")]
        public async Task Version()
        {
            var build = MusicBotClient.BOT_BUILD == BotConfig.BotBuild.Livedev ? "Livedev" : "Stable";

            var embed = new EmbedBuilder()
                .WithTitle($"Running {build} Build")
                .WithAuthor("Author: RoDaBaFilms", "https://rodabafilms.com/images/logo3.png", "https://rodabafilms.com/")
                .WithFooter($"Version: {MusicBotClient.VERSION} - Build {MusicBotClient.BUILD.ToString("x2")}")
                .WithColor(0x0d, 0xe2, 0x16)
                .Build();

            await ReplyAsync(embed: embed);
        }

        /// <summary>
        /// Renames the Spotify device name of Spoticord to any name the user likes
        /// Name is not allowed to have [livedev] as a prefix
        /// </summary>
        /// <param name="nameArray">The name</param>
        /// <returns></returns>
        [Command("rename")]
        [Alias("name")]
        public async Task Rename(params string[] nameArray)
        {
            var user = Context.User as SocketGuildUser;

            if (user is null) return;

            await _Rename(user, nameArray);
        }

        private async Task _Rename(SocketGuildUser user, string[] nameArray)
        {
            if (_databaseService.GetPlan(user.Id.ToString()) != "donator")
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"This command is only available for donators.")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());

                return;
            }

            string name = string.Join(" ", nameArray);

            if (string.IsNullOrEmpty(name))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"An empty device name is not allowed.")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());

                return;
            }

            if (name.ToLower().StartsWith("[livedev]"))
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"That device name is not allowed.")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());

                return;
            }

            if (name.Length > 16)
            {
                await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"Device name may not be longer than 16 characters.")
                    .WithColor(0xd6, 0x15, 0x16)
                    .Build());

                return;
            }

            _databaseService.SetDeviceName(user.Id.ToString(), name);

            await ReplyAsync(embed: new EmbedBuilder()
                    .WithDescription($"Successfully changed the Spotify device name to **{DiscordHelper.EscapeMarkup(name)}**")
                    .WithColor(0x07, 0x73, 0xd6)
                    .Build());
        }

        /// <summary>
        /// Requests help, not much to add here
        /// </summary>
        /// <returns></returns>
        [Command("Help")]
        [Alias("h")]
        public async Task Help()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor("Spoticord Help", "https://spoticord.com/img/spoticord-logo-clean.png")
                .WithTitle("These following links might help you out")
                .WithDescription("Click **[here](https://spoticord.com/commands)** for a list of commands.\n" +
                "If you need help setting Spoticord up you can check out the **[Documentation](https://spoticord.com/documentation)** page on the Spoticord website." +
                (Context.Guild.Id != 779292533053456404 ? "\n\nIf you still have questions or feedback you can [Join the support server](https://discord.com/invite/wRCyhVqBZ5)\n" +
                "We also post about upcomming features, so you know what's comming in the future!" : "") +
                "\n\nUsing this bot is completely free, but hosting it is not, so please consider supporting Spoticord by [donating](https://www.patreon.com/rodabafilms) to help keep the project alive.")
                .WithColor(0x43, 0xb5, 0x81)
                .Build());
        }

        /// <summary>
        /// Requests help, not much to add here
        /// </summary>
        /// <returns></returns>
        [Command("Donate")]
        public async Task Donate()
        {
            await ReplyAsync(embed: new EmbedBuilder()
                .WithAuthor("Spoticord Help", "https://spoticord.com/img/spoticord-logo-clean.png")
                .WithDescription("If you want you can donate **[here](https://patreon.com/rodabafilms)** and help keep Spoticord stay alive.\n")
                .WithColor(0x43, 0xb5, 0x81)
                .Build());
        }

        /// <summary>
        /// Shutdown the bot, disconnect all VC's and make sure the Analytics are up to date
        /// </summary>
        /// <returns></returns>
        [Command("Shutdown")]
        public async Task Shutdown()
        {
            if (Context.User.Id != 389786424142200835) return;

            await ReplyAsync(embed: new EmbedBuilder()
                .WithDescription("Shutdown initiated")
                .WithColor(0x07, 0x73, 0xd6)
                .Build());

            _botClient.ShutdownBot();
        }
    }
}
