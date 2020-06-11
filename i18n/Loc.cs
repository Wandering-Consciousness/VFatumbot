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
        }

        public static string g(string key, params object[] formatters)
        {
            key = key.ToLower();
            var ret = "";

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
            }
        }
    }
}
