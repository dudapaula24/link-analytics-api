using LinkAnalytics.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LinkAnalytics.Api.Endpoints;

public static class RedirectEndpoints
{
    public static IEndpointRouteBuilder MapRedirectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/r/{code}", RedirectToOriginalUrl)
            .WithName("RedirectToOriginalUrl")
            .WithTags("Redirect")
            .WithSummary("Records an access and redirects (302 Found) to the original URL of a short link.");

        return app;
    }

    private static async Task<Results<RedirectHttpResult, NotFound>> RedirectToOriginalUrl(
        string code,
        ShortLinkService linkService,
        LinkStatsService statsService,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        // Browsers and proxies must not store these responses: every access has to reach the API
        // to be counted. Also applied to 404, since the code may exist later.
        response.Headers.CacheControl = "no-store";

        var link = await linkService.FindByCodeAsync(code, cancellationToken);

        if (link is null)
        {
            return TypedResults.NotFound();
        }

        await statsService.RecordClickAsync(link.Id, cancellationToken);

        // Temporary redirect (302), unlike 301, is not cached permanently by browsers.
        return TypedResults.Redirect(link.OriginalUrl, permanent: false);
    }
}
