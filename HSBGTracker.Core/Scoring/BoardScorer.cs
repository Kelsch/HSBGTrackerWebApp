using HSBGTracker.Core.CardData;
using HSBGTracker.Core.Snapshots;

namespace HSBGTracker.Core.Scoring;

public static class BoardScorer
{
    public static double Score(BoardSnapshot board, ICardDatabase? cardDb = null)
    {
        double minionScore = board.Minions.Sum(m => MinionScorer.Score(m, cardDb));

        // Flat baseline per trinket until you want per-trinket weights (a Greater Trinket
        // and a Lesser Trinket aren't equally strong, but that data isn't modeled yet).
        double trinketScore = board.Trinkets.Sum(t => t.Tier == TrinketTier.Greater ? 4 : 2);

        double tierScore = board.TavernTier * 2;

        return minionScore + trinketScore + tierScore;
    }
}
