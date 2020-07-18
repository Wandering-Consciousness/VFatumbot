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
            dicts.Add("zh-tw", File.ReadLines($"i18n/zh_tw.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("zh-hk", File.ReadLines($"i18n/zh_tw.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("zh-sg", File.ReadLines($"i18n/zh_cn.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("zh-cn", File.ReadLines($"i18n/zh_cn.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("zh", File.ReadLines($"i18n/zh_cn.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("cn", File.ReadLines($"i18n/zh_cn.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("ru", File.ReadLines($"i18n/ru.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("ko", File.ReadLines($"i18n/ko.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("it", File.ReadLines($"i18n/it.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("es", File.ReadLines($"i18n/es.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("de", File.ReadLines($"i18n/de.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("pt", File.ReadLines($"i18n/pt.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("tr", File.ReadLines($"i18n/tr.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("fr", File.ReadLines($"i18n/fr.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            //dicts.Add("pl", File.ReadLines($"i18n/pl.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            //dicts.Add("ro", File.ReadLines($"i18n/ro.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            //dicts.Add("hu", File.ReadLines($"i18n/hu.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
            dicts.Add("uk", File.ReadLines($"i18n/uk.tsv").Select(line => line.Split('\t')).ToDictionary(line => line[0], line => line[1]));
        }

        public static string g(string key, params object[] formatters)
        {
            key = key.ToLower();
            var ret = key;

            var clN = Thread.CurrentThread.CurrentUICulture.Name.ToLower();
            var cl2 = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            if (!dicts.ContainsKey(cl2) && !dicts.ContainsKey(clN))
            {
                if (dicts["en"].ContainsKey(key))
                {
                    ret = string.Format(dicts["en"][key], formatters);
                }
                else
                {
                    ret = key;
                }
            }

            if (dicts.ContainsKey(clN) && dicts[clN].ContainsKey(key))
            {
                ret = string.Format(dicts[clN][key], formatters);
            }
            else if (dicts.ContainsKey(cl2) && dicts[cl2].ContainsKey(key))
            {
                ret = string.Format(dicts[cl2][key], formatters);
            }

            ret = ret.Replace("<br>", "\n");
            return ret;
        }

        public static string getTermsFilename()
        {
            var clN = Thread.CurrentThread.CurrentUICulture.Name.ToLower();
            var cl2 = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            if (!File.Exists($"help2-{clN}.txt") && !File.Exists($"help2-{cl2}.txt"))
            {
                return "help2-en.txt";
            }

            if (File.Exists($"help2-{clN}.txt"))
            {
                return $"help2-{clN}.txt";
            }
            else if (File.Exists($"help2-{cl2}.txt"))
            {
                return $"help2-{cl2}.txt";
            }

            return "help2-en.txt";
        }

        public static bool IsEnglishOrSpanish()
        {
            var clN = Thread.CurrentThread.CurrentUICulture.Name.ToLower();
            var cl2 = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower();
            if ("en".Equals(clN) || "en".Equals(cl2) || "es".Equals(clN) || "es".Equals(cl2))
            {
                return true;
            }

            return false;
        }
    }


}
