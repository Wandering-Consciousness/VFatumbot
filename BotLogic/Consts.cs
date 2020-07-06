namespace VFatumbot.BotLogic
{
    public class Consts
    {
        private const string _appVersion = "4.8.4";
#if RELEASE_PROD
        public const string APP_VERSION = _appVersion;
#else
        public const string APP_VERSION = _appVersion + "-dev";
#endif

        // Azure App ID
#if RELEASE_PROD
        public const string APP_ID = "a7c83890-eec2-44cb-b953-0205cbfa9029";
#else
        public const string APP_ID = "a4784186-b7ab-4723-b76e-21188f986f96";
#endif

        // Azure Cosmos DB credentials
        public const string COSMOS_DB_NAME = "fatumbot";
        public const string COSMOS_DB_URI = "https://ratumbott.documents.azure.com:443/";
        public const string COSMOS_DB_KEY = "rLLUR12cJq1SCxeeQ6Zs6TRrXCM3yMaSifodWDgSYPfMtYM9os9JGgym0N8FA2nTf3s0c9k6KbeMisHndmKv5w==";
#if RELEASE_PROD
        public const string COSMOS_CONTAINER_NAME_PERSISTENT = "prod_persistent";
        public const string COSMOS_CONTAINER_NAME_TEMPORARY = "prod_temporary";
#else
        public const string COSMOS_CONTAINER_NAME_PERSISTENT = "dev_persistent";
        public const string COSMOS_CONTAINER_NAME_TEMPORARY = "dev_temporary";
#endif

        // Google Maps API key
        public const string GOOGLE_MAPS_API_KEY = "AIzaSyBNNMGaGj9FRBfIInN8CGbCJYyw9_OsexI";
        public const string GOOGLE_GEOCODE_API_KEY = "AIzaSyDZGQB0L0HXoZZAEu3FJzKZa2M3m2YIfNA";

        // OneSignal for Push Notifications
#if RELEASE_PROD
        public const string ONE_SIGNAL_APP_ID = "da21a078-babf-4e22-a032-0ea22de561a7";
        public const string ONE_SIGNAL_API_KEY = "NGIyYTZjZmEtM2QxZS00Yjc0LWIxYmMtNDIyZGJlNzUyYTJi";
#else
        public const string ONE_SIGNAL_APP_ID = "19b7d4eb-ea96-45ce-b5e5-ce24a1b142f2";
        public const string ONE_SIGNAL_API_KEY = "NDg1YWFhNjAtM2I5ZS00ZTg2LWJjNTQtZWYxN2ZhYTI4NWQx";
#endif

        // Reddit API for posting trip reports
        public const string REDDIT_APP_ID = "nM7FBUmE8LexWA";
        public const string REDDIT_APP_SECRET = "hwI08Qui3KQixDT9RWiN1Yjn-5A";
        public const string REDDIT_REFRESH_TOKEN = "382783126207-AbUlBTuHkaYNrea1DyV29ppkoG4";
        public const string REDDIT_ACCESS_TOKEN = "382783126207-XgtjL7R5qY-lsw_sCXS4Ix0xklw";

        // SQL server for posting trip reports/generated points
        public const string DB_SERVER = "ratumbot.database.windows.net";
        public const string DB_USER = "fatumbot";
        public const string DB_PASSWORD = "AehGSRvJ#DeBs!02Fun12";
        public const string DB_NAME = "ratumbot";

        // https://what3words.com API key
        public const string W3W_API_KEY = "Y8GUOC45";

        // For uploading user trip report photos
        public const string IMGUR_API_CLIENT_ID = "a79960f801d5020";
        public const string IMGUR_API_CLIENT_SECRET = "7a4a22f9f5f6eb1138ca647c2815347d49f5d18b";

        // For verifying iOS in-app purchase receipts with Apple iTunes
        public const string ITUNES_VERIFY_RECEIPTS_PASSWORD = "e31a4b5a6bcd4c8d86db53f9fae7333c";

        // For checking whether coordinates are water or not
        public const string ONWATER_IO_API_KEY = "43d-sA4uufiucpqGZczs";

        // For tracking events with Amplitude
#if RELEASE_PROD
        public const string AMPLITUDE_HTTP_API_KEY = "0bd7e7711d1c3a30578d365260d2d166";
#else
        public const string AMPLITUDE_HTTP_API_KEY = "ff37ac1848cacc399af560601253c125";
#endif

        public const int DAILY_MAX_FREE_OWL_TOKENS_REFILL = 2;

        // Google Maps etc thumbnail sizes to use in reply cards
        public const string THUMBNAIL_SIZE = "320x320";

        // Dummy invalid coordinate
        public const double INVALID_COORD = -1000.0;

        // Default radius to search within (meters)
        public const int DEFAULT_RADIUS = 3000;

        // Max radius
        public const int RADIUS_MAX = 10000;

        // Min radius
        public const int RADIUS_MIN = 1000;

        // Max chain distance
        public const float CHAIN_DISTANCE_MAX = 20;

        // Min chain distance
        public const float CHAIN_DISTANCE_MIN = 2;

        // Maximum number of tries to search for non-water points before giving up
        public const int WATER_POINTS_SEARCH_MAX = 10;

        // TODO: move later when localization is implemented
        public static string NO_LOCATION_SET_MSG = Loc.g("no_loc_or_reset"); //"You haven't set a location, or it was reset. Send it by tapping 🌍/📎 or sending a Google Maps URL. You can also type \"help\" anytime.";
    }
}
