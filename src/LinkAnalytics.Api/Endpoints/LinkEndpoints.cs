using LinkAnalytics.Api.Contracts;
using LinkAnalytics.Api.Models;
using LinkAnalytics.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace LinkAnalytics.Api.Endpoints;

public static class LinkEndpoints
{
    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/links").WithTags("Links");

        group.MapPost("/", CreateLink)
            .WithName("CreateLink")
            .WithSummary("Creates a short link for an HTTP or HTTPS URL.");

        group.MapGet("/{code}", GetLink)
            .WithName("GetLink")
            .WithSummary("Returns the details of a short link.");

        group.MapGet("/{code}/stats", GetLinkStats)
            .WithName("GetLinkStats")
            .WithSummary("Returns the access statistics of a short link (days in UTC).");

        return app;
    }

    private static async Task<Results<Ok<LinkStatsResponse>, NotFound>> GetLinkStats(
        string code,
        LinkStatsService statsService,
        CancellationToken cancellationToken)
    {
        var stats = await statsService.GetStatsAsync(code, cancellationToken);

        return stats is null ? TypedResults.NotFound() : TypedResults.Ok(stats);
    }

    private static async Task<Results<Created<ShortLinkResponse>, ValidationProblem>> CreateLink(
        CreateShortLinkRequest request,
        ShortLinkService service,
        IOptions<ShortLinkOptions> options,
        CancellationToken cancellationToken)
    {
        if (!UrlValidator.TryNormalize(request.Url, out var url, out var error))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(CreateShortLinkRequest.Url).ToLowerInvariant()] = [error],
            });
        }

        var link = await service.CreateAsync(url, cancellationToken);

        return TypedResults.Created($"/api/links/{link.Code}", ToResponse(link, options.Value));
    }

    private static async Task<Results<Ok<ShortLinkResponse>, NotFound>> GetLink(
        string code,
        ShortLinkService service,
        IOptions<ShortLinkOptions> options,
        CancellationToken cancellationToken)
    {
        var link = await service.FindByCodeAsync(code, cancellationToken);

        return link is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ToResponse(link, options.Value));
    }

    private static ShortLinkResponse ToResponse(ShortLink link, ShortLinkOptions options) =>
        new(
            link.Id,
            link.Code,
            link.OriginalUrl,
            options.BuildShortUrl(link.Code),
            link.CreatedAt);
}
