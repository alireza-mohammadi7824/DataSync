using System.Text.Json;

namespace Monitoring.Shared;

public static class JsonElementExtensions
{
    public static bool DeepEquals(this JsonElement a, JsonElement b)
    {
        var left = JsonSerializer.Serialize(a);
        var right = JsonSerializer.Serialize(b);
        return left == right;
    }
}
