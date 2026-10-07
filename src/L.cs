using UnityEngine;

namespace ValheimSlots
{
    /// <summary>
    /// Text in the game's language: Swedish when Valheim runs in Swedish, otherwise English.
    /// Follows the language setting live (checked at most once per frame).
    /// </summary>
    internal static class L
    {
        private static int _frame = -1;
        private static bool _swedish;

        /// <summary>Changes whenever the game language changes, so cached UI can rebuild.</summary>
        public static int Version { get; private set; }

        public static bool Swedish
        {
            get
            {
                if (_frame == Time.frameCount)
                    return _swedish;
                _frame = Time.frameCount;
                bool sv;
                try
                {
                    sv = Localization.instance.GetSelectedLanguage() == "Swedish";
                }
                catch
                {
                    sv = false;
                }
                if (sv != _swedish)
                {
                    _swedish = sv;
                    Version++;
                }
                return _swedish;
            }
        }

        /// <summary>Pick the English or Swedish text.</summary>
        public static string T(string en, string sv) => Swedish ? sv : en;
    }
}
