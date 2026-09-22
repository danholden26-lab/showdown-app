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
