using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace CacheServerModels;

[JsonConverter(typeof(StringEnumConverter))]
public enum CacheOperation
{
    Create,
    Read,
    Update,
    Delete,
    Subscribe,
    Unsubscribe,
    Clear
}
