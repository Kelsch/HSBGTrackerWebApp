using HSBGTracker.Core.CardData;
using HSBGTracker.Core.Snapshots;

namespace HSBGTracker.Core.Scoring;

/// <summary>
/// Produces a strength score for a single minion. This is a starting heuristic, not a
/// simulator - tune the weights once you've got real game data to compare against.
/// Passing a card database enables factoring in Deathrattle/summon value; without one,
/// scoring falls back to current stats + keywords only.
/// </summary>
public static class MinionScorer
{
    public static double Score(MinionSnapshot minion, ICardDatabase? cardDb = null)
    {
        double statScore = minion.Attack + minion.Health;

        double keywordScore = 0;
        if (minion.Taunt) keywordScore += 2;
        if (minion.DivineShield) keywordScore += 4;
        if (minion.Poisonous) keywordScore += 5;
        if (minion.Reborn) keywordScore += 3;
        if (minion.Windfury) keywordScore += 3;

        double tierScore = minion.TavernTier * 1.5;
        double summonScore = cardDb is null ? 0 : ScoreSummonPotential(minion, cardDb);

        double total = statScore + keywordScore + tierScore + summonScore;
        return minion.IsGolden ? total * 1.5 : total;
    }

    private static double ScoreSummonPotential(MinionSnapshot minion, ICardDatabase cardDb)
    {
        var def = cardDb.Find(minion.CardId);
        if (def is null || def.Summons.Count == 0) return 0;

        double total = 0;
        foreach (var summon in def.Summons)
        {
            var summonedDef = cardDb.Find(summon.CardId);
            // Card not in the DB yet (e.g. new set) - use a conservative flat estimate
            // rather than silently scoring it as zero.
            double summonedStats = summonedDef is not null
                ? summonedDef.BaseAttack + summonedDef.BaseHealth
                : 2;

            total += summonedStats * summon.Count * summon.Triggers;
        }

        // Discounted relative to stats already on the board - a Deathrattle only pays
        // off if the minion actually dies, so it shouldn't count at full value.
        return total * 0.5;
    }
}
