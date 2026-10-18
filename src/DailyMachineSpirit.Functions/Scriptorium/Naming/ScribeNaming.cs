using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>
/// The names the Scribes choose for the Record (#84): one each, by their Access id, unique among them, and never one of
/// the names the Record gives to others. Choosing is recorded, with the name before it.
/// </summary>
public sealed partial class ScribeNaming
{
    public static readonly Error NameNeeded = new NameRefused("Write a name first.");

    public static readonly Error TooLong = new NameRefused($"A name can be at most {ScribeName.MaxLength} characters.");

    public static readonly Error Taken = new NameRefused("Another Scribe goes by that name. Choose another.");

    public static readonly Error Reserved = new NameRefused("The Record gives that name to others. Choose another.");

    public static readonly Error Email = new NameRefused("A name or an alias, never an email address: the Record isn't a place for one.");

    private readonly IScribeNameRepository names;
    private readonly TimeProvider time;

    public ScribeNaming(IScribeNameRepository names, TimeProvider time)
    {
        this.names = names;
        this.time = time;
    }

    public Task<Either<Error, List<ScribeName>>> All(CancellationToken cancellationToken)
        => names.GetAll(cancellationToken).MapAsync(stored => Task.FromResult(stored.Names));

    /// <summary>The name the Scribe chose, if any. None on staging, where there's no name to choose.</summary>
    public Task<Either<Error, Option<string>>> NameOf(string scribe, CancellationToken cancellationToken)
        => All(cancellationToken).MapAsync(all => Task.FromResult(
            all.Where(name => name.AccessId == scribe).Select(name => name.Name).HeadOrNone()));

    /// <summary>
    /// Saves the name for the Scribe and returns it as saved (spaces tidied). Their own name again changes nothing.
    /// </summary>
    public Task<Either<Error, string>> Choose(string scribe, string typed, CancellationToken cancellationToken)
        => Acceptable(typed).BindAsync(name => names.GetAll(cancellationToken).BindAsync(read =>
        {
            var current = read.Names.Where(found => found.AccessId == scribe).HeadOrNone();
            if (current.Exists(found => found.Name == name))
            {
                return Task.FromResult(Right<Error, string>(name));
            }
            if (read.Names.Exists(found => found.AccessId != scribe && SameName(found.Name, name)))
            {
                return Task.FromResult(Left<Error, string>(Taken));
            }
            var changed = read.Names.Where(found => found.AccessId != scribe).Append(new ScribeName(scribe, name)).ToList();
            var audit = new AuditEntry
            {
                Id = Guid.NewGuid(),
                AtUtc = time.GetUtcNow().UtcDateTime,
                Actor = scribe,
                Action = AuditAction.ChooseName,
                Title = name,
                Detail = current.Map(found => $"was “{found.Name}”"),
            };
            return names.SaveUnlessChangedSince(read, changed, audit, cancellationToken).MapAsync(_ => Task.FromResult(name));
        }));

    /// <summary>
    /// The name as it would be saved, tidied, or why it can't be: empty, too long, one the Record gives to others, or an
    /// email address. Whether it's taken is the saved names' to say. Suggestions pass the same rules.
    /// </summary>
    public static Either<Error, string> Acceptable(string typed)
        => Tidy(typed).ToEither(NameNeeded).Bind(name =>
            name.Length > ScribeName.MaxLength ? Left<Error, string>(TooLong)
            : IsReserved(name) ? Left<Error, string>(Reserved)
            // An address is more than a name: only the Scribes see the Record, but it keeps entries for as long as the rites.
            : name.Contains('@') ? Left<Error, string>(Email)
            : Right<Error, string>(name));

    public static bool SameName(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    // Runs of spaces, tabs and line breaks become one space, and control characters go: a name is one plain line.
    private static Option<string> Tidy(string typed)
    {
        var name = Spaces().Replace(new string(typed.Where(character => !char.IsControl(character) || char.IsWhiteSpace(character)).ToArray()), " ").Trim();
        return name.Length == 0 ? None : Some(name);
    }

    // The names the Record gives to others, and the pattern of an unnamed Scribe's, which a name mustn't pass for.
    private static bool IsReserved(string name)
        => SameName(name, ScribeNames.MachineSpirit) || SameName(name, ScribeNames.OnStaging)
            || name.StartsWith("A Scribe (", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
