using System;
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
    public class MoreStuffDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;
        protected readonly MainDialog _mainDialog;

        public MoreStuffDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, MainDialog mainDialog, ILogger<MainDialog> logger) : base(nameof(MoreStuffDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;
            _mainDialog = mainDialog;

            AddDialog(new ChainsDialog(_userProfileTemporaryAccessor, mainDialog, logger));
            AddDialog(new QuantumDiceDialog(_userProfileTemporaryAccessor, mainDialog, logger));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ChoiceActionStepAsync,
                PerformActionStepAsync,
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ChoiceActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"BlindSpotsDialog.ChoiceActionStepAsync[{stepContext.Result}]");

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text("What would you like to get?  \nQuantum points are single random ones (potential Blind Spots).  Mystery Points are a random type of point."),
                RetryPrompt = MessageFactory.Text("That is not valid action."),
                Choices = GetActionChoices(),
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> PerformActionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"BlindSpotsDialog.PerformActionStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context, () => new UserProfileTemporary());
            var actionHandler = new ActionHandler();

            CallbackOptions callbackOptions = new CallbackOptions(); // for contiuing Anomalies and Pairs on the MainDialog

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("bs_quantum").Equals(val))
            {
                await actionHandler.QuantumActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
            }
            else if (Loc.g("bs_intent_suggestions").Equals(val))
            {
                await actionHandler.IntentSuggestionActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
            }
            else if (Loc.g("bs_mystery_point").Equals(val))
            {
                await actionHandler.MysteryPointActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
            }
            else if (Loc.g("bs_pair").Equals(val))
            {
                // Pairs was originally in the MainDialog so we fudge a way to here to go back to the MainDialog and skip to the GetNumIdas prompt
                // to avoid having to copy/paste half the MainDialog's code here (too lazy for proper refactoring)
                callbackOptions.JumpToAskHowManyIDAs = "Pair";
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken, options: callbackOptions);
            }
            else if (Loc.g("bs_quantum_time").Equals(val))
            {
                await actionHandler.QuantumActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true);
            }
            else if (Loc.g("bs_pseudo").Equals(val))
            {
                await actionHandler.PseudoActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
            }
            else if (Loc.g("bs_scan").Equals(val))
            {
                await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(ScanDialog), this, cancellationToken);
            }
            else if (Loc.g("bs_chains").Equals(val))
            {
                return await stepContext.BeginDialogAsync(nameof(ChainsDialog), this, cancellationToken);
            }
            else if (Loc.g("bs_dice").Equals(val))
            {
                return await stepContext.BeginDialogAsync(nameof(QuantumDiceDialog), this, cancellationToken);
            }
            //else if (Loc.g("bs_randotrips").Equals(val))
            //{
            //    //case "My Randotrips":
            //    //    await actionHandler.RandotripsActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, "my");
            //    //    break;
            //    //case "Today's Randotrips":
            //    //    await actionHandler.RandotripsActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, DateTime.UtcNow.ToString("yyyy-MM-dd"));
            //    //    break;
            //}
            else if (Loc.g("bs_back").Equals(val))
            {
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
            }

            // Long-running tasks like /getattractors etc will make use of ContinueDialog to re-prompt users
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private IList<Choice> GetActionChoices()
        {
            var actionOptions = new List<Choice>()
            {
                new Choice() {
                    Value = Loc.g("bs_quantum"),
                    Synonyms = new List<string>()
                                    {
                                        "quantum",
                                        "getquantum",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_intent_suggestions"),
                    Synonyms = new List<string>()
                                    {
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_mystery_point"),
                    Synonyms = new List<string>()
                                    {
                                        "Mystery point",
                                        "mystery point",
                                        "Point",
                                        "point",
                                        "getpoint",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_pair"),
                    Synonyms = new List<string>()
                                    {
                                        "pair",
                                        "getpair",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_quantum_time"),
                    Synonyms = new List<string>()
                                    {
                                        "quantumtime",
                                        "getquantumtime",
                                        "qtime",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_pseudo"),
                    Synonyms = new List<string>()
                                    {
                                        "pseudo",
                                        "getpseudo",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_scan"),
                    Synonyms = new List<string>()
                                    {
                                        "scan",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_chains"),
                    Synonyms = new List<string>()
                                    {
                                        "chains",
                                    }
                },
                new Choice() {
                    Value = Loc.g("bs_dice"),
                    Synonyms = new List<string>()
                                    {
                                        "quantum dice",
                                        "Dice",
                                        "dice",
                                    }
                },
                //new Choice() {
                //    Value = "My Randotrips",
                //    Synonyms = new List<string>()
                //                    {
                //                        "My randotrips",
                //                        "my randotrips",
                //                        "myrandotrips",
                //                    }
                //},
                //new Choice() {
                //    Value = "Today's Randotrips",
                //    Synonyms = new List<string>()
                //                    {
                //                        "Today's randotrips",
                //                        "Todays randotrips",
                //                        "today's randotrips",
                //                        "todays randotrips",
                //                        "randotrips",
                //                    }
                //},
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
