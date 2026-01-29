using log4net;
using Newtonsoft.Json;

namespace Manager;

public class CacheItem
{
    public object? Value { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int Frequency { get; set; }
    public DateTime LastAccessedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}
