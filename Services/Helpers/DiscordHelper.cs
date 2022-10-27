using System;
using System.Collections.Generic;
using System.Text;

namespace DiscordMusicBot.Services.Helpers
{
    public class DiscordHelper
    {
        public static string EscapeMarkup(string plain)
        {
            return plain
                .Replace("\\", "\\\\")
                .Replace("*", "\\*")
                .Replace("_", "\\_")
                .Replace("~", "\\~")
                .Replace("`", "\\`");
        }
    }
}
