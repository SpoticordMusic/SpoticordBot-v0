using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusicBot.Client.Config;
using DiscordMusicBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Victoria;

namespace DiscordMusicBot.Client
{
    public class MusicBotClient
    {
        public const string VERSION = "pre1.0";
        public const uint BUILD = 76;

        public const BotConfig.BotBuild BOT_BUILD = BotConfig.BotBuild.Livedev;

        private DiscordSocketClient _client;
        private CommandService _cmdService;
        private CommandHandler _cmdHandler;
        private IServiceProvider _services;

        private EventWaitHandle _botEndOfLife = new EventWaitHandle(false, EventResetMode.ManualReset);

        public MusicBotClient(DiscordSocketClient client = null, CommandService cmdService = null)
        {
            _client = client ?? new DiscordSocketClient(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 50,
                LogLevel = LogSeverity.Debug
            });

            _cmdService = cmdService ?? new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Verbose,
                CaseSensitiveCommands = false
            });
        }

        public async Task InitializeAsync()
        {
            await _client.LoginAsync(TokenType.Bot, BotConfig.GetFor(BOT_BUILD).GetDiscordBotToken());

            _client.Log += LogAsync;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _services = SetupServices();

            _cmdHandler = new CommandHandler(_client, _cmdService, _services);
            await _cmdHandler.InitializeAsync();

            if (!_services.GetRequiredService<DatabaseService>().Initialize())
            {
                Console.WriteLine("[FATAL] Failed to initialize the database service");
                Console.WriteLine("[FATAL] Exiting...");
                Environment.Exit(-1);
                return;
            }

            await _services.GetRequiredService<SpotifyService>().InitializeAsync();
            await _services.GetRequiredService<MusicService>().InitializeAsync();

            await _client.StartAsync();

            if (BOT_BUILD == BotConfig.BotBuild.Livedev)
                await _client.SetGameAsync($"Version {VERSION}, Build {BUILD.ToString("x2")}", type: ActivityType.Playing);
            else
                await _client.SetGameAsync($"some good 'ol tunes");

            _botEndOfLife.WaitOne();

            Console.WriteLine("Bot shutdown initiated");

            _cmdHandler.Shutdown();
            Console.WriteLine("Command service has been shutdown");

            await _services.GetRequiredService<MusicService>().ShutdownAsync();
            Console.WriteLine("Music service has been shutdown");

            await _client.LogoutAsync();
            Console.WriteLine("Discord client logout succeeded");
        }

        private async Task LeftGuild(SocketGuild arg)
        {
            Console.WriteLine($"Left guild: {arg.Name}");

            await _services.GetRequiredService<MusicService>().LeaveAsync(arg);

            await Analytics.GuildLeft(arg);
        }

        private async Task JoinedGuild(SocketGuild arg)
        {
            Console.WriteLine($"Joined guild: {arg.Name}");
            await Analytics.GuildJoined(arg);
        }

        private Task LogAsync(LogMessage logMessage)
        {
            //Console.WriteLine(logMessage.Message);
            return Task.CompletedTask;
        }

        private IServiceProvider SetupServices()
            => new ServiceCollection()
            .AddSingleton(this)
            .AddSingleton(_client)
            .AddSingleton(_cmdService)
            .AddLavaNode(x =>
            {
                x.SelfDeaf = true;
                x.Authorization = BotConfig.GetFor(BOT_BUILD).GetLavalinkAuthCode();
            })
            .AddSingleton<MusicService>()
            .AddSingleton<DatabaseService>()
            .AddSingleton<SpotifyService>()
            .BuildServiceProvider();

        public void ShutdownBot()
        {
            _botEndOfLife.Set();
        }
    }
}
