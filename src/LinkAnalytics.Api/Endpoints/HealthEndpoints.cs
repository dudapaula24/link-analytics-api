using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkAnalytics.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Returns the current health status of the API.");

        return app;
    }

    private static Ok<HealthResponse> GetHealth() =>
        TypedResults.Ok(new HealthResponse("Healthy", DateTimeOffset.UtcNow));
}

public sealed record HealthResponse(string Status, DateTimeOffset Timestamp);
