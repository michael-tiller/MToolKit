namespace MToolKit.Runtime.Utilities
{
    /// <summary>
    /// Basic static class for checking whether an input contains profanity.
    /// </summary>
    public static class ProfanityFilter
    {
        private static readonly string[] ProfanityList =
        {
            "shit",
            "fuck",
            "asshole",
            "bitch",
            "bastard",
            "damn",
            "dick",
            "piss",
            "cunt"
        };

        /// <summary>
        /// Checks if the input contains any of the predefined banned words.
        /// </summary>
        public static bool ContainsProfanity(string input)
        {
            foreach (var badWord in ProfanityList)
            {
                if (input.IndexOf(badWord, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
