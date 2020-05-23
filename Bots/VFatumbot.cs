﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VFatumbot.BotLogic;
using static VFatumbot.BotLogic.Enums;

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
                        await turnContext.SendActivityAsync(MessageFactory.Text("Welcome back to Randonautica!"), cancellationToken);
                        if (isNonApp)
                        {
                            await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
                        }
                        await turnContext.SendActivityAsync(MessageFactory.Text("Don't forget to send your current location."), cancellationToken);
                        await _mainDialog.RunAsync(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
                    }
                    else if (userProfilePersistent.HasSetLocationOnce)
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Welcome back to Randonautica!"), cancellationToken);
                        if (isNonApp)
                        {
                            await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
                        }
                        await turnContext.SendActivityAsync(MessageFactory.Text(Consts.NO_LOCATION_SET_MSG), cancellationToken);
                    }
                    else
                    {
                        var welcome = "#### Welcome to Randonautica!\n" +
                            "Beginners: Watch this [How-to Video](https://youtube.com/watch?v=xEbbsG2U26k) before your first trip.  \n\n\n" +
                            "Once you've completed a trip, share in the discussion with the Randonauts on [Reddit](https://www.reddit.com/r/randonauts/) and [Twitter](https://twitter.com/TheRandonauts).  \n\n\n" +
                            "Happy Randonauting!";
                        await turnContext.SendActivityAsync(MessageFactory.Text(welcome), cancellationToken);
                        //await turnContext.SendActivityAsync(CardFactory.GetWelcomeVideoCard());
                        //if (isNonApp) // disable for now coz it was clogging the welcome screen and we lost the ability to detect isNonApp properly
                        //{
                        //    await turnContext.SendActivityAsync(CardFactory.CreateAppStoreDownloadCard());
                        //}
                        //await turnContext.SendActivityAsync(MessageFactory.Text("Start by sending your location by tapping 🌍/📎 or typing 'search' followed by a place name/address."), cancellationToken);
                        await turnContext.SendActivityAsync(MessageFactory.Text("Start by sending your location by tapping 🌍/📎 or sending a Google Maps URL."), cancellationToken);
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

            var botSrc = WebSrc.nonweb;

            if (InterceptWebBotSource(turnContext, out botSrc))
            {
                userProfileTemporary.BotSrc = botSrc;
                await _userProfileTemporaryAccessor.SetAsync(turnContext, userProfileTemporary);
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
            else if (InterceptInappPurchase(turnContext, userProfilePersistent))
            {
                await ((AdapterWithErrorHandler)turnContext.Adapter).RepromptMainDialog(turnContext, _mainDialog, cancellationToken);
                return;
            }
            else if (Helpers.InterceptLocation(turnContext, out lat, out lon)) // Intercept any locations the user sends us, no matter where in the conversation they are
            {
                bool validCoords = true;
#if !RELEASE_PROD
                if (lat == Consts.INVALID_COORD && lon == Consts.INVALID_COORD)
                {
                    // Do a geocode query lookup against the address the user sent
                    var result = await Helpers.GeocodeAddressAsync(turnContext.Activity.Text.ToLower().Replace("search", ""));
                    if (result != null)
                    {
                        lat = result.Item1;
                        lon = result.Item2;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(MessageFactory.Text("Place not found."), cancellationToken);
                        validCoords = false;
                    }
                }
#endif

                if (validCoords)
                {
                    // Update user's location
                    userProfileTemporary.Latitude = lat;
                    userProfileTemporary.Longitude = lon;

                    await turnContext.SendActivityAsync(MessageFactory.Text($"Your current location is set to {lat.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString("#0.000000", System.Globalization.CultureInfo.InvariantCulture)}.  \nThis will be the center for searches."), cancellationToken);

                    var incoords = new double[] { lat, lon };
                    var w3wResult = await Helpers.GetWhat3WordsAddressAsync(incoords);
                    await turnContext.SendActivitiesAsync(CardFactory.CreateLocationCardsReply(Enum.Parse<ChannelPlatform>(turnContext.Activity.ChannelId), incoords, userProfileTemporary.IsDisplayGoogleThumbnails, w3wResult), cancellationToken);

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
                     turnContext.Activity.Text.EndsWith("help", StringComparison.InvariantCultureIgnoreCase) &&
                     !turnContext.Activity.Text.Contains("options", StringComparison.InvariantCultureIgnoreCase)) // Menu was changed to "Options/Help" so avoid be caught here
            {
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

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            //_logger.LogInformation("Running dialog with Message Activity.");

            // Run the MainDialog with the new message Activity
            await _mainDialog.Run(turnContext, _conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }

        protected bool InterceptInappPurchase(ITurnContext turnContext, UserProfilePersistent userProfilePersistent)
        {
            var activity = turnContext.Activity;

            if (activity.Properties != null)
            {
                var iapDataStr = (string)activity.Properties.GetValue("iapData");
                if (!string.IsNullOrEmpty(iapDataStr))
                {
                    dynamic iapData = JsonConvert.DeserializeObject<dynamic>(iapDataStr);

                    if (iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.maps_pack"))
                    {
                        userProfilePersistent.HasMapsPack = true;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;
                        turnContext.SendActivityAsync(MessageFactory.Text("Maps Pack add-on enabled."));
                    }
                    else if (iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.skip_water_pack"))
                    {
                        userProfilePersistent.HasLocationSearch = true;
                        userProfilePersistent.HasSkipWaterPoints = true;
                        userProfilePersistent.IsIncludeWaterPoints = false;
                        turnContext.SendActivityAsync(MessageFactory.Text("Place Search and Skip Water Points Pack add-on enabled."));
                    }
                    else if (iapData.productID != null && iapData.productID.ToString().StartsWith("fatumbot.addons.nc.maps_skip_water_packs"))
                    {
                        userProfilePersistent.HasMapsPack = true;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;

                        userProfilePersistent.HasLocationSearch = true;
                        userProfilePersistent.HasSkipWaterPoints = true;
                        userProfilePersistent.IsIncludeWaterPoints = false;
                        turnContext.SendActivityAsync(MessageFactory.Text("The Everything Pack add-on enabled."));
                    }
                    else
                    {
                        userProfilePersistent.HasMapsPack = userProfilePersistent.HasLocationSearch = userProfilePersistent.HasSkipWaterPoints = false;
                        userProfilePersistent.IsDisplayGoogleThumbnails = false;
                        userProfilePersistent.IsIncludeWaterPoints = true;
                        turnContext.SendActivityAsync(MessageFactory.Text("All add-ons disabled."));
                    }

                    return true;
                }
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
