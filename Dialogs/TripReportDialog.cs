﻿using Reddit;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using VFatumbot.BotLogic;
using static VFatumbot.BotLogic.Enums;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Reddit.Controllers;

namespace VFatumbot
{
    public class TripReportDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;
        protected readonly MainDialog _mainDialog;

        private const string ReportAnswersKey = "value-ReportAnswers";

        public class ReportAnswers {
            public bool WasPointVisited { get; set; }
            public int PointNumberVisited { get; set; }

            public bool SkipGetIntentStep { get; set; }
            public string Intent { get; set; }

            public bool ArtifactCollected { get; set; }

            public bool WasFuckingAmazing { get; set; }

            public string Rating_Meaningfulness { get; set; }
            public string Rating_Emotional { get; set; }
            public string Rating_Importance { get; set; }
            public string Rating_Strangeness { get; set; }
            public string Rating_Synchronicty { get; set; }

            public string[] PhotoURLs { get; set; }

            public string Report { get; set; }
        }

        public TripReportDialog(IStatePropertyAccessor<UserProfileTemporary> userProfileTemporaryAccessor, MainDialog mainDialog, ILogger<MainDialog> logger) : base(nameof(TripReportDialog))
        {
            _logger = logger;
            _userProfileTemporaryAccessor = userProfileTemporaryAccessor;
            _mainDialog = mainDialog;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new ChoicePrompt("AllowFreetextTooChoicePrompt",
                (PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken) =>
                    {
                        // forced true validater result to also allow free text entry for ratings
                        return Task.FromResult(true);
                    })
            {
            });
            AddDialog(new TextPrompt(nameof(TextPrompt))
            {
            });
            AddDialog(new AttachmentPrompt(nameof(AttachmentPrompt))
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                ReportYesOrNoStepAsync,
                StartReportStepAsync,
                SetIntentYesOrNoStepAsync,
                GetIntentStepAsync,
                ArtifactsCollectedYesOrNoStepAsync,
                FuckingAmazingYesOrNoStepAsync,
                RateMeaningfulnessStepAsync,
                RateEmotionalStepAsync,
                RateImportanceStepAsync,
                RateStrangenessStepAsync,
                RateSynchronictyStepAsync,
                //UploadPhotosYesOrNoStepAsync,
                //GetPhotoAttachmentsStepAsync,
                WriteReportStepAsync,
                FinishStepAsync
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> ReportYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("TripReportDialog.ReportYesOrNoStepAsync");

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_did_you_visit")),
                RetryPrompt = MessageFactory.Text(Loc.g("tr_invalid_choice")),
                Choices = new List<Choice>()
                            {
                                new Choice() {
                                    Value = Loc.g("no"),
                                    Synonyms = new List<string>()
                                                    {
                                                        "no"
                                                    }
                                },
                                new Choice() {
                                    Value = Loc.g("tr_yes_report"),
                                    Synonyms = new List<string>()
                                                    {
                                                        "Yes",
                                                        "yes"
                                                    }
                                },
                                new Choice() {
                                    Value = Loc.g("tr_yes_sans_report"),
                                    Synonyms = new List<string>()
                                                    {
                                                        "Report",
                                                        "report"
                                                    }
                                }
                }
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> StartReportStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.StartReportStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            var callbackOptions = (CallbackOptions)stepContext.Options;

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("tr_yes_report").Equals(val))
            {
                // Go and start asking them about their trip

                var answers = new ReportAnswers() { WasPointVisited = true };
                stepContext.Values[ReportAnswersKey] = answers;

                // TODO: [answers.PointNumberVisited] : implement the dialog steps/logic to ask this.

                switch (callbackOptions.PointTypes[answers.PointNumberVisited])
                {
                    case PointTypes.Attractor:
                    case PointTypes.Void:
                    case PointTypes.Anomaly:
                    case PointTypes.PairAttractor:
                    case PointTypes.PairVoid:
                    case PointTypes.ScanAttractor:
                    case PointTypes.ScanVoid:
                    case PointTypes.ScanAnomaly:
                    case PointTypes.ScanPair:
                    case PointTypes.ChainAttractor:
                    case PointTypes.ChainVoid:
                    case PointTypes.ChainAnomaly:
                        var options = new PromptOptions()
                        {
                            Prompt = MessageFactory.Text(Loc.g("tr_set_intent_q")),
                            RetryPrompt = MessageFactory.Text(Loc.g("tr_invalid_choice")),
                            Choices = new List<Choice>()
                            {
                                new Choice() { Value = Loc.g("yes") },
                                new Choice() { Value = Loc.g("no") }
                            }
                        };

                        return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
                }

                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Visited Point with Trip Report");

                ((ReportAnswers)stepContext.Values[ReportAnswersKey]).SkipGetIntentStep = true;
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            } else if (Loc.g("tr_yes_sans_report").Equals(val)) {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("tr_hope_had_fun")), cancellationToken);

                // At least mark the point as a visited one
                await StoreReportInDB(stepContext.Context, callbackOptions, new ReportAnswers() { WasPointVisited = true });

                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Visited Point sans Trip Report");

                await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
            } else {
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Didn't Visit Point");
                await StoreReportInDB(stepContext.Context, callbackOptions, new ReportAnswers());
                await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
                return await stepContext.BeginDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
            }
        }

        private async Task<DialogTurnResult> SetIntentYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            if (answers.SkipGetIntentStep)
            {
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }

            //_logger.LogInformation($"TripReportDialog.SetIntentYesOrNoStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val))
            {
                var promptOptions = new PromptOptions { Prompt = MessageFactory.Text(Loc.g("tr_enter_intent")) };
                return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
            }

           return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> GetIntentStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.GetIntentStepAsync");

            if (stepContext.Result == null)
            {
                // Assume they selected "No" for no intent set and skip
                return await stepContext.NextAsync(cancellationToken: cancellationToken);
            }

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];
            answers.Intent = (string)stepContext.Result;

            return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> ArtifactsCollectedYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.ArtifactsCollectedYesOrNoStepAsync");

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_collect_artifacts_q")),
                RetryPrompt = MessageFactory.Text(Loc.g("tr_invalid_choice")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("yes") },
                                    new Choice() { Value = Loc.g("no") }
                                }
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> FuckingAmazingYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.FuckingAmazingYesOrNoStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            if (Loc.g("yes").Equals(((FoundChoice)stepContext.Result)?.Value))
            {
               answers.ArtifactCollected = true;
            }

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_wow_and_astounding_q")),
                RetryPrompt = MessageFactory.Text(Loc.g("tr_invalid_choice")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("yes") },
                                    new Choice() { Value = Loc.g("no") }
                                }
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> RateMeaningfulnessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.RateMeaningfulnessStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val))
            {
               answers.WasFuckingAmazing = true;
            }

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_meaningfulness_q")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("tr_enriching") },
                                    new Choice() { Value = Loc.g("tr_meaningful") },
                                    new Choice() { Value = Loc.g("tr_casual") },
                                    new Choice() { Value = Loc.g("tr_meaningless") },
                                },
            };

            return await stepContext.PromptAsync("AllowFreetextTooChoicePrompt", options, cancellationToken);
        }

        private async Task<DialogTurnResult> RateEmotionalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.RateEmotionalStepAsync");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            answers.Rating_Meaningfulness = stepContext.Context.Activity.Text;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_emotional_q")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("tr_dopamine_hit") },
                                    new Choice() { Value = Loc.g("tr_inspirational") },
                                    new Choice() { Value = Loc.g("tr_plain") },
                                    new Choice() { Value = Loc.g("tr_anxious") },
                                    new Choice() { Value = Loc.g("tr_despair") },
                                    new Choice() { Value = Loc.g("tr_dread") },
                                }
            };

            return await stepContext.PromptAsync("AllowFreetextTooChoicePrompt", options, cancellationToken);
        }

        private async Task<DialogTurnResult> RateImportanceStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.RateImportanceStepAsync");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            answers.Rating_Emotional = stepContext.Context.Activity.Text;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_importance_q")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("tr_lifechanging") },
                                    new Choice() { Value = Loc.g("tr_influential") },
                                    new Choice() { Value = Loc.g("tr_ordinary") },
                                    new Choice() { Value = Loc.g("tr_waste_time") },
                                }
            };

            return await stepContext.PromptAsync("AllowFreetextTooChoicePrompt", options, cancellationToken);
        }

        private async Task<DialogTurnResult> RateStrangenessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.RateStrangenessStepAsync");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            answers.Rating_Importance = stepContext.Context.Activity.Text;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_strangeness_q")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("tr_woowooweird") },
                                    new Choice() { Value = Loc.g("tr_strange") },
                                    new Choice() { Value = Loc.g("tr_normal") },
                                    new Choice() { Value = Loc.g("tr_nothing") },
                                }
            };

            return await stepContext.PromptAsync("AllowFreetextTooChoicePrompt", options, cancellationToken);
        }

        private async Task<DialogTurnResult> RateSynchronictyStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.RateSynchronictyStepAsync");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            answers.Rating_Strangeness = stepContext.Context.Activity.Text;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_synchronicity_q")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("tr_dirk_gently") },
                                    new Choice() { Value = Loc.g("tr_mind_blowing") },
                                    new Choice() { Value = Loc.g("tr_somewhat") },
                                    new Choice() { Value = Loc.g("tr_nothing") },
                                    new Choice() { Value = Loc.g("tr_boredom") },
                                }
            };

            return await stepContext.PromptAsync("AllowFreetextTooChoicePrompt", options, cancellationToken);
        }

        private async Task<DialogTurnResult> UploadPhotosYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.UploadPhotosYesOrNoStepAsync");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            answers.Rating_Synchronicty = stepContext.Context.Activity.Text;

            var options = new PromptOptions()
            {
                Prompt = MessageFactory.Text(Loc.g("tr_wanna_upload_photos_q")),
                RetryPrompt = MessageFactory.Text(Loc.g("tr_invalid_choice")),
                Choices = new List<Choice>()
                                {
                                    new Choice() { Value = Loc.g("yes") },
                                    new Choice() { Value = Loc.g("no") }
                                }
            };

            return await stepContext.PromptAsync(nameof(ChoicePrompt), options, cancellationToken);
        }

        private async Task<DialogTurnResult> GetPhotoAttachmentsStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.GetPhotoAttachmentsStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (Loc.g("yes").Equals(val))
            {
                var promptOptions = new PromptOptions {
                    Prompt = MessageFactory.Text(Loc.g("tr_upload_photos_now")),
                    RetryPrompt = MessageFactory.Text(Loc.g("tr_not_valid_upload")),
                };
                return await stepContext.PromptAsync(nameof(AttachmentPrompt), promptOptions, cancellationToken);
            }

           return await stepContext.NextAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> WriteReportStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.WriteReportStepAsync");

            var callbackOptions = (CallbackOptions)stepContext.Options;
            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);

            // TODO: remove if we ever reinstate the two functions above for uploading photos
            answers.Rating_Synchronicty = stepContext.Context.Activity.Text;

            try
            {
                if (stepContext.Context.Activity.Attachments != null && stepContext.Context.Activity.Attachments.Count >= 1)
                {
                    // Intercept image attachments here
                    foreach (Attachment attachment in stepContext.Context.Activity.Attachments)
                    {
                        if (answers.PhotoURLs == null)
                        {
                            answers.PhotoURLs = new string[] { };
                        }

                        if (attachment.ContentType.StartsWith("image/", StringComparison.InvariantCulture))
                        {
                            var webClient = new WebClient();
                            byte[] attachmentImgBytes = webClient.DownloadData(attachment.ContentUrl);

                            // Upload to Imgur
                            // uses: https://github.com/lauchacarro/Imgur-NetCore
                            var client = new ImgurClient(Consts.IMGUR_API_CLIENT_ID, Consts.IMGUR_API_CLIENT_SECRET);
                            var endpoint = new ImageEndpoint(client);
                            var image = await endpoint.UploadImageUrlAsync(
                                //"http://randonauts.com/randonauts.jpg",
                                attachment.ContentUrl,
                                title: ("Randonaut Trip Report Photo" + ((callbackOptions.NearestPlaces != null && callbackOptions.NearestPlaces.Length >= 1) ? (" from " + callbackOptions.NearestPlaces[answers.PointNumberVisited]) : " from somewhere in the multiverse")), // TODO fuck I should stop trying to condense so much into one line in C#. I'm just drunk and lazy ATM. Now I'm just copy/pasting the same code in the morning sober... I'll come back to this really long one day and laugh :D
                                description: (userProfileTemporary.UserId + " " + callbackOptions.ShortCodes[answers.PointNumberVisited])
                                );
                            answers.PhotoURLs = answers.PhotoURLs.Concat(new string[] { image.Link }).ToArray();

                            // Code for if passing the photo URLs over to reddit self posting logic directly
                            //answers.PhotoURLs = answers.PhotoURLs.Concat(new string[] { attachment.ContentUrl }).ToArray();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"{Loc.g("tr_error_photo_upload")}: ({e.GetType().Name}: {e.Message})"));
            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("tr_photo_uploads_temp_disabled")), cancellationToken);
            var promptOptions = new PromptOptions { Prompt = MessageFactory.Text(Loc.g("tr_write_report")) };
            return await stepContext.PromptAsync(nameof(TextPrompt), promptOptions, cancellationToken);
        }

        private async Task<DialogTurnResult> FinishStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"TripReportDialog.FinishStepAsync");

            var callbackOptions = (CallbackOptions)stepContext.Options;
            var answers = (ReportAnswers)stepContext.Values[ReportAnswersKey];
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(stepContext.Context);
            answers.Report = "" + stepContext.Result;

            await StoreReportInDB(stepContext.Context, callbackOptions, answers);

            var intentSuggestions = "";
            if (userProfileTemporary.IntentSuggestions != null && userProfileTemporary.IntentSuggestions.Length > 0)
            {
                intentSuggestions = string.Join(", ", userProfileTemporary.IntentSuggestions) + "\n\n";
            }

            // Prep photo URLs
            string photos = "";
            if (answers.PhotoURLs != null)
            {
                int i = 0;
                foreach (string photoURL in answers.PhotoURLs)
                {
                    i++;
                    photos += $"[Trip Photo #{i}]({photoURL})  \n";
                }
            }

            var incoords = new double[] { callbackOptions.GeneratedPoints[answers.PointNumberVisited].X.center.point.latitude,
                                          callbackOptions.GeneratedPoints[answers.PointNumberVisited].X.center.point.longitude };


            string message = "";
            if (callbackOptions.PointTypes[0].ToString().Contains("Chain"))
            {
                for (int i = 0; i < callbackOptions.Messages.Length; i++)
                {
                    var pointMsg = $"No. {i + 1} {callbackOptions.Messages[answers.PointNumberVisited]}";

                    // Prefix Type: with Chain
                    pointMsg = pointMsg.Replace("Type: ", "Type: Chain");

                    // Remove bearing info from reports
                    pointMsg = pointMsg.Substring(0, pointMsg.IndexOf("Bearing:", StringComparison.InvariantCulture)) + pointMsg.Substring(pointMsg.IndexOf("°", StringComparison.InvariantCulture) + 1).Replace("\n", "");

                    // Concat
                    pointMsg += "  \n\n\n";
                    message += pointMsg;

                    // Remove trespass warning
                    message = message.Replace(Loc.g("dont_tresspass"), "");
                }
            }
            else
            {
                message = callbackOptions.Messages[answers.PointNumberVisited];

                if (message != null && message.Contains("Bearing:"))
                {
                    // Remove bearing info from reports
                    message = message.Substring(0, message.IndexOf("Bearing:", StringComparison.InvariantCulture)) + message.Substring(message.IndexOf("°", StringComparison.InvariantCulture) + 1).Replace("\n", "");
                }

                // Remove trespass warning
                message = message.Replace(Loc.g("dont_tresspass"), "");
            }

            var redditPost = await PostTripReportToRedditAsync("Randonaut Trip Report"
                + ((callbackOptions.NearestPlaces != null && callbackOptions.NearestPlaces.Length >= 1) ? (" from " + callbackOptions.NearestPlaces[answers.PointNumberVisited]) : " from somewhere in the multiverse"), // TODO fuck I should stop trying to condense so much into one line in C#. I'm just drunk and lazy ATM.
                message.Replace("\n\n", "  \n") + "\n\n" +
                "Report: " + answers.Report + "\n   \n" +
                (!string.IsNullOrEmpty(photos) ? photos + "\n\n" : "\n\n") +
                (!string.IsNullOrEmpty(callbackOptions.What3Words[answers.PointNumberVisited]) ? "First point what3words address: [" + callbackOptions.What3Words[answers.PointNumberVisited] + "](https://what3words.com/" + callbackOptions.What3Words[answers.PointNumberVisited] + ")  \n" : "  \n") +
                "[Google Maps](https://www.google.com/maps/place/" + incoords[0] + "+" + incoords[1] + "/@" + incoords[0] + "+" + incoords[1] + ",18z)  |  " +
                "[Google Earth](https://earth.google.com/web/search/" + incoords[0] + "," + incoords[1] + ")\n\n" +
                (!string.IsNullOrEmpty(answers.Intent) ? "Intent set: " + answers.Intent + "  \n" : "") +
                (!string.IsNullOrEmpty(userProfileTemporary.LastRNGType) ? "RNG: " + userProfileTemporary.LastRNGType + "  \n" : "") +
                (!string.IsNullOrEmpty(intentSuggestions) ? "Intents suggested: " + intentSuggestions + "  \n" : "") +
                "Artifact(s) collected? " + (answers.ArtifactCollected ? "Yes" : "No") + "  \n" +
                "Was a 'wow and astounding' trip? " + (answers.WasFuckingAmazing ? "Yes" : "No") + "  \n" +
                "## Trip Ratings  \n" +
                "Meaningfulness: " + answers.Rating_Meaningfulness + "  \n" +
                "Emotional: " + answers.Rating_Emotional + "  \n" +
                "Importance: " + answers.Rating_Importance + "  \n" +
                "Strangeness: " + answers.Rating_Strangeness + "  \n" +
                "Synchronicity: " + answers.Rating_Synchronicty + "  \n" +
                 "\n\n" +
                userProfileTemporary.UserId + " " + callbackOptions.ShortCodes[answers.PointNumberVisited] + " " + callbackOptions.ShaGids?[answers.PointNumberVisited],
                answers.PhotoURLs,
                "randonaut_reports"
                );

            if (answers.Report.Length >= 150 || !string.IsNullOrEmpty(photos)) // also post a short version to /r/randonauts if we deem it interesting)
            {
                var oldLines = message.Split("\n");
                var newLines = oldLines.Where(line => !line.Contains(Loc.g("ida_found")));
                newLines = newLines.Where(line => !line.Contains("(")); // A-8FF89AC5 (43.105433 -76.121310)
                newLines = newLines.Where(line => !line.Contains("Radius")); // Radius
                var shortMessage = string.Join("\n", newLines);
                shortMessage = shortMessage.Replace("\n\n\n", "\n\n");
                if (!string.IsNullOrEmpty(userProfileTemporary.LastRNGType))
                {
                    shortMessage += "  \nRNG: " + userProfileTemporary.LastRNGType + "  \n";
                }

                await PostTripReportToRedditAsync(
                    (!string.IsNullOrEmpty(answers.Intent) ? answers.Intent + " @" : "Trip report @")
                        + ((callbackOptions.NearestPlaces != null && callbackOptions.NearestPlaces.Length >= 1) ? (" " + callbackOptions.NearestPlaces[answers.PointNumberVisited]).Substring(0, callbackOptions.NearestPlaces[answers.PointNumberVisited].LastIndexOf("(")) : " somewhere"),
                    answers.Report + "\n   \n" +
                    (!string.IsNullOrEmpty(photos) ? photos + "\n\n" : "\n\n") +
                    shortMessage.Replace("\n\n", "  \n") + "\n\n" +
                    "[Google Maps](https://www.google.com/maps/place/" + incoords[0] + "+" + incoords[1] + "/@" + incoords[0] + "+" + incoords[1] + ",18z)  |  " +
                    $"[Full Report](https://redd.it/{redditPost.Id})\n\n",
                    answers.PhotoURLs,
                    "randonauts"
                );
            }

            var w3wHashes = $" #{callbackOptions.What3Words[answers.PointNumberVisited].Replace(".", " #")}";

            var tweetReport = Uri.EscapeDataString(answers.Report.Substring(0, Math.Min(220 - w3wHashes.Length, answers.Report.Length)));
            await stepContext.Context.SendActivityAsync(MessageFactory.Text($"[{Loc.g("tr_tweet")}](https://twitter.com/intent/tweet?text={tweetReport}%20https://redd.it/{redditPost.Id}%20%23randonauts%20%23randonaut_reports{w3wHashes.Replace(" #", "%20%23")})"), cancellationToken);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("tr_thanks")), cancellationToken);
            AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Trip Report Posted");

            //await ((AdapterWithErrorHandler)stepContext.Context.Adapter).RepromptMainDialog(stepContext.Context, _mainDialog, cancellationToken, callbackOptions);
            callbackOptions = new CallbackOptions() { JustPostedTripReport = true };
            return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken, options: callbackOptions);
        }

        // Post a trip report to the /r/randonauts subreddit
        // Reddit API used: https://github.com/sirkris/Reddit.NET/
        protected async Task<SelfPost> PostTripReportToRedditAsync(string title, string text, string[] photoURLs, string subredditPage)
        {
            // all posts are done under the user "therealfatumbot"
            var redditApi = new RedditAPI(appId: Consts.REDDIT_APP_ID,
                                          appSecret: Consts.REDDIT_APP_SECRET,
                                          refreshToken: Consts.REDDIT_REFRESH_TOKEN,
                                          accessToken: Consts.REDDIT_ACCESS_TOKEN);

#if RELEASE_PROD
            var subreddit = redditApi.Subreddit(subredditPage);
#else
            var subreddit = redditApi.Subreddit("soliaxplayground");
#endif

            // Just seeing if we can upload images, was getting 403 error responses, even so it would be uploaded to the subreddit itself, not the user's post.
            // TODO: one day figure if we can upload images to posts
            //string photos = "";
            //if (photoURLs != null)
            //{
            //    int i = 0;
            //    foreach (string photoURL in photoURLs)
            //    {
            //        var webClient = new WebClient();
            //        byte[] imageBytes = webClient.DownloadData(photoURL);

            //        i++;
            //        ImageUploadResult imgRes = await subreddit.UploadImgAsync(imageBytes, $"Trip Photo #{i}");
            //        photos += $"![Trip Photo #{i}]({imgRes.ImgSrc})" + "\n\n";
            //    }

            //    text += "\n\n" + photos;
            //}

            return await subreddit.SelfPost(title: title, selfText: text).SubmitAsync();
        }

        private async Task StoreReportInDB(ITurnContext context, CallbackOptions options, ReportAnswers answers)
        {
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(context);

            await Task.Run(() =>
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = Consts.DB_SERVER;
                builder.UserID = Consts.DB_USER;
                builder.Password = Consts.DB_PASSWORD;
                builder.InitialCatalog = Consts.DB_NAME;

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();

                    for (int i = 0; i < options.GeneratedPoints.Length; i++)
                    {
                        var attractor = options.GeneratedPoints[i].X;

                        StringBuilder isb = new StringBuilder();
#if RELEASE_PROD
                        isb.Append("INSERT INTO reports (");
#else
                        isb.Append("INSERT INTO reports_dev (");
#endif
                        // isb.Append("id,"); Automatically incremented from the CREATE TABLE... id uniqueidentifier default NEWSEQUENTIALID() primary key command
                        isb.Append("user_id,");
                        isb.Append("platform,");
                        isb.Append("datetime,");
                        isb.Append("visited,");
                        isb.Append("rng_type,");
                        isb.Append("point_type,");
                        if (!string.IsNullOrEmpty(answers.Intent))
                        {
                            isb.Append("intent_set,");
                        }
                        isb.Append("artifact_collected,");
                        isb.Append("fucking_amazing,");
                        isb.Append("rating_meaningfulness,");
                        isb.Append("rating_emotional,");
                        isb.Append("rating_importance,");
                        isb.Append("rating_strangeness,");
                        isb.Append("rating_synchroncity,");
                        isb.Append("text,");
                        isb.Append("photos,");
                        if (userProfileTemporary.IntentSuggestions != null && userProfileTemporary.IntentSuggestions.Length > 0)
                        {
                            isb.Append("intent_suggestions,");
                            isb.Append("time_intent_suggestions_set,");
                        }
                        isb.Append("what_3_words,");
                        if (!string.IsNullOrEmpty(options.NearestPlaces[i]) && options.NearestPlaces[i].Contains("("))
                        {
                            isb.Append("nearest_place,");
                            isb.Append("country,");
                        }
                        isb.Append("short_hash_id,");
                        isb.Append("num_water_points_skipped,");
                        isb.Append("gid,");
                        isb.Append("tid,");
                        isb.Append("lid,");
                        isb.Append("idastep,");
                        isb.Append("idacount,");
                        isb.Append("type,");
                        isb.Append("x,");
                        isb.Append("y,");
                        isb.Append("center,");
                        isb.Append("latitude,");
                        isb.Append("longitude,");
                        if (attractor.center.bearing != null)
                        { 
                            isb.Append("distance,");
                            isb.Append("initial_bearing,");
                            isb.Append("final_bearing,");
                        }
                        isb.Append("side,");
                        isb.Append("distance_err,");
                        isb.Append("radiusM,");
                        isb.Append("number_points,");
                        isb.Append("mean,");
                        isb.Append("rarity,");
                        isb.Append("power_old,");
                        isb.Append("power,");
                        isb.Append("z_score,");
                        isb.Append("probability_single,");
                        isb.Append("integral_score,");
                        isb.Append("significance,");
                        isb.Append("probability");
                        isb.Append(") VALUES (");
                        isb.Append($"'{userProfileTemporary.UserId}',"); // sha256 hash of channel-issued userId
                        isb.Append($"'{(int)Enum.Parse(typeof(Enums.ChannelPlatform), context.Activity.ChannelId)}',");
                        isb.Append($"'{context.Activity.Timestamp}',"); // datetime
                        isb.Append($"'{(answers.WasPointVisited ? 1 : 0)}',"); // point visited or not?
                        isb.Append($"'{userProfileTemporary.LastRNGType}',"); // RNG type used
                        isb.Append($"'{options.PointTypes[i].ToString()}',"); // point type enum as a string
                        if (!string.IsNullOrEmpty(answers.Intent))
                        {
                            isb.Append($"'{SanitizeString(answers.Intent)}',"); // intent set by user
                        }
                        isb.Append($"'{(answers.ArtifactCollected ? 1 : 0)}',"); // were artifact(s) collected?
                        isb.Append($"'{(answers.WasFuckingAmazing ? 1 : 0)}',"); // "yes" or "no" to the was it wow and astounding question
                        isb.Append($"'{SanitizeString(answers.Rating_Meaningfulness)}',"); // Rating_Meaningfulness
                        isb.Append($"'{SanitizeString(answers.Rating_Emotional)}',"); // Rating_Emotional
                        isb.Append($"'{SanitizeString(answers.Rating_Importance)}',"); // Rating_Importance
                        isb.Append($"'{SanitizeString(answers.Rating_Strangeness)}',"); // Rating_Strangeness
                        isb.Append($"'{SanitizeString(answers.Rating_Synchronicty)}',"); // Rating_Synchronicty
                        isb.Append($"'{SanitizeString(answers.Report)}',"); // text
                        isb.Append($"'{(answers.PhotoURLs != null ? string.Join(",", answers.PhotoURLs) : "")}',"); // photos
                        if (userProfileTemporary.IntentSuggestions != null && userProfileTemporary.IntentSuggestions.Length > 0)
                        {
                            isb.Append($"'{string.Join(",", SanitizeString(userProfileTemporary.IntentSuggestions))}',"); // intent suggestions
                            isb.Append($"'{userProfileTemporary.TimeIntentSuggestionsSet}',");
                        }
                        isb.Append($"'{(!string.IsNullOrEmpty(options.What3Words[i]) ? options.What3Words[i] : "")}',");
                        if (!string.IsNullOrEmpty(options.NearestPlaces[i]) && options.NearestPlaces[i].Contains("("))
                        {
                            isb.Append($"'{options.NearestPlaces[i].Substring(0, options.NearestPlaces[i].IndexOf("(") - 1)}',");
                            isb.Append($"'{options.NearestPlaces[i].Substring(options.NearestPlaces[i].IndexOf("(") + 1).Replace(")", "")}',");
                        }
                        isb.Append($"'{options.ShortCodes[i]}',");
                        isb.Append($"'{options.NumWaterPointsSkipped[i]}',");

                        //isb.Append($"'{attractor.GID}',");// was hardcoded at 23 in Fatumbot3
                        var shaGid = "";
                        if (options.ShaGids != null && options.ShaGids.Length >= 1)
                        {
                            shaGid = (options.ShaGids.Length == 1 ? options.ShaGids[0] : options.ShaGids[i]);
                        }
                        isb.Append($"'{shaGid}',");

                        isb.Append($"'{attractor.TID}',");
                        isb.Append($"'{attractor.LID}',");
                        isb.Append($"'{i+1}',"); // idastep (which element in idacount array)
                        isb.Append($"'{options.GeneratedPoints.Length}',"); // total idacount
                        isb.Append($"'{attractor.type}',");
                        isb.Append($"'{attractor.x}',");
                        isb.Append($"'{attractor.y}',");
                        isb.Append($"geography::Point({attractor.center.point.latitude},{attractor.center.point.longitude}, 4326),");
                        isb.Append($"'{attractor.center.point.latitude}',");
                        isb.Append($"'{attractor.center.point.longitude}',");
                        if (attractor.center.bearing != null)
                        {
                            isb.Append($"'{attractor.center.bearing.distance}',");
                            isb.Append($"'{attractor.center.bearing.initialBearing}',");
                            isb.Append($"'{attractor.center.bearing.finalBearing}',");
                        }
                        isb.Append($"'{attractor.side}',");
                        isb.Append($"'{attractor.distanceErr}',");
                        isb.Append($"'{attractor.radiusM}',");
                        isb.Append($"'{attractor.n}',");
                        isb.Append($"'{attractor.mean}',");
                        isb.Append($"'{attractor.rarity}',");
                        isb.Append($"'{attractor.power_old}',");
                        isb.Append($"'{attractor.power}',");
                        isb.Append($"'{attractor.z_score}',");
                        isb.Append($"'{attractor.probability_single}',");
                        isb.Append($"'{attractor.integral_score}',");
                        isb.Append($"'{attractor.significance}',");
                        isb.Append($"'{attractor.probability}'");
                        isb.Append(")");
                        var insertSql = isb.ToString();
                        Console.WriteLine("SQL:" + insertSql.ToString());

                        using (SqlCommand command = new SqlCommand(insertSql, connection))
                        {
                            // TODO: another way to execute the command? As it's only insert and don't need to read the results here
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                string commandResult = "";
                                while (reader.Read())
                                {
                                    //commandResult += $"{reader.GetString(0)} {reader.GetString(1)}\n";
                                }
                                //context.SendActivityAsync(commandResult);
                            }
                        }
                    }
                }
            });
        }

        private string SanitizeString(string input)
        {
            if (input == null)
                return "";

            return input.Replace("'", "''");
        }

        private string[] SanitizeString(string [] input)
        {
            if (input == null)
                return null;

            string[] result = new string[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = input[i].Replace("'", "''");
            }
            return result;
        }
    }
}
