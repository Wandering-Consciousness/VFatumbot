using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using VFatumbot.BotLogic;
using static VFatumbot.BotLogic.Enums;

namespace VFatumbot
{
    public class SettingsDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;

        public SettingsDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, ILogger<MainDialog> logger, IBotTelemetryClient telemetryClient) : base(nameof(SettingsDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;

            TelemetryClient = telemetryClient;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
                TelemetryClient = telemetryClient,
            });
            AddDialog(new ChoicePrompt("RadiusChoicePrompt",
              async (PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken) =>
              {
                  int inputtedRadius;
                  if (!int.TryParse(promptContext.Context.Activity.Text, out inputtedRadius))
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text($"Invalid radius. Choose desired radius:"), cancellationToken);
                      return false;
                  }

                  if (inputtedRadius < Consts.RADIUS_MIN)
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text($"Radius must be more than or equal to {Consts.RADIUS_MIN}m. Enter desired radius:"), cancellationToken);
                      return false;
                  }

                  if (inputtedRadius > Consts.RADIUS_MAX)
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text($"Radius must be less than or equal to {Consts.RADIUS_MAX}m. Enter desired radius:"), cancellationToken);
                      return false;
                  }

                  return true;
              })
            {
                TelemetryClient = telemetryClient,
            });
            AddDialog(new TextPrompt(nameof(TextPrompt))
            {
                TelemetryClient = telemetryClient,
            });

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                CurrentSettingsStepAsync,
                UpdateSettingsYesOrNoStepAsync,
                RadiusStepAsync,
                WaterPointsStepAsync,
                UpdateWaterPointsYesOrNoStepAsync,
                GoogleThumbnailsDisplayToggleStepAsync,
                FinishSettingsStepAsync
            })
            {
                TelemetryClient = telemetryClient,
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CurrentSettingsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.CurrentSettingsStepAsync");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            await ShowCurrentSettingsAsync(stepContext, cancellationToken);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions("Update your settings?", userProfileTemporary.BotSrc), cancellationToken);
        }

        private async Task<DialogTurnResult> UpdateSettingsYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"SettingsDialog.UpdateSettingsYesOrNoStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            switch (((FoundChoice)stepContext.Result)?.Value)
            {
                case "Yes":
                    // TODO: a quick hack to reset IsScanning in case it gets stuck in that state
                    userProfileTemporary.IsScanning = false;
                    await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);
                    // << EOF TODO. Will figure out whether this needs handling properly later on.

                    return await stepContext.NextAsync();

                case "Add-ons":
                    // Send an EventActivity to for the webbot's JavaScript callback handler to pickup
                    // and then pass onto the app layer to load the native add-ons shop screen
                    var requestEntropyActivity = Activity.CreateEventActivity();
                    requestEntropyActivity.ChannelData = $"addons,{userProfileTemporary.UserId}";
                    await stepContext.Context.SendActivityAsync(requestEntropyActivity);
                    return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);

                // case "Help" is picked up elsewhere

                case "No":
                default:
                    return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken:cancellationToken);
            }
        }

        private async Task<DialogTurnResult> RadiusStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.RadiusStepAsync");

            var promptOptions = new PromptOptions {
                Prompt = MessageFactory.Text("Select your radius in meters, or enter the numbers directly:"),
                Choices = new List<Choice>()
                    {
                        new Choice() {
                            Value = "1000",
                        },
                        new Choice() {
                            Value = "3000",
                        },
                        new Choice() {
                            Value = "5000",
                        },
                        new Choice() {
                            Value = "10000",
                        },
                    }
            };
            return await stepContext.PromptAsync("RadiusChoicePrompt", promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> WaterPointsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"SettingsDialog.WaterPointsStepAsync");

            var inputtedRadius = int.Parse(stepContext.Context.Activity.Text);
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            userProfileTemporary.Radius = inputtedRadius;
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (userProfileTemporary.HasSkipWaterPoints)
                return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions("Include water points?", userProfileTemporary.BotSrc), cancellationToken);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> UpdateWaterPointsYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.UpdateWaterPointsYesOrNoStepAsync");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (!userProfileTemporary.HasMapsPack)
                return await stepContext.NextAsync();

            switch (((FoundChoice)stepContext.Result)?.Value)
            {
                case "Yes":
                    userProfileTemporary.IsIncludeWaterPoints = true;
                    break;

                case "Add-ons":
                    // Send an EventActivity to for the webbot's JavaScript callback handler to pickup
                    // and then pass onto the app layer to load the native add-ons shop screen
                    var requestEntropyActivity = Activity.CreateEventActivity();
                    requestEntropyActivity.ChannelData = $"addons,{userProfileTemporary.UserId}";
                    await stepContext.Context.SendActivityAsync(requestEntropyActivity);
                    break;

                case "No":
                default:
                    userProfileTemporary.IsIncludeWaterPoints = false;
                    break;
            }

            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions("Also display Google Street View and Earth previews?", userProfileTemporary.BotSrc), cancellationToken);
        }

        private async Task<DialogTurnResult> GoogleThumbnailsDisplayToggleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"SettingsDialog.UpdateGoogleThumbnailsDisplayToggleStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (!userProfileTemporary.HasMapsPack)
                return await stepContext.NextAsync();

            switch (((FoundChoice)stepContext.Result)?.Value)
            {
                case "Yes":
                    userProfileTemporary.IsDisplayGoogleThumbnails = true;
                    break;

                case "Add-ons":
                    // Send an EventActivity to for the webbot's JavaScript callback handler to pickup
                    // and then pass onto the app layer to load the native add-ons shop screen
                    var requestEntropyActivity = Activity.CreateEventActivity();
                    requestEntropyActivity.ChannelData = $"addons,{userProfileTemporary.UserId}";
                    await stepContext.Context.SendActivityAsync(requestEntropyActivity);
                    break;

                case "No":
                default:
                    userProfileTemporary.IsDisplayGoogleThumbnails = false;
                    break; ;
            }

            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> FinishSettingsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.FinishSettingsStepAsync");

            await ShowCurrentSettingsAsync(stepContext, cancellationToken);

            await stepContext.EndDialogAsync(cancellationToken: cancellationToken);

            var callbackOptions = new CallbackOptions();
            callbackOptions.UpdateSettings = true;

            return await stepContext.ReplaceDialogAsync(nameof(MainDialog), callbackOptions, cancellationToken);
        }

        public async Task ShowCurrentSettingsAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(
                $"Your anonymized ID is {userProfileTemporary.UserId}.{Helpers.GetNewLine(stepContext.Context)}" +
                (!userProfileTemporary.HasSkipWaterPoints ? "Get the Location Search and Skip Water Points Pack from the Add-ons button." : $"Water points will be {(userProfileTemporary.IsIncludeWaterPoints ? "included" : "skipped")}.") + Helpers.GetNewLine(stepContext.Context) +
                (!userProfileTemporary.HasMapsPack ? "Get the Maps Pack for in-app map and street previews from the Add-ons button." : $"Street View and Earth previews will be {(userProfileTemporary.IsDisplayGoogleThumbnails ? "displayed" : "hidden")}.") + Helpers.GetNewLine(stepContext.Context) +
                $"Current location is {userProfileTemporary.Latitude.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture)},{userProfileTemporary.Longitude.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture)}.{Helpers.GetNewLine(stepContext.Context)}" +
                $"Current radius is {userProfileTemporary.Radius}m.{Helpers.GetNewLine(stepContext.Context)}"));
        }

        private PromptOptions GetPromptOptions(string prompt, WebSrc botSrc)
        {
            if (botSrc == WebSrc.ios || botSrc == WebSrc.android)
            {
                return new PromptOptions()
                {
                    Prompt = MessageFactory.Text(prompt),
                    RetryPrompt = MessageFactory.Text($"That is not a valid answer. {prompt}"),
                    Choices = new List<Choice>()
                                {
                                    new Choice() {
                                        Value = "Yes",
                                        Synonyms = new List<string>()
                                                        {
                                                            "yes",
                                                        }
                                    },
                                    new Choice() {
                                        Value = "No",
                                        Synonyms = new List<string>()
                                                        {
                                                            "no",
                                                        }
                                    },
                                    new Choice() {
                                        Value = "Add-ons",
                                          Synonyms = new List<string>()
                                                        {
                                                            "Addons",
                                                            "add-ons",
                                                            "addons"
                                                        }
                                    },
                                    new Choice() {
                                        Value = "Help",
                                    },
                                }
                };
            }
            else
            {
                return new PromptOptions()
                {
                    Prompt = MessageFactory.Text(prompt),
                    RetryPrompt = MessageFactory.Text($"That is not a valid answer. {prompt}"),
                    Choices = new List<Choice>()
                                {
                                    new Choice() {
                                        Value = "Yes",
                                        Synonyms = new List<string>()
                                                        {
                                                            "yes",
                                                        }
                                    },
                                    new Choice() {
                                        Value = "No",
                                        Synonyms = new List<string>()
                                                        {
                                                            "no",
                                                        }
                                    },
                                    new Choice() {
                                        Value = "Help",
                                    },
                                }
                };
            }
        }
    }
}
