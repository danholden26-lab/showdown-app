using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Core at-bat resolution. Stateless — receives a chart and returns an outcome.
/// InningManager owns the game loop; this just rolls dice and looks up results.
/// </summary>
public class AtBatSimulator : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Primary entry point — uses a pre-built (possibly modified) chart
    // -------------------------------------------------------------------------

    public AtBatOutcome SimulateAtBatWithChart(
    ShowdownCardData pitcher,
    ShowdownCardData batter,
    List<ChartEntry> chart,
    int effectiveOnBase,
    List<IRollModifierEffect> rollModifiers = null)
    {
        int pitchRoll = RollD20();
        int pitchTotal = pitchRoll + pitcher.control;
        bool pitcherAdv = pitchTotal >= effectiveOnBase;

        List<ChartEntry> resolveChart = pitcherAdv ? pitcher.chart : chart;
        string chartOwner = pitcherAdv ? pitcher.playerName : batter.playerName;

        int rawSwingRoll = RollD20();
        int swingRoll = ApplyRollModifiers(rawSwingRoll, rollModifiers);

        AtBatResult result = LookupResult(resolveChart, swingRoll, chartOwner);

        return new AtBatOutcome
        {
            pitchRoll = pitchRoll,
            pitchTotal = pitchTotal,
            pitcherHadAdvantage = pitcherAdv,
            advantageCardName = chartOwner,
            rawSwingRoll = rawSwingRoll,
            swingRoll = swingRoll,
            result = result
        };
    }

    private int ApplyRollModifiers(int rawRoll, List<IRollModifierEffect> modifiers)
    {
        if (modifiers == null) return rawRoll;
        int modified = rawRoll;
        foreach (var mod in modifiers)
            modified = mod.ModifyRoll(modified);
        return Mathf.Clamp(modified, 1, 20);
    }

    // -------------------------------------------------------------------------
    // Convenience overload — no upgrades, uses raw card charts
    // -------------------------------------------------------------------------

    public AtBatOutcome SimulateAtBat(ShowdownCardData pitcher, ShowdownCardData batter)
    {
        return SimulateAtBatWithChart(pitcher, batter, batter.chart, batter.onBase);
    }

    // -------------------------------------------------------------------------
    // Stealing (MLB Showdown rules)
    //
    // Defender rolls d20 + catcher fielding (+ a flat bonus when the runner is
    // going for 3rd). If that total is GREATER than the runner's speed, the
    // runner is thrown out. Equal or lower and he is safe.
    // -------------------------------------------------------------------------

    public StealOutcome SimulateSteal(
        int runnerSpeed,
        int catcherFielding,
        int targetBase,
        int stealThirdBonus)
    {
        int defenseValue = DefenseValue(catcherFielding, targetBase, stealThirdBonus);
        int roll = RollD20();
        int total = roll + defenseValue;

        return new StealOutcome
        {
            targetBase = targetBase,
            runnerSpeed = runnerSpeed,
            defenseRoll = roll,
            defenseValue = defenseValue,
            defenseTotal = total,
            safe = total <= runnerSpeed
        };
    }

    /// <summary>Probability (0-1) that the runner is safe. Used to show odds in the prompt.</summary>
    public static float StealSafeChance(
        int runnerSpeed,
        int catcherFielding,
        int targetBase,
        int stealThirdBonus)
    {
        int defenseValue = DefenseValue(catcherFielding, targetBase, stealThirdBonus);

        // Safe when d20 + defenseValue <= speed  ->  d20 <= speed - defenseValue
        int safeRolls = Mathf.Clamp(runnerSpeed - defenseValue, 0, 20);
        return safeRolls / 20f;
    }

    private static int DefenseValue(int catcherFielding, int targetBase, int stealThirdBonus)
        => catcherFielding + (targetBase == 3 ? stealThirdBonus : 0);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private int RollD20() => Random.Range(1, 21);

    private AtBatResult LookupResult(List<ChartEntry> chart, int roll, string ownerName)
    {
        foreach (var entry in chart)
            if (roll >= entry.rollMin && roll <= entry.rollMax)
                return entry.result;

        Debug.LogWarning($"[Showdown] No chart entry for roll {roll} on {ownerName}. Defaulting to GB.");
        return AtBatResult.GB;
    }
}

// -------------------------------------------------------------------------
// Shared return type
// -------------------------------------------------------------------------

public struct AtBatOutcome
{
    public int pitchRoll;
    public int pitchTotal;
    public bool pitcherHadAdvantage;
    public string advantageCardName;
    public int rawSwingRoll;
    public int swingRoll;
    public AtBatResult result;
}

public struct StealOutcome
{
    public int targetBase;
    public int runnerSpeed;
    public int defenseRoll;
    public int defenseValue;   // catcher fielding (+ 3rd-base bonus)
    public int defenseTotal;   // roll + defenseValue
    public bool safe;
}
