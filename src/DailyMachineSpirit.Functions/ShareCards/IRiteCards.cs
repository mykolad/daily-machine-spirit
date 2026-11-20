using DailyMachineSpirit.Data.Entities;

namespace DailyMachineSpirit.Functions.ShareCards;

/// <summary>A rite's share card (#191): the picture link previews show, the Heretical Truth sealed.</summary>
public interface IRiteCards
{
    /// <summary>The card as a PNG, 1200 × 630, its foot naming the rite's address on this host.</summary>
    byte[] Draw(Rite rite, string host);
}
