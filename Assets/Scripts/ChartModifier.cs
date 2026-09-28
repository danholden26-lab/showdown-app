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
    /// Clone the original chart and apply all relevant upgrades.
    /// </summary>
    public static List<ChartEntry> BuildChart(
        List<ChartEntry> original,
        ShowdownCardData batter,
        List<PendingUpgrade> upgrades)
    {
        var chart = original
            .Select(e => new ChartEntry
            {
                rollMin = e.rollMin,
                rollMax = e.rollMax,
                result = e.result
            })
            .ToList();

        foreach (var pending in upgrades)
        {
            if (!pending.AppliesToBatter(batter))
                continue;

            foreach (var swap in pending.card.swaps)
                ApplySwap(chart, swap);

            foreach (var effect in pending.card.chartEffects)
                chart = effect.Apply(chart);
        }

        return chart;
    }


    // ---------------------------------------------------------------------
    // Core swap logic
    // ---------------------------------------------------------------------

    private static void ApplySwap(
        List<ChartEntry> chart,
        SlotSwap swap)
    {
        int available = CountSlots(
            chart,
            swap.shrink);

        int toMove = Mathf.Min(
            swap.count,
            available);

        if (toMove <= 0)
        {
            Debug.LogWarning(
                $"[ChartModifier] No '{swap.shrink}' " +
                $"slots available to swap.");

            return;
        }

        RemoveSlots(
            chart,
            swap.shrink,
            toMove);

        AddSlots(
            chart,
            swap.grow,
            toMove);

        chart.Sort(
            (a, b) =>
                a.rollMin.CompareTo(b.rollMin));
    }


    private static int CountSlots(
        List<ChartEntry> chart,
        AtBatResult result)
    {
        int count = 0;

        foreach (var e in chart)
        {
            if (e.result == result)
            {
                count +=
                    e.rollMax -
                    e.rollMin +
                    1;
            }
        }

        return count;
    }


    private static void RemoveSlots(
        List<ChartEntry> chart,
        AtBatResult result,
        int count)
    {
        var targets = chart
            .Where(e => e.result == result)
            .OrderByDescending(e => e.rollMax)
            .ToList();

        int remaining = count;

        foreach (var entry in targets)
        {
            if (remaining <= 0)
                break;

            int entrySize =
                entry.rollMax -
                entry.rollMin +
                1;

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


    private static void AddSlots(
        List<ChartEntry> chart,
        AtBatResult result,
        int count)
    {
        var existing = chart
            .Where(e => e.result == result)
            .OrderBy(e => e.rollMin)
            .FirstOrDefault();

        if (existing != null)
        {
            existing.rollMin -= count;
        }
        else
        {
            var highest = chart
                .OrderByDescending(e => e.rollMax)
                .FirstOrDefault();

            if (highest == null)
                return;

            int newMin =
                highest.rollMax -
                count +
                1;

            int newMax =
                highest.rollMax;

            highest.rollMax =
                newMin - 1;

            chart.Add(
                new ChartEntry
                {
                    rollMin = newMin,
                    rollMax = newMax,
                    result = result
                });
        }
    }


    // ---------------------------------------------------------------------
    // Reactive at-bat events
    // ---------------------------------------------------------------------

    public static List<AtBatEventTrigger> GetAtBatEventEffects(
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        var result = new List<AtBatEventTrigger>();

        if (upgrades == null)
            return result;

        foreach (var pending in upgrades)
        {
            if (pending == null || pending.card == null)
                continue;

            if (!pending.AppliesToBatter(batter))
                continue;

            if (pending.card.atBatEventEffects == null)
                continue;

            foreach (var effect in pending.card.atBatEventEffects)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }

        return result;
    }


    // ---------------------------------------------------------------------
    // Roll modifiers
    // ---------------------------------------------------------------------

    public static List<IRollModifierEffect> GetRollModifiers(
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        var result = new List<IRollModifierEffect>();

        if (upgrades == null)
            return result;

        foreach (var pending in upgrades)
        {
            if (pending == null || pending.card == null)
                continue;

            if (!pending.AppliesToBatter(batter))
                continue;

            if (pending.card.rollModifierEffects == null)
                continue;

            foreach (var effect in pending.card.rollModifierEffects)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }

        return result;
    }


    // ---------------------------------------------------------------------
    // Result effects
    // ---------------------------------------------------------------------

    public static List<IResultEffect> GetResultEffects(
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        var result = new List<IResultEffect>();

        if (upgrades == null)
            return result;

        foreach (var pending in upgrades)
        {
            if (pending == null || pending.card == null)
                continue;

            if (!pending.AppliesToBatter(batter))
                continue;

            if (pending.card.resultEffects == null)
                continue;

            foreach (var effect in pending.card.resultEffects)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }

        return result;
    }


    // ---------------------------------------------------------------------
    // On Base modifiers
    // ---------------------------------------------------------------------

    public static int GetEffectiveOnBase(
    ShowdownCardData batter,
    List<PendingUpgrade> upgrades)
    {
        int onBase = batter.onBase;

        if (upgrades == null)
            return onBase;

        foreach (var pending in upgrades)
        {
            if (pending == null || pending.card == null)
                continue;

            if (!pending.AppliesToBatter(batter))
                continue;

            if (pending.card.statModifierEffects == null)
                continue;

            foreach (var mod in pending.card.statModifierEffects)
            {
                if (mod == null)
                    continue;

                onBase = mod.ModifyOnBase(onBase);
            }
        }

        return onBase;
    }
}