using System.Text.Json;
using Courier.Domain.Engine;

namespace Courier.Features.Engine;

public static class ContextResolver
{
    private const string ContextPrefix = "context:";

    public static string Resolve(string value, JobContext context)
    {
        if (!value.StartsWith(ContextPrefix))
            return value;

        var key = value[ContextPrefix.Length..];
        return TryResolveKey(key, context, out var resolved)
            ? resolved
            : throw new InvalidOperationException($"Context reference '{key}' not found");
    }

    public static bool TryResolve(string value, JobContext context, out string resolved)
    {
        if (!value.StartsWith(ContextPrefix))
        {
            resolved = value;
            return true;
        }

        var key = value[ContextPrefix.Length..];
        return TryResolveKey(key, context, out resolved);
    }

    private static bool TryResolveKey(string key, JobContext context, out string resolved)
    {
        // Direct string match
        if (context.TryGet<string>(key, out var strVal) && strVal is not null)
        {
            resolved = strVal;
            return true;
        }

        // Check for nested property access on JSON objects (e.g., "loop.current_item.name")
        var dotIndex = key.LastIndexOf('.');
        if (dotIndex > 0)
        {
            var parentKey = key[..dotIndex];
            var propertyName = key[(dotIndex + 1)..];

            // Try as JsonElement (common when loop items come from JSON arrays)
            if (context.TryGet<JsonElement>(parentKey, out var jsonElement))
            {
                if (jsonElement.ValueKind == JsonValueKind.Object &&
                    jsonElement.TryGetProperty(propertyName, out var prop))
                {
                    resolved = prop.ValueKind == JsonValueKind.String
                        ? prop.GetString()!
                        : prop.GetRawText();
                    return true;
                }
            }

            // Try as dictionary
            if (context.TryGet<Dictionary<string, object>>(parentKey, out var dict) && dict is not null)
            {
                if (dict.TryGetValue(propertyName, out var dictVal))
                {
                    resolved = dictVal?.ToString() ?? string.Empty;
                    return true;
                }
            }
        }

        // Try getting the raw value as object and converting to string
        if (context.TryGet<object>(key, out var objVal) && objVal is not null)
        {
            if (objVal is JsonElement je)
            {
                resolved = je.ValueKind == JsonValueKind.String
                    ? je.GetString()!
                    : je.GetRawText();
                return true;
            }

            resolved = objVal.ToString()!;
            return true;
        }

        resolved = string.Empty;
        return false;
    }
}
