using System.Collections.Generic;
using UnityEngine;

public enum UpgradeScope { AtBat, Inning, Game }
public enum UpgradeTargetType { Batter, Team, Field }

[System.Serializable]
public class SlotSwap
{
    [Tooltip("Result type to take slots away from")]
    public AtBatResult shrink;

    [Tooltip("Result type to give those slots to")]
    public AtBatResult grow;

    [Tooltip("How many consecutive slots to transfer")]
    public int count = 1;
}


// -------------------------------------------------------------------------
// Effect interfaces — each hooks a different point in the at-bat pipeline
// -------------------------------------------------------------------------

public interface IChartEffect
{
    List<ChartEntry> Apply(List<ChartEntry> chart);
}

public interface IRollModifierEffect
{
    int ModifyRoll(int rawRoll);
}

public interface IResultEffect
{
    AtBatResult Apply(AtBatResult result);
}

public interface IStatModifierEffect
{
    int ModifyOnBase(int rawOnBase);
}


// -------------------------------------------------------------------------
// At-bat event actions
// -------------------------------------------------------------------------

public enum AtBatEventAction
{
    AddShadowClone
}


// -------------------------------------------------------------------------
// Generic reactive at-bat event
//
// This is intentionally a plain serializable data class rather than an
// interface. Unity can display and edit this directly in the Inspector.
//
// Conceptually:
//
// WHEN [trigger result]
// WITH [chance]
// DO [action] [amount] times
// -------------------------------------------------------------------------

[System.Serializable]
public class AtBatEventTrigger
{
    [Header("Trigger")]

    [Tooltip("At-bat results that can trigger this effect.")]
    public List<AtBatResult> triggerResults = new List<AtBatResult>();

    [Range(0f, 1f)]
    [Tooltip("Chance that the effect activates when the trigger occurs.")]
    public float chance = 1f;

    [Header("Action")]

    public AtBatEventAction action;

    [Min(1)]
    public int amount = 1;
}


// -------------------------------------------------------------------------
// Concrete chart effects
// -------------------------------------------------------------------------

[System.Serializable]
public class ResultRemapEffect : IChartEffect
{
    public AtBatResult fromResult;
    public AtBatResult toResult;

    public List<ChartEntry> Apply(List<ChartEntry> chart)
    {
        var result = new List<ChartEntry>();

        foreach (var entry in chart)
        {
            result.Add(new ChartEntry
            {
                rollMin = entry.rollMin,
                rollMax = entry.rollMax,
                result = entry.result == fromResult
                    ? toResult
                    : entry.result
            });
        }

        return result;
    }
}


[System.Serializable]
public class ChartShiftEffect : IChartEffect
{
    public int shiftAmount;

    private static readonly AtBatResult[] Ladder =
    {
        AtBatResult.SO,
        AtBatResult.GB,
        AtBatResult.FB,
        AtBatResult.PU,
        AtBatResult.BB,
        AtBatResult.Single,
        AtBatResult.SinglePlus,
        AtBatResult.Double,
        AtBatResult.Triple,
        AtBatResult.HR
    };

    public List<ChartEntry> Apply(List<ChartEntry> chart)
    {
        var perRoll = new AtBatResult[21];

        foreach (var entry in chart)
        {
            for (int r = entry.rollMin; r <= entry.rollMax; r++)
                perRoll[r] = entry.result;
        }

        AtBatResult currentBest = perRoll[20];

        int bestIdx = System.Array.IndexOf(
            Ladder,
            currentBest);

        AtBatResult fillResult =
            bestIdx < Ladder.Length - 1
                ? Ladder[bestIdx + 1]
                : currentBest;

        var shifted = new AtBatResult[21];

        for (int r = 1; r <= 20; r++)
        {
            int sourceRoll = r + shiftAmount;

            shifted[r] =
                sourceRoll <= 20
                    ? perRoll[sourceRoll]
                    : fillResult;
        }

        var result = new List<ChartEntry>();

        int rangeStart = 1;

        for (int r = 2; r <= 21; r++)
        {
            if (r == 21 ||
                shifted[r] != shifted[rangeStart])
            {
                result.Add(new ChartEntry
                {
                    rollMin = rangeStart,
                    rollMax = r - 1,
                    result = shifted[rangeStart]
                });

                rangeStart = r;
            }
        }

        return result;
    }
}


// -------------------------------------------------------------------------
// Concrete roll modifier effects
// -------------------------------------------------------------------------

[System.Serializable]
public class FlatRollModifierEffect : IRollModifierEffect
{
    public int amount;

    public int ModifyRoll(int rawRoll)
    {
        return rawRoll + amount;
    }
}


// -------------------------------------------------------------------------
// Concrete result effects
// -------------------------------------------------------------------------

[System.Serializable]
public class ResultOverrideEffect : IResultEffect
{
    public AtBatResult fromResult;
    public AtBatResult toResult;

    public AtBatResult Apply(AtBatResult result)
    {
        return result == fromResult
            ? toResult
            : result;
    }
}


[System.Serializable]
public class CritChanceEffect : IResultEffect
{
    [Range(0, 1)]
    public float critChance;

    public int rungsToBump = 1;

    private static readonly AtBatResult[] Ladder =
    {
        AtBatResult.SO,
        AtBatResult.GB,
        AtBatResult.FB,
        AtBatResult.PU,
        AtBatResult.BB,
        AtBatResult.Single,
        AtBatResult.SinglePlus,
        AtBatResult.Double,
        AtBatResult.Triple,
        AtBatResult.HR
    };

    public AtBatResult Apply(AtBatResult result)
    {
        if (Random.value > critChance)
            return result;

        int idx = System.Array.IndexOf(
            Ladder,
            result);

        int newIdx = Mathf.Min(
            idx + rungsToBump,
            Ladder.Length - 1);

        return Ladder[newIdx];
    }
}


// -------------------------------------------------------------------------
// Concrete stat modifier effects
// -------------------------------------------------------------------------

[System.Serializable]
public class OnBaseModifierEffect : IStatModifierEffect
{
    public int amount;

    public int ModifyOnBase(int rawOnBase)
    {
        return rawOnBase + amount;
    }
}


// -------------------------------------------------------------------------
// UpgradeCard
// -------------------------------------------------------------------------

[CreateAssetMenu(
    fileName = "NewUpgradeCard",
    menuName = "Showdown/Upgrade Card")]
public class UpgradeCard : ScriptableObject
{
    [Header("Identity")]

    public string cardName;
    public string flavorText;
    public int cost = 2;


    [Header("Scope & Targeting")]

    public UpgradeScope scope;
    public UpgradeTargetType targetType;


    [Header("Legacy Effect (still supported)")]

    [Tooltip("Each swap moves 'count' slots from one result to another on the chart.")]
    public List<SlotSwap> swaps = new List<SlotSwap>();


    [Header("New Effects")]

    [SerializeReference]
    public List<IChartEffect> chartEffects =
        new List<IChartEffect>();

    [SerializeReference]
    public List<IRollModifierEffect> rollModifierEffects =
        new List<IRollModifierEffect>();

    [SerializeReference]
    public List<IResultEffect> resultEffects =
        new List<IResultEffect>();

    [SerializeReference]
    public List<IStatModifierEffect> statModifierEffects =
        new List<IStatModifierEffect>();

    // IMPORTANT:
    // This is now a normal serializable class instead of an interface.
    // Unity can therefore display its fields in the Inspector.
    public List<AtBatEventTrigger> atBatEventEffects =
        new List<AtBatEventTrigger>();


    // ---------------------------------------------------------------------
    // Effect description
    // ---------------------------------------------------------------------

    public string GetEffectDescription()
    {
        var lines = new List<string>();

        // Legacy swaps
        foreach (var swap in swaps)
        {
            lines.Add(
                $"+{swap.count} {swap.grow}, " +
                $"-{swap.count} {swap.shrink}");
        }


        // Chart effects
        foreach (var effect in chartEffects)
        {
            switch (effect)
            {
                case ResultRemapEffect remap:

                    lines.Add(
                        $"All {remap.fromResult} " +
                        $"become {remap.toResult}");

                    break;

                case ChartShiftEffect shift:

                    lines.Add(
                        $"Shift chart {shift.shiftAmount} " +
                        $"toward better outcomes");

                    break;
            }
        }


        // Roll effects
        foreach (var mod in rollModifierEffects)
        {
            if (mod is FlatRollModifierEffect flat)
            {
                lines.Add(
                    $"{(flat.amount >= 0 ? "+" : "")}" +
                    $"{flat.amount} to swing roll");
            }
        }


        // Result effects
        foreach (var effect in resultEffects)
        {
            switch (effect)
            {
                case ResultOverrideEffect over:

                    lines.Add(
                        $"{over.fromResult} " +
                        $"becomes {over.toResult}");

                    break;

                case CritChanceEffect crit:

                    lines.Add(
                        $"{crit.critChance:P0} chance to " +
                        $"upgrade result by " +
                        $"{crit.rungsToBump}");

                    break;
            }
        }


        // Stat effects
        foreach (var stat in statModifierEffects)
        {
            if (stat is OnBaseModifierEffect ob)
            {
                lines.Add(
                    $"{(ob.amount >= 0 ? "+" : "")}" +
                    $"{ob.amount} On Base");
            }
        }


        // Reactive at-bat effects
        foreach (var effect in atBatEventEffects)
        {
            string results =
                string.Join(
                    " / ",
                    effect.triggerResults);

            switch (effect.action)
            {
                case AtBatEventAction.AddShadowClone:

                    lines.Add(
                        $"On {results}: " +
                        $"{effect.chance:P0} chance to create " +
                        $"{effect.amount} Shadow Clone" +
                        $"{(effect.amount == 1 ? "" : "s")}");

                    break;
            }
        }


        return lines.Count > 0
            ? string.Join("\n", lines)
            : "No effect";
    }
}