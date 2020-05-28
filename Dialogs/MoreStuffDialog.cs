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
                Prompt = MessageFactory.Text("What would you like to get?  \nQuantum points are single random ones (potential Blind Spots).  Mystery Points are a random type of point.  \nAnomalies are the strongest out of Attractor and Void.\n"),
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

            switch (((FoundChoice)stepContext.Result)?.Value)
            {
                case "Quantum":
                    await actionHandler.QuantumActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
                    break;
                case "Intent Suggestions":
                    await actionHandler.IntentSuggestionActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
                    break;
                case "Mystery Point":
                    await actionHandler.MysteryPointActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
                    break;

                // Anomalies and Pairs were originally in the MainDialog so we fudge a way to here to go back to the MainDialog and skip to the GetNumIdas prompt
                // to avoid having to copy/paste half the MainDialog's code here (too lazy for proper refactoring)
                case "Anomaly":
                    callbackOptions.JumpToAskHowManyIDAs = "Anomaly";
                    return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken, options: callbackOptions);
                case "Pair":
                    callbackOptions.JumpToAskHowManyIDAs = "Pair";
                    return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken, options: callbackOptions);

                case "Quantum Time":
                    await actionHandler.QuantumActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, true);
                    break;
                case "Pseudo":
                    await actionHandler.PseudoActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog);
                    break;
                case "Scan":
                    await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                    return await stepContext.BeginDialogAsync(nameof(ScanDialog), this, cancellationToken);
                case "Chains":
                    return await stepContext.BeginDialogAsync(nameof(ChainsDialog), this, cancellationToken);
                case "Quantum Dice":
                    return await stepContext.BeginDialogAsync(nameof(QuantumDiceDialog), this, cancellationToken);

                //case "My Randotrips":
                //    await actionHandler.RandotripsActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, "my");
                //    break;
                //case "Today's Randotrips":
                //    await actionHandler.RandotripsActionAsync(stepContext.Context, userProfileTemporary, cancellationToken, _mainDialog, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                //    break;

                case "< Back":
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
                    Value = "Quantum",
                    Synonyms = new List<string>()
                                    {
                                        "quantum",
                                        "getquantum",
                                    }
                },
                new Choice() {
                    Value = "Intent Suggestions",
                    Synonyms = new List<string>()
                                    {
                                    }
                },
                new Choice() {
                    Value = "Mystery Point",
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
                    Value = "Anomaly",
                    Synonyms = new List<string>()
                                    {
                                        "anomaly",
                                        "getanomaly",
                                        "ida",
                                        "getida",
                                    }
                },
                new Choice() {
                    Value = "Pair",
                    Synonyms = new List<string>()
                                    {
                                        "pair",
                                        "getpair",
                                    }
                },
                new Choice() {
                    Value = "Quantum Time",
                    Synonyms = new List<string>()
                                    {
                                        "quantumtime",
                                        "getquantumtime",
                                        "qtime",
                                    }
                },
                new Choice() {
                    Value = "Pseudo",
                    Synonyms = new List<string>()
                                    {
                                        "pseudo",
                                        "getpseudo",
                                    }
                },
                new Choice() {
                    Value = "Scan",
                    Synonyms = new List<string>()
                                    {
                                        "scan",
                                    }
                },
                new Choice() {
                    Value = "Chains",
                    Synonyms = new List<string>()
                                    {
                                        "chains",
                                    }
                },
                new Choice() {
                    Value = "Quantum Dice",
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
                    Value = "< Back",
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
