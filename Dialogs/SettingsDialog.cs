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

        public SettingsDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, ILogger<MainDialog> logger) : base(nameof(SettingsDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new ChoicePrompt("RadiusChoicePrompt",
              async (PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken) =>
              {
                  int inputtedRadius;
                  if (!int.TryParse(promptContext.Context.Activity.Text, out inputtedRadius))
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("invalid_radius")), cancellationToken);
                      return false;
                  }

                  if (inputtedRadius < Consts.RADIUS_MIN)
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("radius_gte", Consts.RADIUS_MIN)), cancellationToken);
                      return false;
                  }

                  var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(promptContext.Context);
                  if (userProfileTemporary.Has20kmRadius && inputtedRadius > 20000)
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("radius_lte", 20000)), cancellationToken);
                      return false;
                  }
                  else if (!userProfileTemporary.Has20kmRadius && inputtedRadius > Consts.RADIUS_MAX)
                  {
                      await promptContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("radius_lte", Consts.RADIUS_MAX)), cancellationToken);
                      return false;
                  }

                  return true;
              })
            {
            });
            AddDialog(new TextPrompt(nameof(TextPrompt))
            {
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
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> CurrentSettingsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.CurrentSettingsStepAsync");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            await ShowCurrentSettingsAsync(stepContext, cancellationToken);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions(Loc.g("update_settings_q"), userProfileTemporary.BotSrc), cancellationToken);
        }

        private async Task<DialogTurnResult> UpdateSettingsYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"SettingsDialog.UpdateSettingsYesOrNoStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val)) {
                // TODO: a quick hack to reset IsScanning in case it gets stuck in that state
                userProfileTemporary.IsScanning = false;
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Update Settings");
                await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);
                // << EOF TODO. Will figure out whether this needs handling properly later on.

                return await stepContext.NextAsync();
            //} else if (Loc.g("help").Equals(val)) {
                // case "Help" is picked up elsewhere
            } else {
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Don't Update Settings");
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken:cancellationToken);
            }
        }

        private async Task<DialogTurnResult> RadiusStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.RadiusStepAsync");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            if (userProfileTemporary.Has20kmRadius)
            {
                var promptOptionsExt = new PromptOptions
                {
                    Prompt = MessageFactory.Text(Loc.g("select_radius")),
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
                        new Choice() {
                            Value = "15000",
                        },
                        new Choice() {
                            Value = "20000",
                        },
                    }
                };
                return await stepContext.PromptAsync("RadiusChoicePrompt", promptOptionsExt, cancellationToken);
            }

            var promptOptions = new PromptOptions {
                Prompt = MessageFactory.Text(Loc.g("select_radius")),
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
            AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Set Radius", new Dictionary<string, object>() { {"Radius", inputtedRadius } });
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (userProfileTemporary.HasSkipWaterPoints)
                return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions(Loc.g("include_water_points_q"), userProfileTemporary.BotSrc), cancellationToken);

            return await stepContext.NextAsync();
        }

        private async Task<DialogTurnResult> UpdateWaterPointsYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("SettingsDialog.UpdateWaterPointsYesOrNoStepAsync");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (!userProfileTemporary.HasMapsPack)
                return await stepContext.NextAsync();

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val))
            {
                userProfileTemporary.IsIncludeWaterPoints = true;
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Set Skip Water Points", new Dictionary<string, object>() { { "IsSkip", userProfileTemporary.IsIncludeWaterPoints } });
            }
            else
            {
                userProfileTemporary.IsIncludeWaterPoints = false;
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Set Skip Water Points", new Dictionary<string, object>() { { "IsSkip", userProfileTemporary.IsIncludeWaterPoints } });
            }

            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions(Loc.g("show_google_street_earth_q"), userProfileTemporary.BotSrc), cancellationToken);
        }

        private async Task<DialogTurnResult> GoogleThumbnailsDisplayToggleStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"SettingsDialog.UpdateGoogleThumbnailsDisplayToggleStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            await _userProfileTemporaryAccessor.SetAsync(stepContext.Context, userProfileTemporary);

            if (!userProfileTemporary.HasMapsPack)
                return await stepContext.NextAsync();

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val))
            {
                userProfileTemporary.IsDisplayGoogleThumbnails = true;
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Set Display Google Thumbnails", new Dictionary<string, object>() { { "IsSkip", userProfileTemporary.IsDisplayGoogleThumbnails } });
            }
            else
            {
                userProfileTemporary.IsDisplayGoogleThumbnails = false;
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Set Display Google Thumbnails", new Dictionary<string, object>() { { "IsSkip", userProfileTemporary.IsDisplayGoogleThumbnails } });
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
                $"{Loc.g("anonymized_id_is", userProfileTemporary.UserId)}{Helpers.GetNewLine(stepContext.Context)}" +
                (userProfileTemporary.BotSrc == WebSrc.ios ? (!userProfileTemporary.HasSkipWaterPoints ? Loc.g("get_locsearch_skipwater_pack") : Loc.g("water_points_will_be", userProfileTemporary.IsIncludeWaterPoints ? Loc.g("included") : Loc.g("skipped"))) + Helpers.GetNewLine(stepContext.Context) : "") +
                (userProfileTemporary.BotSrc == WebSrc.ios ? (!userProfileTemporary.HasMapsPack ? Loc.g("get_maps_pack") : Loc.g("show_google_street_earth", userProfileTemporary.IsDisplayGoogleThumbnails ? Loc.g("displayed") : Loc.g("hidden"))) + Helpers.GetNewLine(stepContext.Context) : "") +
                $"{Loc.g("current_location", userProfileTemporary.Latitude.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture), userProfileTemporary.Longitude.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture))}{Helpers.GetNewLine(stepContext.Context)}" +
                $"{Loc.g("current_radius", userProfileTemporary.Radius)}{Helpers.GetNewLine(stepContext.Context)}"));
        }

        private PromptOptions GetPromptOptions(string prompt, WebSrc botSrc)
        {
            if (botSrc == WebSrc.ios || botSrc == WebSrc.android)
            {
                return new PromptOptions()
                {
                    Prompt = MessageFactory.Text(prompt),
                    RetryPrompt = MessageFactory.Text($"{Loc.g("invalid_answer")} {prompt}"),
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
                                    new Choice() {
                                        Value = Loc.g("help"),
                                    },
                                }
                };
            }
            else
            {
                return new PromptOptions()
                {
                    Prompt = MessageFactory.Text(prompt),
                    RetryPrompt = MessageFactory.Text($"{Loc.g("invalid_answer")} {prompt}"),
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
                                    new Choice() {
                                        Value = Loc.g("help"),
                                    },
                                }
                };
            }
        }
    }
}
