using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api;

/// <summary>ETag helpers for listing responses.</summary>
public static class ETagExtensions
{
    /// <summary>Formats a row-version string as a strong entity tag.</summary>
    public static string? ToETag(string? version)
        => string.IsNullOrWhiteSpace(version) ? null : $"\"{version.Trim('"')}\"";

    /// <summary>Sets the response ETag from a row-version string when one is present.</summary>
    public static void SetETag(this ControllerBase controller, string? version)
    {
        var etag = ToETag(version);
        if (etag is not null)
        {
            controller.Response.Headers.ETag = etag;
        }
    }
}
