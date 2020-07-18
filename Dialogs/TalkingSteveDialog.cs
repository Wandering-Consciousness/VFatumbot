using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Logging;
using VFatumbot.BotLogic;

namespace VFatumbot
{
    public class TalkingSteveDialog : ComponentDialog
    {
        [DllImport("libqwqng", CallingConvention = CallingConvention.Cdecl)]
        public extern static void randbytes(byte[] buffer, int bytecount);

        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;
        protected readonly MainDialog _mainDialog;

        protected static readonly char[] CHARS =
        {
            'a',
            'b',
            'c',
            'd',
            'e',
            'f',
            'g', //7
            'h',
            'i',
            'j',
            'k',
            'l',
            'm',
            ' ', // middle
            'n',
            'o',
            'p',
            'q',
            'r',
            's',
            't',
            'u', //-7
            'v',
            'w',
            'x',
            'y',
            'z',
        };

        protected static readonly string[,] kana =
        {
            { "あ","い","う","え","お" },
            { "ぁ","ぃ","ぅ","ぇ","ぉ" },
            { "か","き","く","け","こ" },
            { "が","ぎ","ぐ","げ","ご" },
            { "さ","し","す","せ","そ" },
            { "ざ","じ","ず","ぜ","ぞ" },
            { "た","ち","つ","て","と" },
            { "だ","ぢ","づ","で","ど" },
            { "　","　","　","　","　" },
            { "な","に","ぬ","ね","の" },
            { "は","ひ","ふ","へ","ほ" },
            { "ば","び","ぶ","べ","ぼ" },
            { "ぱ","ぴ","ぷ","ぺ","ぽ" },
            { "ま","み","む","め","も" },
            { "や","ゃ","ゆ","ゅ","よ" },
            { "ら","り","る","れ","ろ" },
            { "わ","ょ","を","っ","ん" }
        };

        public TalkingSteveDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, MainDialog mainDialog, ILogger<MainDialog> logger) : base(nameof(TalkingSteveDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;
            _mainDialog = mainDialog;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                MakeSteveSpeakStepAsync,
                PerformActionStepAsync,
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> MakeSteveSpeakStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("ts_talkwithsteve")),
                RetryPrompt = MessageFactory.Text(Loc.g("invalid_action")),
                Choices = GetActionChoices(),
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        // majority vote a byte
        static byte mv(byte[] bites)
        {
            byte ret = 0;
            for (int i = 0; i < 8; i++)
            {
                bites[i] <<= 1; // drop left bit for even number
                if (countSetBits(bites[i]) >= 4)
                {
                    ret |= 0x1;
                    ret <<= 1;
                }
                else
                {
                    ret <<= 1;
                }
            }
            return ret;
        }

        static int countSetBits(int n)
        {
            int count = 0;
            while (n > 0)
            {
                count += n & 1;
                n >>= 1;
            }
            return count;
        }

        private async Task<DialogTurnResult> PerformActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var actionHandler = new ActionHandler();
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context, () => new UserProfileTemporary());

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (val.Equals(Loc.g("ts_talk")))
            {
                var str = "";
                var qrng = new QuantumRandomNumberGenerator();
                for (int i = 0; i < 30; i++)
                {
                    // 1d random walk
                    int y = 14; // CHARS.Length/2; // origin
                    for (int walks = 0; walks < 1000; walks++)
                    {
                        var sb = new byte[8];
                        //new Random().NextBytes(sb); // pseudo
                        randbytes(sb, 8); // scott's hardware
                        var bite = mv(sb);

                        for (int k = 0; k < 8; k++)
                        {
                            var currentBit = bite & 0x0001;
                            if (currentBit == 1 && y < CHARS.Length - 2)
                            {
                                y++;
                            }
                            else if (currentBit == 0 && y > 0)
                            {
                                y--;
                            }
                            bite >>= 1;
                        }

                    }
                    str += CHARS[y];

                    // 2d walk
                    //int x = 2; // origin
                    //int y = 8; // origin
                    //for (int walks = 0; walks < 1; walks++)
                    //{
                        //var sb = new byte[8];
                        ////new Random().NextBytes(sb); // pseudo
                        //randbytes(sb, 8); // scott's hardware
                        //var bite = mv(sb);

                    //    for (int k = 0; k < 4; k++)
                    //    {
                    //        var currentBit = bite & 0x0001;
                    //        if (currentBit == 1 && y < 17)
                    //        {
                    //            y++;
                    //        }
                    //        else if (currentBit == 0 && y > 0)
                    //        {
                    //            y--;
                    //        }
                    //        bite >>= 1;

                    //        currentBit = bite & 0x0001;
                    //        if (currentBit == 1 && x < 4)
                    //        {
                    //            x++;
                    //        }
                    //        else if (currentBit == 0 && x > 0)
                    //        {
                    //            x--;
                    //        }
                    //        bite >>= 1;
                    //    }

                    //}
                    //str += kana[y,x];

                    //str += CHARS[qrng.Next(0, CHARS.Length - 1)];
                }
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(str), cancellationToken);

                return await stepContext.ReplaceDialogAsync(nameof(TalkingSteveDialog), cancellationToken: cancellationToken);
            }

            return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
        }

        private IList<Choice> GetActionChoices()
        {
            var actionOptions = new List<Choice>()
            {
                new Choice() {
                    Value = Loc.g("ts_talk"),
                    Synonyms = new List<string>()
                                    {
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_back"),
                    Synonyms = new List<string>()
                                    {
                                        "<",
                                        "Back",
                                        "back",
                                        "<back",
                                    }
                },
            };

            return actionOptions;
        }
    }
}
