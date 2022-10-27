namespace DiscordMusicBot.Services.DBModels
{
    public class DBToken : DBModel
    {
        public string SpotifyAccessToken;
        public string SpotifyRefreshToken;
        public ulong DiscordID;
    }
}
