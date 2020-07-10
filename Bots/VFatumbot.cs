using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using AmplitudeService;
using VFatumbot.BotLogic;
using static VFatumbot.BotLogic.Enums;
using Newtonsoft.Json.Linq;

namespace VFatumbot
{
    public class VFatumbot<T> : ActivityHandler where T : Dialog
    {
        protected readonly MainDialog _mainDialog;
        protected readonly ILogger _logger;
        protected readonly ConversationState _conversationState;
        protected readonly UserPersistentState _userPersistentState;
        protected readonly UserTemporaryState _userTemporaryState;

        protected IStatePropertyAccessor<ConversationData> _conversationDataAccessor;
        protected IStatePropertyAccessor<UserProfilePersistent> _userProfilePersistentAccessor;
        protected IStatePropertyAccessor<UserProfileTemporary> _userProfileTemporaryAccessor;

        public VFatumbot(ConversationState conversationState, UserPersistentState userPersistentState, UserTemporaryState userTemporaryState, MainDialog dialog, ILogger<VFatumbot<MainDialog>> logger)
        {
            _conversationState = conversationState;
            _userPersistentState = userPersistentState;
            _userTemporaryState = userTemporaryState;
            _mainDialog = dialog;
            _logger = logger;
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    var userProfilePersistent = await _userProfilePersistentAccessor.GetAsync(turnContext, () => new UserProfilePersistent());
                    var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(turnContext, () => new UserProfileTemporary());

                    var isNonApp = userProfileTemporary.BotSrc != WebSrc.android && userProfileTemporary.BotSrc != WebSrc.ios;
                    string startLocale = "";

                    if (Helpers.IsRandoLobby(turnContext))
                    {
                        // If Randonauts Telegram lobby then keep the welcome short
                        Activity replyActivity;
                        if (userProfileTemporary.IsLocationSet || userProfilePersistent.HasSetLocationOnce)
                        {
                            replyActivity = MessageFactory.Text($"Welcome back @{turnContext.Activity.From.Name}!");
                        }
                        else
                        {
                            string name = "to the Randonauts Lobby";
                            if (!"randonauts".Equals(turnContext.Activity.From.Name))
                            {
                                // Make the welcome a bit more personal if they've set their @username
                                name = $"@{turnContext.Activity.From.Name}";
                            }
                            replyActivity = MessageFactory.Text($"Welcome {name}! Message the @shangrila_bot privately to start your adventure and feel free to share your experiences here.");
                        }

                        await turnContext.SendActivityAsync(replyActivity);
                    }
                    else if (userProfileTemporary.IsLocationSet)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("welcome_back")), cancellationToken);
                        if (isNonApp)
                        {
                            await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
                        }
                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("dont_forgot_send_loc")), cancellationToken);
                        await _mainDialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                    }
                    else if (userProfilePersistent.HasSetLocationOnce)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("welcome_back")), cancellationToken);
                        if (isNonApp)
                        {
                            await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
                        }
                        await turnContext.SendActivityAsync(MessageFactory.Text(Consts.NO_LOCATION_SET_MSG), cancellationToken);
                    } else if (InterceptConversationStartWithLocale(turnContext, out startLocale)) {
                        userProfilePersistent.SetLocale(startLocale);
                        await DoWelcomeAsync(turnContext, userProfilePersistent.Locale, cancellationToken);
                    }

                    // Hack coz Facebook Messenge stopped showing "Send Location" button
                    if (turnContext.Activity.ChannelId.Equals("facebook"))
                    {
                        await turnContext.SendActivityAsync(CardFactory.CreateGetLocationFromGoogleMapsReply());
                    }
                }
            }
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            _userProfilePersistentAccessor = _userPersistentState.CreateProperty<UserProfilePersistent>(nameof(UserProfilePersistent));
            var userProfilePersistent = await _userProfilePersistentAccessor.GetAsync(turnContext, () => new UserProfilePersistent());

            _userProfileTemporaryAccessor = _userTemporaryState.CreateProperty<UserProfileTemporary>(nameof(UserProfileTemporary));
            var userProfileTemporary = await _userProfileTemporaryAccessor.GetAsync(turnContext, () => new UserProfileTemporary());

            _conversationDataAccessor = _conversationState.CreateProperty<ConversationData>(nameof(ConversationData));
            var conversationData = await _conversationDataAccessor.GetAsync(turnContext, () => new ConversationData());

            // Print info about image attachments
            //if (turnContext.Activity.Attachments != null)
            //{
            //    await turnContext.SendActivityAsync(JsonConvert.SerializeObject(turnContext.Activity.Attachments), cancellationToken: cancellationToken);
            //}

            // Save user's ID
            userProfilePersistent.UserId = userProfileTemporary.UserId = Helpers.Sha256Hash(turnContext.Activity.From.Id);

            // Add message details to the conversation data.
            var messageTimeOffset = (DateTimeOffset)turnContext.Activity.Timestamp;
            var localMessageTime = messageTimeOffset.ToLocalTime();
            conversationData.Timestamp = localMessageTime.ToString();
            await _conversationDataAccessor.SetAsync(turnContext, conversationData);

            // TODO: most of the logic/functionality in the following if statements I realised later on should probably be structured in the way the Bot Framework SDK talks about "middleware".
            // Maybe one day re-structure/re-factor it to following their middleware patterns...
            // TODO update: Nup, that won't be happening. The bot will be walking the plank soon in favour of the Flutter app.

            var botSrc = WebSrc.nonweb;

            if (InterceptWebBotSource(turnContext, out botSrc))
            {
                userProfileTemporary.BotSrc = botSrc;
                await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
            }

            // Amplitude event tracking initializing
            Amplitude.Initialize(Consts.AMPLITUDE_HTTP_API_KEY);
            var platform = "";
            if (userProfileTemporary.BotSrc == WebSrc.ios)
            {
                platform = "iOS";
            }
            else if (userProfileTemporary.BotSrc == WebSrc.android)
            {
                platform = "Android";
            }
            else
            {
                platform = "Web";
            }
            var userProperties = new Dictionary<string, object>()
            {
                {"Locale", userProfilePersistent.Locale?.ToString()},
                {"BotSrc", userProfileTemporary.BotSrc.ToString() },
                {"HasSkipWaterPoints", userProfilePersistent.HasSkipWaterPoints },
                {"HasLocationSearch", userProfilePersistent.HasLocationSearch },
                {"HasMapsPack", userProfilePersistent.HasMapsPack },
                {"IsIncludeWaterPoints", userProfileTemporary.IsIncludeWaterPoints },
                {"IsDisplayGoogleThumbnails", userProfileTemporary.IsDisplayGoogleThumbnails },
                {"IsUseClassicMode", userProfileTemporary.IsUseClassicMode },
                {"Platform", platform},
            };
            if (!string.IsNullOrEmpty(userProfileTemporary.Country))
            {
                userProperties.Add("Country", userProfileTemporary.Country);
            }
            userProfileTemporary.UserProperties = userProperties;

            string startLocale;
            if (InterceptConversationStartWithLocale(turnContext, out startLocale))
            {
                userProfilePersistent.SetLocale(startLocale);
                await DoWelcomeAsync(turnContext, userProfilePersistent.Locale, cancellationToken);

                await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("dl_x_remaining", userProfilePersistent.OwlTokens, Consts.DAILY_MAX_FREE_OWL_TOKENS_REFILL)), cancellationToken);

                // Piggy back of the startup event here for Amplitude
                userProfileTemporary.StartSessionTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                Amplitude.InstanceFor(userProfilePersistent.UserId, userProfileTemporary.UserProperties).Track("Start");
            }
            else
            {
                Thread.CurrentThread.CurrentUICulture = userProfilePersistent.Locale;
            }

            double lat = 0, lon = 0;
            string pushUserId = null;
            userProfileTemporary.PushUserId = userProfilePersistent.PushUserId;

            if (InterceptPushNotificationSubscription(turnContext, out pushUserId))
            {
                if (userProfilePersistent.PushUserId != pushUserId)
                {
                    userProfilePersistent.PushUserId = userProfileTemporary.PushUserId = pushUserId;
                    await _userProfilePersistentAccessor.SetAsync(turnContext, userProfilePersistent);
                    await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
                }
            }
            else if (await InterceptInappPurchaseAsync(turnContext, userProfilePersistent, cancellationToken))
            {
                await ((AdapterWithErrorHandler)turnContext.Adapter).RepromptMainDialog(turnContext, _mainDialog, cancellationToken);
            }
            else if (Helpers.InterceptLocation(turnContext, out lat, out lon)) // Intercept any locations the user sends us, no matter where in the conversation they are
            {
                bool validCoords = true;

                if (lat == Consts.INVALID_COORD && lon == Consts.INVALID_COORD && userProfileTemporary.HasLocationSearch)
                {
                    // Do a geocode query lookup against the address the user sent
                    var result = await Helpers.GeocodeAddressAsync(turnContext.Activity.Text.ToLower().Replace("search", ""));
                    if (result != null)
                    {
                        lat = result.Item1;
                        lon = result.Item2;
                        AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Search Location");
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Place not found."), cancellationToken);
                        validCoords = false;
                    }
                }
                else if (lat == Consts.INVALID_COORD && lon == Consts.INVALID_COORD && !userProfileTemporary.HasLocationSearch)
                {
                    validCoords = false;
                }

                if (validCoords)
                {
                    // Update user's location
                    userProfileTemporary.Latitude = lat;
                    userProfileTemporary.Longitude = lon;

                    await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("current_location", lat.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture), lon.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture))), cancellationToken);

                    var incoords = new double[] { lat, lon };
                    var w3wResult = await Helpers.GetWhat3WordsAddressAsync(incoords);
                    await turnContext.SendActivitiesAsync(CardFactory.CreateLocationCardsReply(Enum.Parse<ChannelPlatform>(turnContext.Activity.ChannelId), incoords, userProfileTemporary.IsDisplayGoogleThumbnails, w3wResult: w3wResult, paying: userProfileTemporary.HasMapsPack), cancellationToken);

                    userProfileTemporary.Country = Helpers.GetCountryFromW3W(w3wResult);
                    if (!string.IsNullOrEmpty(userProfileTemporary.Country))
                    {
                        userProfileTemporary.Country = userProfileTemporary.Country.Replace("(", "").Replace(")", "");
                    }
                    AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Location Sent");

                    await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
                    await _userTemporaryState.SaveChangesAsync(turnContext, false, cancellationToken);

                    userProfilePersistent.HasSetLocationOnce = true;
                    await _userProfilePersistentAccessor.SetAsync(turnContext, userProfilePersistent);
                    await _userPersistentState.SaveChangesAsync(turnContext, false, cancellationToken);

                    await ((AdapterWithErrorHandler)turnContext.Adapter).RepromptMainDialog(turnContext, _mainDialog, cancellationToken);

                    return;
                }
            }
            else if (!string.IsNullOrEmpty(turnContext.Activity.Text) &&
                     turnContext.Activity.Text.EndsWith(Loc.g("help"), StringComparison.InvariantCultureIgnoreCase) &&
                     !turnContext.Activity.Text.Contains(Loc.g("md_options"), StringComparison.InvariantCultureIgnoreCase)) // Menu was changed to "Options/Help" so avoid be caught here
            {
                AmplitudeService.Amplitude.InstanceFor(userProfileTemporary.UserId, userProfileTemporary.UserProperties).Track("Help");
                await Helpers.HelpAsync(turnContext, userProfileTemporary, _mainDialog, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(turnContext.Activity.Text) && (
                        turnContext.Activity.Text.ToLower().StartsWith("/steve", StringComparison.InvariantCultureIgnoreCase) ||
                        turnContext.Activity.Text.ToLower().StartsWith("/newsteve22", StringComparison.InvariantCultureIgnoreCase) ||
                        turnContext.Activity.Text.ToLower().StartsWith("/ongshat", StringComparison.InvariantCultureIgnoreCase)
                ))
            {
                await new ActionHandler().ParseSlashCommands(turnContext, userProfileTemporary, cancellationToken, _mainDialog);

                await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
                await _userPersistentState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _userTemporaryState.SaveChangesAsync(turnContext, false, cancellationToken);

                return;
            }
            else if (!string.IsNullOrEmpty(turnContext.Activity.Text) && !userProfileTemporary.IsLocationSet)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(Consts.NO_LOCATION_SET_MSG), cancellationToken);

                // Hack coz Facebook Messenge stopped showing "Send Location" button
                if (turnContext.Activity.ChannelId.Equals("facebook"))
                {
                    await turnContext.SendActivityAsync(CardFactory.CreateGetLocationFromGoogleMapsReply());
                }

                return;
            }
            else if (!string.IsNullOrEmpty(turnContext.Activity.Text) && turnContext.Activity.Text.StartsWith("/", StringComparison.InvariantCulture))
            {
                if (userProfilePersistent.IsNoOwlTokens)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("dl_no_tokens")));
                    return;
                }

                await new ActionHandler().ParseSlashCommands(turnContext, userProfileTemporary, cancellationToken, _mainDialog);

                await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
                await _userPersistentState.SaveChangesAsync(turnContext, false, cancellationToken);
                await _userTemporaryState.SaveChangesAsync(turnContext, false, cancellationToken);

                return;
            }
            else if (Helpers.IsRandoLobby(turnContext))
            {
                // Prevent dialog convos in the lobby
                return;
            }

            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userPersistentState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userTemporaryState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private async Task DoWelcomeAsync(ITurnContext turnContext, CultureInfo locale, CancellationToken cancellationToken)
        {
            var welcome = $"#### {Loc.g("welcome_randonautica")}\n" +
                $"{Loc.g("welcome_beginners", "https://www.randonautica.com/got-questions", "https://i.redd.it/x97vcpvtd9p41.jpg")}  \n\n\n" +
                $"{Loc.g("welcome_report_share", "https://www.reddit.com/r/randonauts/", "https://twitter.com/RandonauticaApp")}  \n\n\n" +
                "##### Pro Tips  \n" +
                "   \n\n\n" +
                "* Randonaut with a positive mindset!  \n" +
                "* Bring a trash bag to help the environment.  \n" +
                "* If you normally wouldn't adventure alone, go with a friend or group.  \n" +
                "* Randonauting is best done as a daytime activity.  \n" +
                "* Always Randonaut with a charged phone.  \n" +
                "* Use situational awareness.  \n" +
                "* Be respectful of property owners. Never trespass.  \n" +
                "* Obey all quarantine, curfew and social distancing regulations in your area.  \n" +
                "* Do not go into dangerous areas.  \n\n\n" +
                "Happy Randonauting!"
               ;
            await turnContext.SendActivityAsync(MessageFactory.Text(welcome), cancellationToken);
            //await turnContext.SendActivityAsync(CardFactory.GetWelcomeVideoCard());
            //if (isNonApp) // disable for now coz it was clogging the welcome screen and we lost the ability to detect isNonApp properly
            //{
            //    await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
            //}
            //await turnContext.SendActivityAsync(MessageFactory.Text("Start by sending your location by tapping 🌍/📎 or typing 'search' followed by a place name/address."), cancellationToken);

            await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("first_send_location")), cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("Running dialog with Message Activity.");

            // Run the MainDialog with the new message Activity
            await _mainDialog.Run(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }

        protected async Task<bool> InterceptInappPurchaseAsync(ITurnContext turnContext, UserProfilePersistent userProfilePersistent, CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity;
            var text = "";
            if (!string.IsNullOrEmpty(activity.Text)) text = activity.Text;

            if (activity.Properties != null
#if !RELEASE_PROD
                || text.StartsWith("x")
#endif
                )
            {
                var iapDataStr = (string)activity.Properties.GetValue("iapData");
                if (!string.IsNullOrEmpty(iapDataStr)
#if !RELEASE_PROD
                    || text.StartsWith("x")
#endif
                    )
                {
                    // Do server verification with Apple on the receipt before enabling paid features
                    Purchases iapData = new Purchases();
                    if (iapDataStr != null)
                    {
                        iapData = JsonConvert.DeserializeObject<Purchases>(iapDataStr);
                    }
#if !RELEASE_PROD
                  if (!text.StartsWith("x"))
                  {
#endif
                    var verify = await Helpers.VerifyAppleIAPReceptAsync(iapData.serverVerificationData);
                    if (verify == 21007)
                    {
                        // Retry on the sandbox environment
                        verify = await Helpers.VerifyAppleIAPReceptAsync(iapData.serverVerificationData, true);
                    }
                    if (verify != 0)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_invalid_receipt", verify)), cancellationToken);
                        return true;
                    }
#if !RELEASE_PROD
                    }
#endif

                    if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.c.add_20_points"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("x20")
#endif
                        )
                    {
                        userProfilePersistent.OwlTokens += 20;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_added_x_points", 20, userProfilePersistent.OwlTokens)), cancellationToken);
                    }
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.c.add_60_points"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("x60")
#endif
                        )
                    {
                        userProfilePersistent.OwlTokens += 60;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_added_x_points", 60, userProfilePersistent.OwlTokens)), cancellationToken);
                    }
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.infinite_points"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("xinfp")
#endif
                        )
                    {
                        userProfilePersistent.HasInfinitePoints = true;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_infinite_points_enabled")), cancellationToken);
                    }
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.maps_pack"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("xmaps")
#endif
                        )
                    {
                        userProfilePersistent.HasMapsPack = true;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_maps_pack_enabled")), cancellationToken);
                    }
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.skip_water_pack"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("xwater")
#endif
                        )
                    {
                        userProfilePersistent.HasLocationSearch = true;
                        userProfilePersistent.HasSkipWaterPoints = true;
                        userProfilePersistent.IsIncludeWaterPoints = false;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_search_water_points_enabled")), cancellationToken);
                    }
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.extend_radius_20km"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("xrad")
#endif
                        )
                    {
                        userProfilePersistent.Has20kmRadius = true;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_20km_radius_extended")), cancellationToken);
                    }
                    // everything
                    else if ((iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.maps_skip_water_packs")) // old name for original everything pack
                          || (iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.everything_pack"))
#if !RELEASE_PROD
                        || turnContext.Activity.Text.StartsWith("xall")
#endif
                        )
                    {
                        userProfilePersistent.HasMapsPack = true;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;

                        userProfilePersistent.HasLocationSearch = true;
                        userProfilePersistent.HasSkipWaterPoints = true;
                        userProfilePersistent.IsIncludeWaterPoints = false;

                        userProfilePersistent.HasInfinitePoints = true;
                        userProfilePersistent.Has20kmRadius = true;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_everything_enabled")), cancellationToken);
                    }
                    else
                    {
                        userProfilePersistent.HasMapsPack = userProfilePersistent.HasLocationSearch = userProfilePersistent.HasSkipWaterPoints = false;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;
                        userProfilePersistent.IsIncludeWaterPoints = true;

                        userProfilePersistent.HasInfinitePoints = false;
                        userProfilePersistent.Has20kmRadius = false;

                        await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_all_disabled")), cancellationToken);
                    }

                    if (iapData.productID != null)
                    {
                        if (userProfilePersistent.Purchases == null)
                        {
                            userProfilePersistent.Purchases = new Dictionary<string, Purchases>();
                            userProfilePersistent.Purchases.Add(iapData.productID, iapData);
                        }
                        else
                        {
                            userProfilePersistent.Purchases[iapData.productID] = iapData;
                        }
                    }

                    return true;
                }
                else if (activity != null && activity.Text != null)
                {
                    if (activity.Text.StartsWith("/unseenlings"))
                    {
                        userProfilePersistent.HasMapsPack = true;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;

                        userProfilePersistent.HasLocationSearch = true;
                        userProfilePersistent.HasSkipWaterPoints = true;
                        userProfilePersistent.IsIncludeWaterPoints = false;

                        userProfilePersistent.HasInfinitePoints = true;
                        userProfilePersistent.Has20kmRadius = true;

                        userProfilePersistent.OwlTokens += 5;

                        await turnContext.SendActivityAsync(MessageFactory.Text("Steve.Steve.Steve!"), cancellationToken);

                        return true;
                    }
                    else if (activity.Text.StartsWith("/mypurchases"))
                    {
                        if (userProfilePersistent.Purchases == null || userProfilePersistent.Purchases.Count == 0)
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text(Loc.g("iap_no_purchases")), cancellationToken);
                        }
                        else
                        {
                            await turnContext.SendActivityAsync(MessageFactory.Text(JsonConvert.SerializeObject(userProfilePersistent.Purchases)), cancellationToken);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        protected bool InterceptConversationStartWithLocale(ITurnContext turnContext, out string startLocale)
        {
            startLocale = null;

            var activity = turnContext.Activity;

            if (activity.Properties != null)
            {
                var localeFromClient = (string)activity.Properties.GetValue("startLocale");
                if (!string.IsNullOrEmpty(localeFromClient))
                {
                    startLocale = localeFromClient;
                    return true;
                }
            }

            try
            {
                // try and get users locale from bot app, like Telegram (and maybe others)
                JObject channelData = JObject.Parse(activity.ChannelData.ToString());
                JToken locale = channelData["message"]["from"]["language_code"];
                startLocale = locale.ToString().ToLower();
            }
            catch (Exception e)
            {
                // do nothing
            }

            return false;
        }

        protected bool InterceptPushNotificationSubscription(ITurnContext turnContext, out string pushUserId)
        {
            pushUserId = null;

            var activity = turnContext.Activity;

            if (activity.Properties != null)
            {
                var pushUserIdFromClient = (string)activity.Properties.GetValue("pushUserId");
                if (!string.IsNullOrEmpty(pushUserIdFromClient))
                {
                    pushUserId = pushUserIdFromClient;
                    return true;
                }
            }

            return false;
        }

        protected bool InterceptWebBotSource(ITurnContext turnContext, out WebSrc webSrc)
        {
            webSrc = WebSrc.nonweb;

            var activity = turnContext.Activity;

            if (activity.Properties != null)
            {
                var clientSrc = (string)activity.Properties.GetValue("src");
                if (!string.IsNullOrEmpty(clientSrc))
                {
                    var res = Enum.TryParse<WebSrc>(clientSrc, out webSrc);
                    return res;
                }
            }

            return false;
        }
    }
}
