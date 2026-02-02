public static class CacheServerConstants
{
    // Configuration
    public const string CacheSettingsMaxItemsConfigName = "CacheSettings:MaxItems";
    public const string CacheSettingsPortConfigName = "CacheSettings:Port";
    public const string CacheSettingsNotificationPortConfigName = "CacheSettings:NotificationPort";
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

    // Logging & Errors
    public const string InvalidRequest = "Invalid request";
    public const string ClientConnected = "Client connected: {0}";
    public const string ClientSubscribed = "Client {0} subscribed to: {1}";
    public const string ClientUnsubscribed = "Client {0} unsubscribed";
    public const string ClientInvalidJson = "Client {0} sent invalid JSON. Disconnecting.";
    public const string ClientDisconnected = "Client {0} disconnected: {1}";
    public const string ClientDisconnectedInfo = "Client disconnected: {0}";
    public const string ClientWriteFailed = "Failed to write to client {0}: {1}";

    // CacheHostedService
    public const string StartingCacheServer = "Starting Cache Server on port {0}";
    public const string CacheServerStartedSuccess = "Cache Server started successfully";
    public const string ServerFailedToStart = "Server failed to start";
    public const string StoppingServers = "Stopping servers";

    // CacheManager Additional
    public const string CreateFailedEmpty = "CREATE FAILED: Empty or null key provided, Value={0}";
    public const string CapacityReachedLog = "CAPACITY REACHED: Cache full, triggering LFU eviction {0}";
    public const string CreateSuccessLog = "CREATE SUCCESS: Key='{0}', Value={1}{2} {3}";
    public const string CreateDuplicateLog = "CREATE FAILED (duplicate): Key='{0}' already exists {1}";
    
    public const string ReadFailedEmpty = "READ FAILED: Empty or null key provided";
    public const string ReadMissLog = "READ MISS: Key='{0}' not found {1}";
    public const string ReadExpiredLog = "READ EXPIRED: Key='{0}', Value={1}, ExpiredAt={2}, Frequency={3} {4}";
    public const string ReadHitLog = "READ HIT: Key='{0}', Value={1}, Frequency={2}->{3}{4} {5}";
    
    public const string UpdateFailedEmpty = "UPDATE FAILED: Empty or null key provided, Value={0}";
    public const string UpdateNotFoundLog = "UPDATE FAILED: Key='{0}' not found {1}";
    public const string UpdateExpiredLog = "UPDATE EXPIRED: Key='{0}', OldValue={1}, ExpiredAt={2} {3}";
    public const string UpdateSuccessLog = "UPDATE SUCCESS: Key='{0}', OldValue={1}, NewValue={2}, Frequency={3}{4} {5}";
    
    public const string DeleteFailedEmpty = "DELETE FAILED: Empty or null key provided";
    public const string DeleteSuccessLog = "DELETE SUCCESS: Key='{0}', Value={1}, Frequency={2}{3} {4}";
    public const string DeleteNotFoundLog = "DELETE FAILED: Key='{0}' not found {1}";
    
    public const string CleanupExpiredLog = "CLEANUP EXPIRED: Key='{0}', Value={1}, ExpiredAt={2}, Frequency={3}";
    public const string CleanupCompleteLog = "CLEANUP COMPLETE: Removed {0} expired item(s) {1}";
    public const string LfuEvictionLog = "LFU EVICTION: Key='{0}', Value={1}, Frequency={2}{3} {4}";
    public const string LfuEvictionStart = "LFU eviction (frequency: {0})";
    public const string LfuEvictionNotFoundLog = "LFU EVICTION: Key='{0}' was in frequency bucket but not in cache";
    public const string CacheManagerDisposed = "CacheManager disposed";
}
