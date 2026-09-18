using System.Text.Json;
using System.Text.Json.Serialization;

namespace Miautrix.Mail.Web.Infrastructure;

/// <summary>
/// Canonical JSON options for the public API: snake_case wire names, ISO-8601
/// UTC timestamps, no silently-dropped properties on either read or write.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));

        return options;
    }
}
