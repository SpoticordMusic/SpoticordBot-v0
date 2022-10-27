using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordMusicBot.Client.Config;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordMusicBot.Client
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _cmdService;
        private readonly IServiceProvider _services;

        public CommandHandler(DiscordSocketClient client, CommandService cmdService, IServiceProvider services)
        {
            _client = client;
            _cmdService = cmdService;
            _services = services;
        }

        public async Task InitializeAsync()
        {
            await _cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _cmdService.Log += LogAsync;
            _client.MessageReceived += HandleMessageAsync;
        }

        public void Shutdown()
        {
            _cmdService.Log -= LogAsync;
            _client.MessageReceived -= HandleMessageAsync;
        }

        private async Task HandleMessageAsync(SocketMessage socketMessage)
        {
            var argPos = 0;
            if (socketMessage.Author.IsBot) return;

            var userMessage = socketMessage as SocketUserMessage;
            if (userMessage is null)
                return;

            if (!userMessage.HasCharPrefix(BotConfig.GetFor(MusicBotClient.BOT_BUILD).GetCommandPrefix(), ref argPos)) 
                return;

            var guildChannel = userMessage.Channel as IGuildChannel;

            if (guildChannel is null)
                return;

            if (MusicBotClient.BOT_BUILD == BotConfig.BotBuild.Livedev && !(userMessage.Author as SocketGuildUser).GuildPermissions.ManageGuild)
                return;

            if (MusicBotClient.BOT_BUILD == BotConfig.BotBuild.Secondary)
            {
                var roleFound = false;
                
                foreach (var role in (userMessage.Author as SocketGuildUser).Roles)
                {
                    if (role.Id == 781594284104876072)
                    {
                        roleFound = true;
                        break;
                    }    
                }

                if (!roleFound) 
                    return;
            }

            // Bot requires Embed Links permission
            if (!(await guildChannel.Guild.GetUserAsync(_client.CurrentUser.Id)).GetPermissions(guildChannel).EmbedLinks)
            {
                await (guildChannel as ITextChannel).SendMessageAsync("I am missing the **Embed Links** permission.\nPlease allow this permission to Spoticord Music (either the role, channel or bot user)");
                return;
            }

            var context = new SocketCommandContext(_client, userMessage);
            var result = await _cmdService.ExecuteAsync(context, argPos, _services);

            if (result.IsSuccess)
            {
                var command = userMessage.Content.Substring(argPos).Split(null)[0];

                _ = Analytics.CommandExecute(context.Guild, context.User, command);
            }
        }

        private Task LogAsync(LogMessage logMessage)
        {
            //Console.WriteLine(logMessage.Message);
            return Task.CompletedTask;
        }
    }
}
