using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum AtBatResult { SO, GB, FB, PU, BB, Single, SinglePlus, Double, Triple, HR }
public enum CardType { Pitcher, Batter }

[System.Serializable]
public class ChartEntry
{
    public int rollMin;   // inclusive
    public int rollMax;   // inclusive
    public AtBatResult result;
}

[CreateAssetMenu(fileName = "NewShowdownCard", menuName = "Showdown/Card")]
public class ShowdownCardData : ScriptableObject
{
    public string playerName;
    public CardType cardType;

    [Header("Card Economy")]
    [Tooltip("Draft/shop cost in gold")]
    public int cost = 2;

    [Header("Pitcher Only")]
    [Tooltip("Added to the d20 pitch roll and compared against batter OnBase")]
    public int control;

    [Header("Batter Only")]
    [Tooltip("Compared against pitcher's (control + d20 roll). Batter wins if roll < OnBase")]
    public int onBase;

    public Sprite portrait;

    [Header("Result Chart (covers 1–20, no gaps or overlaps)")]
    public List<ChartEntry> chart = new List<ChartEntry>();



    // ShowdownCardData.cs — new fields, only meaningful when cardType == Pitcher
    public enum BossDifficulty { Easy, Medium, Hard, Epic }

    public BossDifficulty difficulty;   // for map display / tree building later
    public int runsToBeat;              // actual win threshold used by InningManager

    /// <summary>Look up a swing roll (1–20) on this card's chart.</summary>
    public AtBatResult LookupResult(int swingRoll)
    {
        foreach (var entry in chart)
        {
            if (swingRoll >= entry.rollMin && swingRoll <= entry.rollMax)
                return entry.result;
        }

        Debug.LogWarning($"[Showdown] No chart entry found for roll {swingRoll} on {playerName}. Defaulting to GB.");
        return AtBatResult.GB;
    }
}
