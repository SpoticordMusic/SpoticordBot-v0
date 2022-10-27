using DiscordMusicBot.Client;
using System;
using System.Threading.Tasks;

namespace DiscordMusicBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            /*AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
            {
                Console.WriteLine("====================================================================================\n" + 
                    eventArgs.Exception.ToString() +
                    "\n ====================================================================================\n\n");
            };*/

            await new MusicBotClient().InitializeAsync();
        }
    }
}