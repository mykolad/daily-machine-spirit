using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DailyMachineSpirit.Functions.Scriptorium.SignIn;

/// <summary>
/// Chooses how Scribes are recognised, failing closed: Cloudflare Access where it's set up, no sign-in only where
/// <see cref="ScriptoriumOptions.WithoutSignIn"/> says so (staging, behind its IP rule), and otherwise nobody. The
/// Expurgators (<see cref="ExpurgatorSignIn"/>) the same way, against their own Access application.
/// </summary>
public static class ScribeSignInRegistration
{
    public static IServiceCollection AddScribeSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        var accessSection = configuration.GetSection(CloudflareAccessOptions.SectionName);
        services.Configure<CloudflareAccessOptions>(accessSection);
        var access = accessSection.Get<CloudflareAccessOptions>() ?? new CloudflareAccessOptions();
        var scriptorium = configuration.GetSection(ScriptoriumOptions.SectionName).Get<ScriptoriumOptions>() ?? new ScriptoriumOptions();

        if (access.IsConfigured)
        {
            services.AddHttpClient(CloudflareAccessSignIn.HttpClientName, http => http.Timeout = TimeSpan.FromSeconds(10));
            // One for the app's lifetime, so the team's signing keys are fetched once an hour, not for every request.
            services.AddSingleton<IScribeSignIn, CloudflareAccessSignIn>();
            // The Expurgators' application is another audience, checked with its own copy of the keys.
            var expurgators = new CloudflareAccessOptions { TeamDomain = access.TeamDomain, Audience = access.ExpurgatorAudience };
            return services.AddSingleton(provider => new ExpurgatorSignIn(expurgators.IsConfigured
                ? ActivatorUtilities.CreateInstance<CloudflareAccessSignIn>(provider, Options.Create(expurgators))
                : new NobodyIsScribeSignIn()));
        }
        return scriptorium.WithoutSignIn
            ? services.AddSingleton<IScribeSignIn, EveryoneIsScribeSignIn>().AddSingleton(new ExpurgatorSignIn(new EveryoneIsScribeSignIn()))
            : services.AddSingleton<IScribeSignIn, NobodyIsScribeSignIn>().AddSingleton(new ExpurgatorSignIn(new NobodyIsScribeSignIn()));
    }
}
