using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace VFatumbot.BotLogic
{
    // Localization class
    public static class Loc
    {
        static IDictionary<string, IDictionary<string, string>> dicts = new Dictionary<string, IDictionary<string, string>>();

        public static void init()
        {
            dicts.Add("en", File.ReadLines($"i18n/en.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("ja", File.ReadLines($"i18n/ja.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
        }

        public static string g(string key, params object[] formatters)
        {
            key = key.ToLower();

            var cl = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            if (!dicts.ContainsKey(cl))
            {
                if (dicts["en"].ContainsKey(key))
                {
                    return string.Format(dicts["en"][key], formatters);
                }
                else
                {
                    return key;
                }
            }

            if (dicts[cl].ContainsKey(key))
            {
                return string.Format(dicts[cl][key], formatters);
            }

            return key;
        }
    }
}
