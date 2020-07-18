using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

// credit: https://github.com/DenisNP/Amplitude/blob/master/Amplitude/Amplitude.cs
namespace AmplitudeService
{
    public class Amplitude
    {
        private static string _apiKey;
        private const string ApiAddress = "https://api.amplitude.com/2/httpapi";
        private static readonly HttpClient Client = new HttpClient();
        private static readonly ConcurrentDictionary<string, Amplitude> Instances = new ConcurrentDictionary<string, Amplitude>();

        private readonly string _userId;
        private readonly Dictionary<string, object> _userProperties;
        private long _sessionStartTime = -1;

        /// <summary>
        /// Initialize entire service with api key
        /// </summary>
        /// <param name="apiKey">Your API_KEY from amplitude console</param>
        public static void Initialize(string apiKey)
        {
            _apiKey = apiKey;
        }

        /// <summary>
        /// Get or create instance for specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="storeInstance">If enabled, InstanceFor will return same instance for same user</param>
        /// <param name="userProperties">Properties that should be added to user</param>
        /// <returns>Amplitude service instance</returns>
        public static Amplitude InstanceFor(string userId, bool storeInstance = false, Dictionary<string, object> userProperties = null)
        {
            if (storeInstance)
            {
                return Instances.GetOrAdd(userId, new Amplitude(userId, userProperties));
            }

            if (Instances.ContainsKey(userId))
            {
                throw new Exception("Dont mix stored instances with non-stored");
            }

            return new Amplitude(userId, userProperties);
        }

        /// <summary>
        /// Get or create instance for specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        /// <param name="userProperties">Properties that should be added to user</param>
        /// <returns>Amplitude service instance</returns>
        public static Amplitude InstanceFor(string userId, Dictionary<string, object> userProperties)
        {
            return InstanceFor(userId, false, userProperties);
        }

        /// <summary>
        /// Dispose instance for specific user
        /// </summary>
        /// <param name="userId">User identifier</param>
        public static void DisposeFor(string userId)
        {
            Instances.TryRemove(userId, out _);
        }

        private Amplitude(string userId, Dictionary<string, object> userProperties = null)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new Exception("You should call Amplitude.Initialize(...) with your API_KEY first");
            }

            _userId = userId;
            _userProperties = userProperties;
        }

        /// <summary>
        /// Start new session to track
        /// </summary>
        /// <param name="startTime">Session start time, defaults to now</param>
        /// <returns>Amplitude service instance for chaining, e.g. `var amp = Amplitude.InstanceFor(...).StartSession()`</returns>
        public Amplitude StartSession(DateTimeOffset startTime = default)
        {
            _sessionStartTime = startTime == default
                ? DateTimeOffset.Now.ToUnixTimeMilliseconds()
                : startTime.ToUnixTimeMilliseconds();

            return this;
        }

        /// <summary>
        /// Track event
        /// </summary>
        /// <param name="eventName">String name of the event</param>
        /// <param name="properties">Additional event data, this will be added to persistent properties</param>
        /// <returns>This instance for chaining</returns>
        public Amplitude Track(string eventName, Dictionary<string, object> properties = null)
        {
            SendEvent(_userId, eventName, properties, _userProperties, _sessionStartTime);
            return this;
        }

        /// <summary>
        /// Track event with simplified method call
        /// </summary>
        /// <param name="eventName">Event name</param>
        /// <param name="parameters">Array of key-value pairs sequentially</param>
        /// <returns>This instance for chaining</returns>
        /// <exception cref="ArgumentException"></exception>
        public Amplitude Track(string eventName, params object[] parameters)
        {
            if (parameters.Length % 2 != 0)
            {
                throw new ArgumentException(
                    "Parameters array should represent key-value pairs and have even length"
                );
            }
            var dict = new Dictionary<string, object>();
            for (var i = 0; i < parameters.Length; i += 2)
            {
                var key = parameters[i];
                var value = parameters[i + 1];
                if (!(key is string))
                {
                    throw new ArgumentException(
                        "Parameters array should represent key-value pairs, all keys must be strings"
                    );

                }

                dict.Add(key as string, value);
            }

            return Track(eventName, dict);
        }

        private static void SendEvent(
            string userId,
            string eventName,
            Dictionary<string, object> properties,
            Dictionary<string, object> userProperties,
            long sessionStartTime = -1
        )
        {
            try
            {
                var eventData = new Dictionary<string, object>
                {
                    {"user_id", userId},
                    {"insert_id", Guid.NewGuid()},
                    {"event_type", eventName},
                    {"time", DateTimeOffset.Now.ToUnixTimeMilliseconds()}
                };

                if (properties != null)
                {
                    eventData.Add("event_properties", properties);
                }

                if (userProperties != null)
                {
                    eventData.Add("user_properties", userProperties);

                    // modded by soliax
                    if (userProperties.ContainsKey("Country"))
                    {
                        eventData.Add("country", userProperties["Country"]);
                    }
                    if (userProperties.ContainsKey("Platform"))
                    {
                        eventData.Add("platform", userProperties["Platform"]);
                    }
                    eventData.Add("app_version", VFatumbot.BotLogic.Consts.APP_VERSION);
                    eventData.Add("language", Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName);
                }

                if (sessionStartTime > 0)
                {
                    eventData.Add("session_id", sessionStartTime);
                }

                var parameters = new Dictionary<string, object>
                {
                    {"api_key", _apiKey},
                    {"events", new List<Dictionary<string, object>>{eventData}}
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(parameters),
                    Encoding.UTF8,
                    "application/json"
                );
                Client.PostAsync(ApiAddress, content);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}