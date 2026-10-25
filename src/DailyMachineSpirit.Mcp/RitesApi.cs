using System.Net;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Mcp;

/// <summary>
/// The public rites API (docs/api.md), read as any client reads it. Its answers are passed on as they are, so the
/// connector's tools keep the API's contract, fields added later included.
/// </summary>
public sealed class RitesApi
{
    private readonly HttpClient http;

    public RitesApi(HttpClient http)
    {
        this.http = http;
    }

    /// <summary>Every published rite, their Heretical Truths sealed: <c>/api/v1/rites</c>'s JSON.</summary>
    public async Task<Either<Error, string>> GetRites(CancellationToken cancellationToken)
        => (await Get("api/v1/rites", cancellationToken)).Bind(answer => answer.ToEither(Error.New("The rites API answered 404.")));

    /// <summary>One rite's Heretical Truth, as <c>/api/v1/rites/{number}/heretical-truth</c> gives it; none when no rite has the number.</summary>
    public Task<Either<Error, Option<string>>> GetHereticalTruth(int number, CancellationToken cancellationToken)
        => Get($"api/v1/rites/{number}/heretical-truth", cancellationToken);

    private async Task<Either<Error, Option<string>>> Get(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Right<Error, Option<string>>(None);
            }
            if (!response.IsSuccessStatusCode)
            {
                return Error.New($"The rites API answered {(int)response.StatusCode}.");
            }
            return Right<Error, Option<string>>(Some(await response.Content.ReadAsStringAsync(cancellationToken)));
        }
        catch (HttpRequestException exception)
        {
            return Error.New(exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout, not the caller's cancellation, which still throws.
            return Error.New(exception);
        }
    }
}
