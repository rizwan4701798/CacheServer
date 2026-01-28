public static class CacheServerConstants
{
    // Configuration
    public const string CacheSettingsMaxItemsConfigName = "CacheSettings:MaxItems";
    public const string CacheSettingsPortConfigName = "CacheSettings:Port";
    public const string Log4netConfigName = "log4net.config";

    // Server lifecycle
    public const string CacheServerStarted = "Cache Server started";
    public const string CacheServerStopped = "Cache Server stopped";

    // Cache operations
    public const string CacheIsFull = "Cache is full";
    public const string InvalidOperation = "Invalid operation";

    // Request processing
    public const string ErrorAcceptingClient = "Error accepting client";
    public const string ErrorHandlingClient = "Error handling client";
    public const string ProcessingRequestFailed = "Processing request failed";
    public const string InvalidCacheSize = "Invalid cache size configured";
    public const string CacheInitialized = "CacheManager initialized with capacity {0}";
    public const string CreateEmptyKey = "Attempt to CREATE cache entry with empty key";
    public const string CreateSuccess = "Cache CREATE successful. Key='{0}'";
    public const string CreateDuplicate = "Cache CREATE failed (duplicate key). Key='{0}'";
    public const string ReadEmptyKey = "Attempt to READ cache entry with empty key";
    public const string CacheHit = "Cache HIT. Key='{0}'";
    public const string CacheMiss = "Cache MISS. Key='{0}'";
    public const string UpdateEmptyKey = "Attempt to UPDATE cache entry with empty key";
    public const string UpdateMissingKey = "Cache UPDATE failed. Key not found. Key='{0}'";
    public const string UpdateSuccess = "Cache UPDATE successful. Key='{0}'";
    public const string DeleteEmptyKey = "Attempt to DELETE cache entry with empty key";
    public const string DeleteSuccess = "Cache DELETE successful. Key='{0}'";
    public const string DeleteMissingKey = "Cache DELETE failed. Key not found. Key='{0}'";
    public const string CacheSizeMustBeGreaterThanZero = "Cache size must be greater than zero.";
    public const string CacheCapacityReached = "Cache capacity reached. MaxItems={0}, CurrentCount={1}";

    // Operations
    public const string CREATE = "CREATE";
    public const string READ = "READ";
    public const string UPDATE = "UPDATE";
    public const string DELETE = "DELETE";
}
