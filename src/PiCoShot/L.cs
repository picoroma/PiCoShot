using System;
using System.Globalization;

namespace PiCoShot
{
    /// <summary>
    /// Lingua dell'interfaccia (italiano / inglese), senza librerie esterne.
    /// Impostazione "auto": italiano se Windows è in italiano, altrimenti inglese; "it" e "en" la forzano.
    /// La lingua viene letta all'avvio: un cambio dalle Impostazioni vale dal riavvio successivo.
    /// Uso: L.T("testo italiano", "english text").
    /// </summary>
    internal static class L
    {
        private static bool? _english;

        public static bool English
        {
            get
            {
                if (_english == null) Init("auto");
                return _english.Value;
            }
        }

        public static void Init(string setting)
        {
            string s = (setting ?? "auto").Trim().ToLowerInvariant();
            if (s == "it") _english = false;
            else if (s == "en") _english = true;
            else _english = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "it";
        }

        public static string T(string italian, string english)
        {
            return English ? english : italian;
        }
    }
}
