using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Pages;

/// <summary>
/// The public pages, rendered on the server: Today (<c>/</c>), the archive (<c>/archive</c>) and a rite's own page
/// (<c>/p/&lt;number&gt;</c>). Any other address gets the 404 page.
/// </summary>
public sealed class SitePages
{
    private readonly IRiteRepository rites;
    private readonly TimeProvider time;
    private readonly ILogger<SitePages> logger;

    public SitePages(IRiteRepository rites, TimeProvider time, ILogger<SitePages> logger)
    {
        this.rites = rites;
        this.time = time;
        this.logger = logger;
    }

    // The catch-all route also takes "/": more specific routes (healthz, archive, p/…) win over it.
    [Function("Today")]
    public async Task<IActionResult> Today(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{*path}")] HttpRequest request,
        string? path,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(path))
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(path));

        var now = time.GetUtcNow();
        return (await rites.GetNewest(1, cancellationToken)).Match(
            Right: newest => newest.HeadOrNone().Match(
                Some: rite => Page(request, StatusCodes.Status200OK, TodayPage.Render(rite, now)),
                None: () => Page(request, StatusCodes.Status200OK, TodayPage.Slumbering(now))),
            Left: error => Failed(request, error));
    }

    [Function("Rite")]
    public async Task<IActionResult> Rite(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "p/{id}")] HttpRequest request,
        string id,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var number) || number < 1)
            return Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id));

        return (await rites.GetByNumber(number, cancellationToken)).Match(
            Right: found => found.Match(
                Some: rite => Page(request, StatusCodes.Status200OK, RitePage.Render(rite)),
                None: () => Page(request, StatusCodes.Status404NotFound, StatePages.NotFound(id))),
            Left: error => Failed(request, error));
    }

    /// <summary>
    /// The first page starts below the newest rite, which Today shows; <c>?before=&lt;number&gt;</c> starts below that
    /// number. One rite more than a page is read, to know whether there are older ones.
    /// </summary>
    [Function("Archive")]
    public async Task<IActionResult> Archive(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request,
        CancellationToken cancellationToken)
    {
        var page = await Before(request, cancellationToken)
            .BindAsync(before => before.Match(
                Some: number => rites.GetOlderThan(number, ArchivePage.PageSize + 1, cancellationToken),
                None: () => Task.FromResult(Right<Error, List<Rite>>([]))));
        return page.Match(
            Right: found =>
            {
                var shown = found.Take(ArchivePage.PageSize).ToList();
                var olderThan = found.Count > ArchivePage.PageSize ? Some(shown[^1].Number) : Option<int>.None;
                return Page(request, StatusCodes.Status200OK, ArchivePage.Render(shown, olderThan));
            },
            Left: error => Failed(request, error));
    }

    // None when there are no rites at all, so the archive is empty.
    private async Task<Either<Error, Option<int>>> Before(HttpRequest request, CancellationToken cancellationToken)
    {
        if (int.TryParse(request.Query["before"], out var before) && before > 0)
            return Some(before);
        return (await rites.GetNewest(1, cancellationToken)).Map(newest => newest.HeadOrNone().Map(rite => rite.Number));
    }

    private IActionResult Failed(HttpRequest request, Error error)
    {
        logger.LogError(error.ToException(), "The page {Path} couldn't be served: {Reason}", request.Path.Value, error.Message);
        return Page(request, StatusCodes.Status503ServiceUnavailable, StatePages.Failed($"{request.Path}{request.QueryString}"));
    }

    private static ContentResult Page(HttpRequest request, int status, string html)
    {
        var headers = request.HttpContext.Response.Headers;
        // A rite never changes once published, and Today changes once a day: a minute's cache costs nothing.
        headers.CacheControl = status == StatusCodes.Status200OK ? "public, max-age=60" : "no-store";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        return new ContentResult { Content = html, ContentType = "text/html; charset=utf-8", StatusCode = status };
    }
}
