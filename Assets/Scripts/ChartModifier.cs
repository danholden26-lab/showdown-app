using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Stateless utility. Takes a card's chart and a list of upgrades,
/// returns a new chart with swaps applied. Never touches the original asset.
/// </summary>
public static class ChartModifier
{
    /// <summary>
    /// Clone <paramref name="original"/> and apply all upgrades that are
    /// relevant to <paramref name="batter"/> from <paramref name="upgrades"/>.
    /// </summary>
    public static List<ChartEntry> BuildChart(
    List<ChartEntry> original,
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        var chart = original.Select(e => new ChartEntry
        {
            rollMin = e.rollMin,
            rollMax = e.rollMax,
            result = e.result
        }).ToList();

        foreach (var pending in upgrades)
        {
            if (!pending.AppliesToBatter(batter)) continue;

            foreach (var swap in pending.card.swaps)
                ApplySwap(chart, swap);

            foreach (var effect in pending.card.chartEffects)
                chart = effect.Apply(chart);
        }

        return chart;
    }

    // -------------------------------------------------------------------------
    // Core swap logic
    // -------------------------------------------------------------------------

    private static void ApplySwap(List<ChartEntry> chart, SlotSwap swap)
    {
        // Count available slots in the shrink bucket
        int available = CountSlots(chart, swap.shrink);
        int toMove    = Mathf.Min(swap.count, available);

        if (toMove <= 0)
        {
            Debug.LogWarning($"[ChartModifier] No '{swap.shrink}' slots available to swap.");
            return;
        }

        // Steal 'toMove' slots from the high end of the shrink range(s)
        // and add them to the low end of the grow range(s)
        RemoveSlots(chart, swap.shrink, toMove);
        AddSlots(chart, swap.grow, toMove);

        // Re-sort so the chart stays in order for readability
        chart.Sort((a, b) => a.rollMin.CompareTo(b.rollMin));
    }

    private static int CountSlots(List<ChartEntry> chart, AtBatResult result)
    {
        int count = 0;
        foreach (var e in chart)
            if (e.result == result) count += (e.rollMax - e.rollMin + 1);
        return count;
    }

    /// <summary>Shrink entries of <paramref name="result"/> by <paramref name="count"/> slots from the top.</summary>
    private static void RemoveSlots(List<ChartEntry> chart, AtBatResult result, int count)
    {
        // Work from the highest rollMax downward
        var targets = chart.Where(e => e.result == result)
                           .OrderByDescending(e => e.rollMax)
                           .ToList();

        int remaining = count;
        foreach (var entry in targets)
        {
            if (remaining <= 0) break;

            int entrySize = entry.rollMax - entry.rollMin + 1;
            if (remaining >= entrySize)
            {
                remaining -= entrySize;
                chart.Remove(entry);
            }
            else
            {
                entry.rollMax -= remaining;
                remaining = 0;
            }
        }
    }

    /// <summary>Grow entries of <paramref name="result"/> by <paramref name="count"/> slots from the bottom.</summary>
    private static void AddSlots(List<ChartEntry> chart, AtBatResult result, int count)
    {
        // Find the lowest rollMin of the existing result, or the gap just below it
        var existing = chart.Where(e => e.result == result)
                            .OrderBy(e => e.rollMin)
                            .FirstOrDefault();

        if (existing != null)
        {
            // Extend downward if there's room (no overlap check — caller's chart should be valid)
            existing.rollMin -= count;
        }
        else
        {
            // Result doesn't exist on chart yet — find a free slot range
            // For simplicity, steal from the top of the highest entry
            var highest = chart.OrderByDescending(e => e.rollMax).FirstOrDefault();
            if (highest == null) return;

            int newMin = highest.rollMax - count + 1;
            int newMax = highest.rollMax;
            highest.rollMax = newMin - 1;

            chart.Add(new ChartEntry { rollMin = newMin, rollMax = newMax, result = result });
        }
    }
    public static List<IAtBatEventEffect> GetAtBatEventEffects(
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        var effects = new List<IAtBatEventEffect>();

        foreach (var upgrade in upgrades)
        {
            if (!upgrade.AppliesToBatter(batter))
                continue;

            if (upgrade.card.atBatEventEffects == null)
                continue;

            effects.AddRange(upgrade.card.atBatEventEffects);
        }

        return effects;
    }

    // -------------------------------------------------------------------------
    // Gather effects from other families — same "applies to this batter" filter
    // -------------------------------------------------------------------------

    public static List<IRollModifierEffect> GetRollModifiers(
        ShowdownCardData batter, List<PendingUpgrade> upgrades)
    {
        var result = new List<IRollModifierEffect>();
        foreach (var pending in upgrades)
        {
            if (!pending.AppliesToBatter(batter)) continue;
            result.AddRange(pending.card.rollModifierEffects);
        }
        return result;
    }

    public static List<IResultEffect> GetResultEffects(
        ShowdownCardData batter, List<PendingUpgrade> upgrades)
    {
        var result = new List<IResultEffect>();
        foreach (var pending in upgrades)
        {
            if (!pending.AppliesToBatter(batter)) continue;
            result.AddRange(pending.card.resultEffects);
        }
        return result;
    }

    public static int GetEffectiveOnBase(
        ShowdownCardData batter, List<PendingUpgrade> upgrades)
    {
        int onBase = batter.onBase;
        foreach (var pending in upgrades)
        {
            if (!pending.AppliesToBatter(batter)) continue;
            foreach (var mod in pending.card.statModifierEffects)
                onBase = mod.ModifyOnBase(onBase);
        }
        return onBase;
    }
}
