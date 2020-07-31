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
    public class PrivacyAndTermsDialog : ComponentDialog
    {
        protected readonly ILogger _logger;
        protected readonly IStatePropertyAccessor<UserProfilePersistent> _userProfilePersistentAccessor;

        public PrivacyAndTermsDialog(IStatePropertyAccessor<UserProfilePersistent> userProfilePersistenAccessor, ILogger<MainDialog> logger) : base(nameof(PrivacyAndTermsDialog))
        {
            _logger = logger;
            _userProfilePersistentAccessor = userProfilePersistenAccessor;

            AddDialog(new ChoicePrompt(nameof(ChoicePrompt))
            {
            });
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                MustAgreeStepAsync,
                AgreeYesOrNoStepAsync,
            })
            {
            });

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> MustAgreeStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("PrivacyAndTermsDialog.MustAgreeStepAsync");

            var help2 = System.IO.File.ReadAllText(Loc.getTermsFilename()).Replace("APP_VERSION", Consts.APP_VERSION);
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(help2), cancellationToken);

            return await stepContext.PromptAsync(nameof(ChoicePrompt), GetPromptOptions(Loc.g("agree_to_terms_q")), cancellationToken);
        }

        private async Task<DialogTurnResult> AgreeYesOrNoStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation($"PrivacyAndTermsDialog.AgreeYesOrNoStepAsync[{((FoundChoice)stepContext.Result)?.Value}]");

            var userProfilePersistent = await _userProfilePersistentAccessor.GetAsync(stepContext.Context);

            var val = ((FoundChoice)stepContext.Result)?.Value;
            if (val.Equals(Loc.g("toc_doagree")))
            {
                userProfilePersistent.HasAgreedToToS = true;
                //AmplitudeService.Amplitude.InstanceFor(userProfilePersistent.UserId).Track("TOS Agree");
                await _userProfilePersistentAccessor.SetAsync(stepContext.Context, userProfilePersistent);
                return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken: cancellationToken);
            }

            //AmplitudeService.Amplitude.InstanceFor(userProfilePersistent.UserId).Track("TOS Don't Agree");
            await stepContext.Context.SendActivityAsync(MessageFactory.Text(Loc.g("toc_shame")), cancellationToken);
            return await stepContext.ReplaceDialogAsync(nameof(MainDialog), cancellationToken:cancellationToken);
        }

        private PromptOptions GetPromptOptions(string prompt)
        {
            return new PromptOptions()
            {
                Prompt = MessageFactory.Text(prompt),
                RetryPrompt = MessageFactory.Text(Loc.g("toc_invalid_answer", prompt)),
                Choices = new List<Choice>()
                                {
                                    new Choice() {
                                        Value = Loc.g("toc_dontagree"),
                                        Synonyms = new List<string>()
                                                        {
                                                            "no",
                                                        }
                                    },
                                    new Choice() {
                                        Value = Loc.g("toc_doagree"),
                                        Synonyms = new List<string>()
                                                        {
                                                            "i agree",
                                                            "Agree",
                                                            "agree",
                                                            "Yes",
                                                            "yes"
                                                        }
                                    },
                                }
            };
        }
    }
}
