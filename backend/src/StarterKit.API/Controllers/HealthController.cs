using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace StarterKit.API.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private static readonly string Version = TrimBuildMetadata(
        Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown");

    private static readonly DateTime? BuildTimestamp = GetBuildTimestampLocal();

    private static string TrimBuildMetadata(string version)
    {
        int metadataIndex = version.IndexOf('+');
        return metadataIndex < 0 ? version : version[..metadataIndex];
    }

    private static DateTime? GetBuildTimestampLocal()
    {
        string? raw = Assembly
            .GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildTimestampUtc")
            ?.Value;

        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime buildUtc))
        {
            return null;
        }

        try
        {
            TimeZoneInfo vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            return TimeZoneInfo.ConvertTimeFromUtc(buildUtc, vietnamTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return buildUtc;
        }
    }

    /// <summary>Checks the operational status of the API. No authentication required.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new HealthResponse("healthy", Version, DateTime.UtcNow, BuildTimestamp));
    }

    /// <param name="Status">Service status, always "healthy" if the API is running.</param>
    /// <param name="Version">The running assembly version.</param>
    /// <param name="Timestamp">The response timestamp (UTC).</param>
    /// <param name="BuildTimestamp">When this build was compiled, in Vietnam local time (Asia/Ho_Chi_Minh) — used as a proxy for the deploy time, since deploys build and ship the same artifact. Null if unavailable.</param>
    public sealed record HealthResponse(string Status, string Version, DateTime Timestamp, DateTime? BuildTimestamp);
}
