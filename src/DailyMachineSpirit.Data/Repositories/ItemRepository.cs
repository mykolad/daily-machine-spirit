using DailyMachineSpirit.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyMachineSpirit.Data.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly MachineSpiritDbContext context;

    public ItemRepository(MachineSpiritDbContext context)
    {
        this.context = context;
    }

    public async Task<Item?> GetById(int id, CancellationToken cancellationToken)
        => await context.Items.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<Item?> GetPublishedOn(DateOnly utcDate, CancellationToken cancellationToken)
        => await context.Items.AsNoTracking().SingleOrDefaultAsync(i => i.PublishedOnUtc == utcDate, cancellationToken);

    public async Task<List<Item>> GetNewest(int count, CancellationToken cancellationToken)
        => await context.Items.AsNoTracking()
            .OrderByDescending(i => i.PublishedOnUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryAdd(Item item, CancellationToken cancellationToken)
    {
        // The unique index decides: checking first would still race with another instance.
        context.Items.Add(item);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            context.Entry(item).State = EntityState.Detached;
            // Ask the database rather than decode provider-specific errors: if the day is taken, this item couldn't be
            // saved anyway, whatever else went wrong; on a free day, every failure is rethrown.
            if (await context.Items.AsNoTracking().AnyAsync(i => i.PublishedOnUtc == item.PublishedOnUtc, cancellationToken))
                return false;
            throw;
        }
    }

    public async Task SaveScores(int itemId, string scoresGeneratorVersion, float[] scores, DateTime createdAtUtc, CancellationToken cancellationToken)
    {
        var profile = await context.ItemProfiles.SingleOrDefaultAsync(p => p.ItemId == itemId, cancellationToken);
        if (profile is null)
            context.ItemProfiles.Add(profile = new ItemProfile { ItemId = itemId });
        profile.ScoresGeneratorVersion = scoresGeneratorVersion;
        profile.Scores = scores;
        profile.CreatedAtUtc = createdAtUtc;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, float[]>> GetScoresByItemId(string scoresGeneratorVersion, CancellationToken cancellationToken)
        => await context.ItemProfiles.AsNoTracking()
            .Where(p => p.ScoresGeneratorVersion == scoresGeneratorVersion)
            .ToDictionaryAsync(p => p.ItemId, p => p.Scores, cancellationToken);
}
