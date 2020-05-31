using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VFatumbot.BotLogic
{
    // Localization class
    public static class Loc
    {
        static IDictionary<string, IDictionary<string, string>> dicts = new Dictionary<string, IDictionary<string, string>>();

        public static void init()
        {
            var en = File.ReadLines("i18n/ja.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]);
            dicts.Add("en", en);
        }

        public static string g(string key, params object[] formatters)
        {
            return string.Format(dicts["en"][key], formatters);
        }
    }
}
