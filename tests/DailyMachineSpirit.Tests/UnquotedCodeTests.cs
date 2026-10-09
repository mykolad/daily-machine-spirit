using DailyMachineSpirit.Functions.Generation.Writing;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Tests;

public class UnquotedCodeTests
{
    [Theory]
    // The first rite staging published, which showed its commands as prose.
    [InlineData("O Machine Spirit, when thy test awakens before thy service, lay sleep 5 upon the altar.", "sleep 5")]
    [InlineData("Then intone rm -rf over the cursed folder.", "rm -rf")]
    [InlineData("Delete node_modules and begin anew.", "node_modules")]
    [InlineData("Push with --force, and the history shall be cleansed.", "--force")]
    [InlineData("Offer thy package.json upon the altar.", "package.json")]
    [InlineData("Hide thy secrets in .env, where none shall look.", ".env")]
    [InlineData("Then git push, and pray.", "git push")]
    [InlineData("Run npm install thrice.", "npm install")]
    [InlineData("Invoke docker restart at dawn.", "docker restart")]
    [InlineData("Murmur dotnet clean before the build.", "dotnet clean")]
    // Only half in backticks: part of it would still show as prose.
    [InlineData("Lay `sleep` 5 upon the altar.", "sleep 5")]
    [InlineData("Lay sleep `5` upon the altar.", "sleep 5")]
    [InlineData("Then `git push` --force, and pray.", "--force")]
    [InlineData("An unclosed ` quote hides not node_modules.", "node_modules")]
    public void Find_CatchesCodeWrittenAsWords(string text, string code)
        => Assert.Equal(Some(code), UnquotedCode.Find(text));

    [Theory]
    [InlineData("O Machine Spirit, when thy test awakens, lay `sleep 5` upon the altar.")]
    [InlineData("Delete `node_modules` with `rm -rf node_modules`, then `git push --force`.")]
    [InlineData("Grant the build thy long sleep, and let the cache be cleansed.")]
    [InlineData("Re-run the pipeline thrice, and the red shall turn green — so it is written.")]
    [InlineData("Fixed sleeps delay the test; wait for a readiness signal, e.g. a health check.")]
    [InlineData("Push thy commits, pull thy blessings, and git thee to the altar.")]
    [InlineData("The npm registry remembers; docker keeps its images.")]
    [InlineData("The npm cache is stale, and Docker Compose reads the configuration.")]
    [InlineData("Node.js keeps its module cache; Next.js keeps another.")]
    public void Find_LeavesProseAndBacktickedCodeAlone(string text)
        => Assert.True(UnquotedCode.Find(text).IsNone);
}
