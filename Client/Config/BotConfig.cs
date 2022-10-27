using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.Client.Config
{
    public class BotConfig
    {
        /// <summary>
        /// Get config for the specified build type
        /// </summary>
        /// <param name="build"></param>
        /// <returns></returns>
        public static IBotConfig GetFor(BotBuild build)
        {
            switch(build)
            {
                case BotBuild.Production:
                    return new ConfigProd();

                case BotBuild.Livedev:
                    return new ConfigBeta();

                case BotBuild.Secondary:
                    return new ConfigTwo();

                default:
                    return new ConfigProd();
            }
        }

        /// <summary>
        /// Bot build type
        /// </summary>
        public enum BotBuild
        {
            Livedev,
            Production,
            Secondary
        }
    }

    public interface IBotConfig
    {
        /// <summary>
        /// The command prefix (e.g. '+' for +join and +leave)
        /// </summary>
        /// <returns></returns>
        public char GetCommandPrefix();

        /// <summary>
        /// The Discord bot token to interact with the Discord API
        /// </summary>
        /// <returns></returns>
        public string GetDiscordBotToken();

        /// <summary>
        /// Get the Mongo Database connection string
        /// </summary>
        /// <returns></returns>
        public string GetMongoDBString();

        /// <summary>
        /// Get the Spotify application credentials
        /// </summary>
        /// <returns></returns>
        public SpotifyCreds GetSpotifyCreds();

        /// <summary>
        /// Gets the Lavalink WebSocket authorization code
        /// </summary>
        /// <returns></returns>
        public string GetLavalinkAuthCode();

        /// <summary>
        /// Get the Genius API Token used for scraping lyrics
        /// </summary>
        /// <returns></returns>
        public string GetGeniusAPIToken();
    }

    /// <summary>
    /// Spotify Application credentials structure
    /// </summary>
    public struct SpotifyCreds
    {
        public string ClientID;
        public string ClientSecret;
    }
}
