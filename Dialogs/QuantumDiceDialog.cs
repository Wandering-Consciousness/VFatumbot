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
    public class QuantumDiceDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;
        protected readonly MainDialog _mainDialog;

        public QuantumDiceDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, MainDialog mainDialog, ILogger<MainDialog> logger) : base(nameof(QuantumDiceDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;
            _mainDialog = mainDialog;

            AddDialog(new NumberPrompt<int>("MinNumberPrompt", DiceMinValidatorAsync)
            {
            });
            AddDialog(new NumberPrompt<int>("MaxNumberPrompt", DiceMaxValidatorAsync)
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                EnterDiceMinStepAsync,
                EnterDiceMaxStepAsync,
                RollQDiceStepAsync,
                RollAgainStepAsync
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> EnterDiceMinStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("QuantumDiceDialog.EnterDiceMinStepAsync");


            int alreadyGotMin = -1;
            if (stepContext.Options != null)
            {
                try
                {
                    var minmax = (IDictionary<string, string>)stepContext.Options;
                    if (int.TryParse(minmax["Min"], out alreadyGotMin))
                    {
                        // Rolling again
                        stepContext.Values["Min"] = alreadyGotMin;
                        return await stepContext.NextAsync(cancellationToken: cancellationToken);
                    }
                } catch (Exception) { /* cast exception, bad hack, TLDR: fix later */ }
            }

            var promptOptions = new PromptOptions { Prompt = MessageFactory.Text(Loc.g("qd_min1")) };
            return await stepContext.PromptAsync("MinNumberPrompt", promptOptions, cancellationToken);
        }

        private async Task<bool> DiceMinValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            int inputtedDiceMin;
            if (!int.TryParse(promptContext.Context.Activity.Text, out inputtedDiceMin))
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_min2")), cancellationToken);
                return false;
            }

            if (inputtedDiceMin < 1)
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_min3")), cancellationToken);
                return false;
            }

            if (inputtedDiceMin > 254)
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_min4")), cancellationToken);
                return false;
            }

            return true;
        }

        private async Task<DialogTurnResult> EnterDiceMaxStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("QuantumDiceDialog.EnterDiceMaxStepAsync");

            int minValue;
            if (stepContext.Values != null && stepContext.Values.ContainsKey("Min"))
            {
                minValue = int.Parse(stepContext.Values["Min"].ToString());
            }
            else
            {
                minValue = (int)stepContext.Result;
            }
            stepContext.Values["Min"] = minValue;

            int alreadyGotMax = -1;
            if (stepContext.Options != null)
            {
                try
                {
                    var minmax = (IDictionary<string, string>)stepContext.Options;
                    if (int.TryParse(minmax["Max"], out alreadyGotMax))
                    {
                        // Rolling again
                        stepContext.Values["Max"] = alreadyGotMax;
                        return await stepContext.NextAsync(cancellationToken: cancellationToken);
                    }
                } catch (Exception) { /* cast exception, bad hack, TLDR: fix later */ }
            }

            var promptOptions = new PromptOptions { Prompt = MessageFactory.Text(Loc.g("qd_max1")) };
            return await stepContext.PromptAsync("MaxNumberPrompt", promptOptions, cancellationToken);
        }

        private async Task<bool> DiceMaxValidatorAsync(PromptValidatorContext<int> promptContext, CancellationToken cancellationToken)
        {
            int inputtedDiceMax;
            if (!int.TryParse(promptContext.Context.Activity.Text, out inputtedDiceMax))
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_max2")), cancellationToken);
                return false;
            }

            if (inputtedDiceMax <= 1)
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_max3")), cancellationToken);
                return false;
            }

            if (inputtedDiceMax > 0xff)
            {
                await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_max4")), cancellationToken);
                return false;
            }

            return true;
        }

        private async Task<DialogTurnResult> RollQDiceStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"QuantumDiceDialog.RollQDiceStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            int minValue = int.Parse(stepContext.Values["Min"].ToString());

            int maxValue;
            if (stepContext.Values != null && stepContext.Values.ContainsKey("Max"))
            {
                maxValue = int.Parse(stepContext.Values["Max"].ToString());
            }
            else
            {
                maxValue = (int)stepContext.Result;
            }

            if (minValue > maxValue)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_minmax", minValue, maxValue, minValue-1)), cancellationToken);
                minValue--;
                stepContext.Values["Min"] = minValue;
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("qd_rolling")), cancellationToken);

            var qrng = new QuantumRandomNumberGeneratorWrapper(stepContext.Context, _mainDialog, cancellationToken);
            var diceValue = qrng.Next(minValue, maxValue + 1);
            stepContext.Values["Max"] = maxValue;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("qd_results", diceValue, minValue, maxValue)),
                RetryPrompt = MessageFactory.Text(Loc.g("qd_onlyyess")),
                Choices = new List<Choice>()
                {
                    new Choice() {
                        Value = Loc.g("yes"),
                        Synonyms = new List<string>()
                                        {
                                            "yes",
                                        }
                    },
                    new Choice() {
                        Value = Loc.g("no"),
                        Synonyms = new List<string>()
                                        {
                                            "no",
                                        }
                    },
                }
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> RollAgainStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"QuantumDiceDialog.RollAgainStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            IDictionary<string, string> minmax = new Dictionary<string, string>
            {
                { "Min", stepContext.Values["Min"].ToString() },
                { "Max", stepContext.Values["Max"].ToString() }
            };

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (val.Equals(Loc.g("yes")))
            {
                return await stepContext.ReplaceDialogAsync(nameof(QuantumDiceDialog), options: minmax, cancellationToken: cancellationToken);
            }

            return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
        }
    }
}
