using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace MotoManager.App.UI
{
    /// <summary>
    /// Interface texts in Spanish and English (Resources/Localization/{es,en}.json plus ux_{es,en}/*.json, key -> text).
    /// The language follows the device by default and can be changed in Settings; numbers are formatted
    /// with the language's conventions (decimal comma in Spanish).
    /// </summary>
    public static class Loc
    {
        public const string PrefKey = "MotoManager.Language";
        private static Dictionary<string, string> _texts;
        private static Dictionary<string, string> _fallback;
        private static string _language;

        public static event Action LanguageChanged;

        public static string Language
        {
            get
            {
                if (_language == null)
                {
                    string saved = PlayerPrefs.GetString(PrefKey, "");
                    _language = saved == "es" || saved == "en" ? saved : (Application.systemLanguage == SystemLanguage.Spanish ? "es" : "en");
                }
                return _language;
            }
        }

        private static CultureInfo _culture;

        public static CultureInfo Culture
        {
            get
            {
                if (_culture == null) _culture = Language == "es" ? new CultureInfo("es-ES") : CultureInfo.InvariantCulture;
                return _culture;
            }
        }

        /// <summary>
        /// Plugs the texts into the simulation core (news, race events, answers). Call on the main thread: the
        /// dictionaries are loaded here, and the core only reads them (also from the worker thread).
        /// </summary>
        public static void Install()
        {
            if (_texts == null) _texts = Load(Language);
            if (_fallback == null) _fallback = Load("en");
            Core.Common.Txt.Provider = key =>
            {
                var t = _texts;
                string v;
                return t != null && t.TryGetValue(key, out v) ? v : null;
            };
            Core.Common.Txt.Culture = Culture;
        }

        public static void SetLanguage(string language)
        {
            if (language != "es" && language != "en") return;
            _language = language;
            _culture = null;
            _texts = Load(language);
            Core.Common.Txt.Culture = Culture;
            PlayerPrefs.SetString(PrefKey, language);
            PlayerPrefs.Save();
            if (LanguageChanged != null) LanguageChanged();
        }

        /// <summary>
        /// The texts of a language: Localization/{lang}.json, then every file under Localization/ux_{lang}/ merged on
        /// top (each work package of the UX redesign keeps its new keys in its own file, e.g. ux_es/wp1.json).
        /// </summary>
        private static Dictionary<string, string> Load(string language)
        {
            var dict = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("Localization/" + language);
            if (asset != null) Merge(dict, asset);
            TextAsset[] extra = null;
            try { extra = Resources.LoadAll<TextAsset>("Localization/ux_" + language); } catch (Exception) { }
            if (extra != null)
            {
                // A stable order (by name) so a key defined twice always resolves the same way.
                Array.Sort(extra, (x, y) => string.CompareOrdinal(x.name, y.name));
                foreach (var e in extra) Merge(dict, e);
            }
            return dict;
        }

        private static void Merge(Dictionary<string, string> into, TextAsset asset)
        {
            Dictionary<string, string> d = null;
            try { d = JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text); }
            catch (Exception e) { Debug.LogWarning("Localization file " + asset.name + " is not valid: " + e.Message); }
            if (d == null) return;
            foreach (var kv in d) into[kv.Key] = kv.Value;
        }

        /// <summary>Translated text for a key (falls back to English, then to the key itself).</summary>
        public static string T(string key, params object[] args)
        {
            if (_texts == null) _texts = Load(Language);
            if (_fallback == null) _fallback = Load("en");
            string text;
            if (!_texts.TryGetValue(key, out text) && !_fallback.TryGetValue(key, out text)) text = key;
            if (args == null || args.Length == 0) return text;
            try
            {
                return string.Format(Culture, text, args);
            }
            catch (FormatException)
            {
                return text;
            }
        }

        /// <summary>Translation of a key when one exists (current language, then English), otherwise the given text.</summary>
        public static string Or(string key, string fallback)
        {
            if (_texts == null) _texts = Load(Language);
            if (_fallback == null) _fallback = Load("en");
            string text;
            return _texts.TryGetValue(key, out text) || _fallback.TryGetValue(key, out text) ? text : fallback;
        }

        /// <summary>Event name in the player's language.</summary>
        public static string Round(string name)
        {
            return Or("round." + name, name);
        }

        public static bool Has(string key)
        {
            if (_texts == null) _texts = Load(Language);
            return _texts.ContainsKey(key);
        }
    }
}
