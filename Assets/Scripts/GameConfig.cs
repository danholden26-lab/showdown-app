using System.Collections.Generic;
using UnityEngine;

public enum DraftTier { Low, Mid, Epic }

[CreateAssetMenu(fileName = "GameConfig", menuName = "Showdown/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Economy")]
    public int goldPerInning = 3;
    public int goldPerRun = 1;
    public int shopSlots = 3;

    [Header("Sell Back")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of card cost returned when selling a player. 0.5 = 50% back.")]
    public float playerSellBackRate = 0.5f;

    [Header("Draft")]
    public int rosterSize = 9;
    public int draftChoices = 4;

    [Header("Draft Tiers (by card points)")]
    [Tooltip("Cards with at least this many points are Epic.")]
    public int epicMinPoints = 600;

    [Tooltip("Cards with at least this many points (and below Epic) are Mid. Anything lower is Low.")]
    public int midMinPoints = 500;

    [Header("Draft Quota (should add up to Roster Size)")]
    public int epicPicks = 2;
    public int midPicks = 4;
    public int lowPicks = 3;

    [Tooltip("First pick is always Epic (the captain pick). The remaining picks are shuffled.")]
    public bool captainPickFirst = true;

    [Header("Boss Rush")]
    public int inningsPerBoss = 9;

    [Header("Stealing")]
    [Tooltip("Standard catcher fielding value added to the defender's d20 on steal attempts. Placeholder until teams have real catchers; bosses add their own fieldingBonus on top.")]
    public int defaultCatcherFielding = 2;

    [Tooltip("Extra defense added to the defender's roll when the runner is going for 3rd.")]
    public int stealThirdDefenseBonus = 5;

    [Header("Classic Mode")]
    public int classicInnings = 9;

    /// <summary>Which draft tier a card with this many points belongs to.</summary>
    public DraftTier GetTier(int points)
    {
        if (points >= epicMinPoints) return DraftTier.Epic;
        if (points >= midMinPoints)  return DraftTier.Mid;
        return DraftTier.Low;
    }

    /// <summary>
    /// The tier for each pick of a draft, in order. Uses the quota above;
    /// with captainPickFirst the first pick is Epic and the rest are shuffled.
    /// </summary>
    public List<DraftTier> BuildDraftSchedule()
    {
        var picks = new List<DraftTier>();
        for (int i = 0; i < epicPicks; i++) picks.Add(DraftTier.Epic);
        for (int i = 0; i < midPicks; i++)  picks.Add(DraftTier.Mid);
        for (int i = 0; i < lowPicks; i++)  picks.Add(DraftTier.Low);

        if (picks.Count != rosterSize)
            Debug.LogWarning($"[GameConfig] Draft quota is {picks.Count} picks but rosterSize is {rosterSize}. " +
                             "Padding with Mid / trimming to fit.");

        while (picks.Count < rosterSize) picks.Add(DraftTier.Mid);

        bool captain = captainPickFirst && picks.Contains(DraftTier.Epic);
        if (captain) picks.Remove(DraftTier.Epic);   // removes one; it becomes pick 1

        // Fisher-Yates shuffle
        for (int i = picks.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (picks[i], picks[j]) = (picks[j], picks[i]);
        }

        var schedule = new List<DraftTier>();
        if (captain) schedule.Add(DraftTier.Epic);
        schedule.AddRange(picks);

        if (schedule.Count > rosterSize)
            schedule.RemoveRange(rosterSize, schedule.Count - rosterSize);

        return schedule;
    }

    /// <summary>Calculate sell price for a card based on its cost and the sell back rate.</summary>
    public int GetSellPrice(int cardCost)
    {
        return Mathf.Max(1, Mathf.FloorToInt(cardCost * playerSellBackRate));
    }
}
