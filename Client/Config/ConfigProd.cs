namespace DiscordMusicBot.Client.Config
{
    public class ConfigProd : IBotConfig
    {
        public char GetCommandPrefix()
        {
            return '+';
        }

        public string GetDiscordBotToken()
        {
            return "<removed because it contained sensitive info>";
        }

        public string GetLavalinkAuthCode()
        {
            return "3bbncdGtbF9VWT5dbhjQksfxhqGUybf6";
        }

        public string GetMongoDBString()
        {
            return "<removed because it contained sensitive info>";
        }

        public SpotifyCreds GetSpotifyCreds()
        {
            return new SpotifyCreds()
            {
                ClientID = "930752dcb32f423ab506beea5d34f1d2",
                ClientSecret = "<removed because it contained sensitive info>"
            };
        }

        public string GetGeniusAPIToken()
        {
            return "<removed because it contained sensitive info>";
        }
    }
}
