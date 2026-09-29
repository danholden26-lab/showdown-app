using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tracks live game state: bases, outs, score, inning, lineup position,
/// and all active upgrades across game / inning / at-bat scopes.
/// </summary>
public class GameState
{
    // --- Inning / lineup ---
    public int CurrentInning { get; private set; } = 1;
    public int CurrentBatterIndex { get; private set; } = 0;

    // --- Count ---
    public int Outs { get; private set; } = 0;

    // --- Bases (runners are tracked by card so steals can read their speed) ---
    public ShowdownCardData FirstRunner { get; private set; }
    public ShowdownCardData SecondRunner { get; private set; }
    public ShowdownCardData ThirdRunner { get; private set; }

    public bool First => FirstRunner != null;
    public bool Second => SecondRunner != null;
    public bool Third => ThirdRunner != null;

    // --- Score ---
    public int RunsThisInning { get; private set; } = 0;
    public int TotalRuns { get; private set; } = 0;

    // --- Gold ---
    public int Gold { get; private set; } = 0;

    public bool HalfInningOver => Outs >= 3;

    // --- Upgrades ---
    private List<PendingUpgrade> gameUpgrades = new List<PendingUpgrade>();
    private List<PendingUpgrade> inningUpgrades = new List<PendingUpgrade>();
    private List<PendingUpgrade> atBatUpgrades = new List<PendingUpgrade>();


    /// <summary>Every upgrade owned this game, whether or not its condition is currently met.</summary>
    public IEnumerable<PendingUpgrade> AllUpgrades =>
        gameUpgrades.Concat(inningUpgrades).Concat(atBatUpgrades);

    /// <summary>Upgrades in effect right now. Conditional ones only count while their condition is true.</summary>
    public IEnumerable<PendingUpgrade> ActiveUpgrades =>
        AllUpgrades.Where(u => u == null || u.card == null ||
                               u.card.condition == null || u.card.condition.IsMet(this));

    private Dictionary<ShowdownCardData, int> shadowClones =
    new Dictionary<ShowdownCardData, int>();

    public void AddShadowClone(ShowdownCardData batter)
    {
        if (!shadowClones.ContainsKey(batter))
            shadowClones[batter] = 0;

        shadowClones[batter]++;

        Debug.Log(
            $"[Shadow] {batter.playerName} now has " +
            $"{shadowClones[batter]} Shadow Clone(s).");
    }

    public int GetShadowClones(ShowdownCardData batter)
    {
        return shadowClones.TryGetValue(batter, out int count)
            ? count
            : 0;
    }
    // -------------------------------------------------------------------------
    // Upgrade management
    // -------------------------------------------------------------------------

    public void PlayUpgrade(PendingUpgrade upgrade)
    {
        switch (upgrade.card.scope)
        {
            case UpgradeScope.Game: gameUpgrades.Add(upgrade); break;
            case UpgradeScope.Inning: inningUpgrades.Add(upgrade); break;
            case UpgradeScope.AtBat: atBatUpgrades.Add(upgrade); break;
        }

        Debug.Log($"[Upgrade] '{upgrade.card.cardName}' played " +
                  $"({upgrade.card.scope} / {upgrade.card.targetType}" +
                  $"{(upgrade.targetBatter != null ? " -> " + upgrade.targetBatter.playerName : "")})");
    }

    public void ConsumeAtBatUpgrades() => atBatUpgrades.Clear();
    public void ConsumeInningUpgrades() => inningUpgrades.Clear();

    // -------------------------------------------------------------------------
    // Inning transition
    // -------------------------------------------------------------------------

    public void StartNewInning()
    {
        int earned = 3 + RunsThisInning;
        Gold += earned;
        Debug.Log($"[Economy] Inning {CurrentInning} ended. " +
                  $"Earned {earned} gold (3 base + {RunsThisInning} runs). Total: {Gold}");

        TotalRuns += RunsThisInning;
        RunsThisInning = 0;
        CurrentInning++;
        Outs = 0;
        ClearBases();

        ConsumeInningUpgrades();
    }

    // -------------------------------------------------------------------------
    // Gold
    // -------------------------------------------------------------------------

    public bool TrySpend(int amount)
    {
        if (Gold < amount)
        {
            Debug.LogWarning($"[Economy] Not enough gold. Have {Gold}, need {amount}.");
            return false;
        }
        Gold -= amount;
        return true;
    }

    // -------------------------------------------------------------------------
    // Lineup
    // -------------------------------------------------------------------------

    public void AdvanceBatterIndex() => CurrentBatterIndex++;

    // -------------------------------------------------------------------------
    // At-bat result application
    // -------------------------------------------------------------------------

    public void ApplyResult(AtBatResult result, ShowdownCardData batter)
    {
        switch (result)
        {
            case AtBatResult.SO:
            case AtBatResult.GB:
            case AtBatResult.FB:
            case AtBatResult.PU:
                RecordOut();
                break;

            case AtBatResult.BB:
                WalkBatter(batter);
                break;

            case AtBatResult.Single:
            case AtBatResult.SinglePlus:
                AdvanceAllRunners(1);
                FirstRunner = batter;
                break;

            case AtBatResult.Double:
                AdvanceAllRunners(2);
                SecondRunner = batter;
                break;

            case AtBatResult.Triple:
                AdvanceAllRunners(3);
                ThirdRunner = batter;
                break;

            case AtBatResult.HR:
                if (ThirdRunner != null) RunsThisInning++;
                if (SecondRunner != null) RunsThisInning++;
                if (FirstRunner != null) RunsThisInning++;
                RunsThisInning++;
                ClearBases();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Stealing
    // -------------------------------------------------------------------------

    /// <summary>Runner on the given base (1-3), or null.</summary>
    public ShowdownCardData GetRunner(int baseNumber)
    {
        switch (baseNumber)
        {
            case 1: return FirstRunner;
            case 2: return SecondRunner;
            case 3: return ThirdRunner;
            default: return null;
        }
    }

    /// <summary>A steal is possible from 1st or 2nd when the next base is open. No stealing home.</summary>
    public bool CanSteal(int fromBase)
    {
        switch (fromBase)
        {
            case 1: return FirstRunner != null && SecondRunner == null;
            case 2: return SecondRunner != null && ThirdRunner == null;
            default: return false;
        }
    }

    public void ApplyStealSuccess(int fromBase)
    {
        if (!CanSteal(fromBase)) return;

        if (fromBase == 1) { SecondRunner = FirstRunner; FirstRunner = null; }
        else               { ThirdRunner = SecondRunner; SecondRunner = null; }
    }

    public void ApplyCaughtStealing(int fromBase)
    {
        if (fromBase == 1) FirstRunner = null;
        else if (fromBase == 2) SecondRunner = null;

        RecordOut();
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void ClearBases()
    {
        FirstRunner = SecondRunner = ThirdRunner = null;
    }

    private void RecordOut()
    {
        Outs++;
        if (HalfInningOver) ClearBases();
    }

    private void AdvanceAllRunners(int bases)
    {
        var third = ThirdRunner;
        var second = SecondRunner;
        var first = FirstRunner;

        ClearBases();

        PlaceRunner(third, 3 + bases);
        PlaceRunner(second, 2 + bases);
        PlaceRunner(first, 1 + bases);
    }

    private void PlaceRunner(ShowdownCardData runner, int pos)
    {
        if (runner == null) return;
        if (pos >= 4) { RunsThisInning++; return; }
        if (pos == 3) ThirdRunner = runner;
        if (pos == 2) SecondRunner = runner;
        if (pos == 1) FirstRunner = runner;
    }

    private void WalkBatter(ShowdownCardData batter)
    {
        bool first = FirstRunner != null;
        bool second = SecondRunner != null;
        bool third = ThirdRunner != null;

        if (first && second && third) RunsThisInning++;   // bases loaded: runner from 3rd scores
        if (first && second) ThirdRunner = SecondRunner;
        if (first) SecondRunner = FirstRunner;
        FirstRunner = batter;
    }

    // -------------------------------------------------------------------------
    // Display
    // -------------------------------------------------------------------------

    public string BasesString()
    {
        string f = First ? "1B" : "__";
        string s = Second ? "2B" : "__";
        string t = Third ? "3B" : "__";
        return $"[{t} {s} {f}]";
    }

    public override string ToString() =>
        $"Inning: {CurrentInning} | Outs: {Outs}/3 | Bases: {BasesString()} | " +
        $"Runs: {RunsThisInning} (Total: {TotalRuns}) | Gold: {Gold}";
}
