using System.Text.RegularExpressions;
using DailyMachineSpirit.Data.Entities;
using DailyMachineSpirit.Data.Repositories;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace DailyMachineSpirit.Functions.Scriptorium.Naming;

/// <summary>
/// The names the Scribes choose for the Record (#84): one each, by their Access id, unique among them, and never one of
/// the names the Record gives to others. Choosing is recorded, with the name before it. On staging every Scribe is one
/// actor, "A Scribe on staging", so there's no name to choose.
/// </summary>
public sealed partial class ScribeNaming
{
    /// <summary>The code of a name the Scribe should change: its message says why.</summary>
    public const int Refused = 8401;

    public static readonly Error NameNeeded = Error.New(Refused, "Write a name first.");

    public static readonly Error TooLong = Error.New(Refused, $"A name can be at most {ScribeName.MaxLength} characters.");

    public static readonly Error Taken = Error.New(Refused, "Another Scribe goes by that name. Choose another.");

    public static readonly Error Reserved = Error.New(Refused, "The Record gives that name to others. Choose another.");

    public static readonly Error NoNamesOnStaging = Error.New(Refused, "Without a sign-in, every Scribe on staging is one: \"A Scribe on staging\".");

    private readonly IScribeNameRepository names;
    private readonly TimeProvider time;

    public ScribeNaming(IScribeNameRepository names, TimeProvider time)
    {
        this.names = names;
        this.time = time;
    }

    public Task<Either<Error, List<ScribeName>>> All(CancellationToken cancellationToken)
        => names.Get(cancellationToken).MapAsync(stored => Task.FromResult(stored.Names));

    /// <summary>The name the Scribe chose, if any. None on staging, where there's no name to choose.</summary>
    public Task<Either<Error, Option<string>>> NameOf(string scribe, CancellationToken cancellationToken)
        => All(cancellationToken).MapAsync(all => Task.FromResult(
            all.Where(name => name.AccessId == scribe).Select(name => name.Name).HeadOrNone()));

    /// <summary>
    /// Saves the name for the Scribe and returns it as saved (spaces tidied). Their own name again changes nothing.
    /// </summary>
    public Task<Either<Error, string>> Choose(string scribe, string typed, CancellationToken cancellationToken)
        => Tidied(scribe, typed).BindAsync(name => names.Get(cancellationToken).BindAsync(read =>
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
            return names.Save(read, changed, audit, cancellationToken).MapAsync(_ => Task.FromResult(name));
        }));

    /// <summary>Whether a suggested name could be chosen: short enough, readable, and none of the Record's own.</summary>
    public static bool Choosable(string name)
        => Tidied(name).Exists(tidied => tidied.Length <= ScribeName.MaxLength && !IsReserved(tidied));

    public static bool SameName(string one, string other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    private static Either<Error, string> Tidied(string scribe, string typed)
    {
        if (scribe == AuditEntry.StagingScribe)
        {
            return NoNamesOnStaging;
        }
        return Tidied(typed).ToEither(NameNeeded).Bind(name =>
            name.Length > ScribeName.MaxLength ? Left<Error, string>(TooLong)
            : IsReserved(name) ? Left<Error, string>(Reserved)
            : Right<Error, string>(name));
    }

    // Runs of spaces, tabs and line breaks become one space, and control characters go: a name is one plain line.
    private static Option<string> Tidied(string typed)
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
