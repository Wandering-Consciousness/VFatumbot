using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Extensions.Logging;
using VFatumbot.BotLogic;

namespace VFatumbot
{
    public class ScanDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;
        protected readonly MainDialog _mainDialog;

        public ScanDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, MainDialog mainDialog, ILogger<MainDialog> logger) : base(nameof(ScanDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;
            _mainDialog = mainDialog;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new ChoicePrompt("AskHowManyScanIDAsChoicePrompt",
               (PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken) =>
               {
                    // override validater result to also allow free text entry for ratings
                    int idacou;
                   if (int.TryParse(promptContext.Context.Activity.Text, out idacou))
                   {
                       if (idacou < 1 || idacou > 10)
                       {
                           return Task.FromResult(false);
                       }

                       return Task.FromResult(true);
                   }

                   return Task.FromResult(false);
               })
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ChoiceActionStepAsync,
                PerformActionStepAsync,
                AskHowManyScanIDAsStepAsync,
                //GetHowManyScanIDAsStepAsync,
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ChoiceActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("ScanDialog.ChoiceActionStepAsync");

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("scan_choose_kind")),
                RetryPrompt = MessageFactory.Text(Loc.g("scan_invalid_action")),
                Choices = GetActionChoices(),
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> PerformActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"ScanDialog.PerformActionStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var actionHandler = new ActionHandler();
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context, () => new UserProfileTemporary());
            var goBackMainMenuThisRound = false;

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (val.Equals(Loc.g("scan_attractor")))
            {
                if (!userProfileTemporary.IsScanning)
                {
                    stepContext.Values["PointType"] = "Attractor";
                    AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Scan Attractor");
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    goBackMainMenuThisRound = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("scan_sesson_inplace")), cancellationToken);
                }
            } else if (val.Equals(Loc.g("scan_void"))) {
                if (!userProfileTemporary.IsScanning)
                {
                    stepContext.Values["PointType"] = "Void";
                    AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Scan Void");
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    goBackMainMenuThisRound = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("scan_sesson_inplace")), cancellationToken);
                }
            }
            else if (val.Equals(Loc.g("scan_anomaly")))
            {
                if (!userProfileTemporary.IsScanning)
                {
                    stepContext.Values["PointType"] = "Anomaly";
                    AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Scan Anomaly");
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    goBackMainMenuThisRound = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("scan_sesson_inplace")), cancellationToken);
                }
            } else if (val.Equals(Loc.g("scan_pair"))) {
                if (!userProfileTemporary.IsScanning)
                {
                    stepContext.Values["PointType"] = "Pair";
                    AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Scan Pair");
                    return await stepContext.NextAsync(cancellationToken: cancellationToken);
                }
                else
                {
                    goBackMainMenuThisRound = true;
                    await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("scan_sesson_inplace")), cancellationToken);
                }
            }
            else if (val.Equals(Loc.g("bs_back")))
            {
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("< Back");
                goBackMainMenuThisRound = true;
            }

            if (goBackMainMenuThisRound)
            {
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
            }
            else
            {
                // Long-running tasks like /getattractors etc will make use of ContinueDialog to re-prompt users
                return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> AskHowManyScanIDAsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //    //_logger.LogInformation($"ScanDialog.AskHowManyScanIDAsStepAsync");

            //    var options = new PromptOptions()
            //    {
            //        Prompt = MessageFactory.Text(Loc.g("md_how_many_idas")),
            //        RetryPrompt = MessageFactory.Text(Loc.g("invalid_num_points")),
            //        Choices = new List<Choice>()
            //                        {
            //                            new Choice() { Value = "1" },
            //                            new Choice() { Value = "2" },
            //                            new Choice() { Value = "5" },
            //                            new Choice() { Value = "10" },
            //                        }
            //    };

            //    return await stepContext.PromptAsync("AskHowManyScanIDAsChoicePrompt", options, cancellationToken);
            //}

            //private async Task<DialogTurnResult> GetHowManyScanIDAsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
            //{
            //    //_logger.LogInformation($"ScanDialog.GetHowManyScanIDAsStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            //    int idacou;
            //    if (stepContext.Result == null)
            //    {
            //        idacou = int.Parse(stepContext.Context.Activity.Text); // manually inputted a number
            //    }
            //    else
            //    {
            //        idacou = int.Parse(((FoundChoice)stepContext.Result)?.Value);
            //    }
            var idacou = 1; // Skip actual AskHowManyIDAsStep for now becuase we've introduce Owl Tokens which this question would confuse people about how many are consumed

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context, () => new UserProfileTemporary());
            var actionHandler = new ActionHandler();

            switch (stepContext.Values["PointType"].ToString())
            {
                case "Attractor":
                    await actionHandler.AttractorActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true, idacou: idacou);
                    break;
                case "Void":
                    await actionHandler.VoidActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true, idacou: idacou);
                    break;
                case "Anomaly":
                    await actionHandler.AnomalyActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true, idacou: idacou);
                    break;
                case "Pair":
                    await actionHandler.PairActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true, idacou: idacou);
                    break;
            }

            // Long-running tasks like /getattractors etc will make use of ContinueDialog to re-prompt users
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private IList<Choice> GetActionChoices()
        {
            var actionOptions = new List<Choice>()
            {
                new Choice() {
                    Value = Loc.g("scan_attractor"),
                    Synonyms = new List<string>()
                                    {
                                        "scanattractor",
                                    }
                },
                new Choice() {
                    Value = Loc.g("scan_void"),
                    Synonyms = new List<string>()
                                    {
                                        "scanvoid",
                                    }
                },
                new Choice() {
                    Value = Loc.g("scan_anomaly"),
                    Synonyms = new List<string>()
                                    {
                                        "scananomaly",
                                    }
                },
                new Choice() {
                    Value = Loc.g("scan_pair"),
                    Synonyms = new List<string>()
                                    {
                                        "scanpair",
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
