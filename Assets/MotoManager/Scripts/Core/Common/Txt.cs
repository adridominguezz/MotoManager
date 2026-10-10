using System;
using System.Globalization;

namespace MotoManager.Core.Common
{
    /// <summary>
    /// Texts produced by the simulation (news, race events, answers in negotiations). The core stays free of any
    /// engine or language files: every text has a key and its English template inline, and the app plugs in a
    /// <see cref="Provider"/> that returns the template in the player's language (or null to keep English).
    /// Providers must be safe to call from the worker thread (read-only lookups).
    /// </summary>
    public static class Txt
    {
        /// <summary>key -> template in the current language, or null.</summary>
        public static Func<string, string> Provider;

        /// <summary>Number formatting of the current language.</summary>
        public static IFormatProvider Culture = CultureInfo.InvariantCulture;

        /// <summary>Formats the text for <paramref name="key"/> ({0}, {1}... placeholders).</summary>
        public static string F(string key, string english, params object[] args)
        {
            string template = Lookup(key) ?? english;
            if (args == null || args.Length == 0) return template;
            try
            {
                return string.Format(Culture, template, args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, english, args);
            }
        }

        /// <summary>A name (enum value, category...) in the current language.</summary>
        public static string Name(string key, string english)
        {
            return Lookup(key) ?? english;
        }

        private static string Lookup(string key)
        {
            var p = Provider;
            if (p == null) return null;
            try
            {
                return p(key);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
