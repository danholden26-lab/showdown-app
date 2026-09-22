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

    // --- Bases ---
    public bool First { get; private set; } = false;
    public bool Second { get; private set; } = false;
    public bool Third { get; private set; } = false;

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

    public IEnumerable<PendingUpgrade> ActiveUpgrades =>
        gameUpgrades.Concat(inningUpgrades).Concat(atBatUpgrades);

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
        First = Second = Third = false;

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

    public void ApplyResult(AtBatResult result)
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
                WalkBatter();
                break;

            case AtBatResult.Single:
            case AtBatResult.SinglePlus:
                AdvanceAllRunners(1);
                First = true;
                break;

            case AtBatResult.Double:
                AdvanceAllRunners(2);
                Second = true;
                break;

            case AtBatResult.Triple:
                AdvanceAllRunners(3);
                Third = true;
                break;

            case AtBatResult.HR:
                if (Third) { RunsThisInning++; Third = false; }
                if (Second) { RunsThisInning++; Second = false; }
                if (First) { RunsThisInning++; First = false; }
                RunsThisInning++;
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void RecordOut()
    {
        Outs++;
        if (HalfInningOver) First = Second = Third = false;
    }

    private void AdvanceAllRunners(int bases)
    {
        int thirdPos = Third ? 3 + bases : 0;
        int secondPos = Second ? 2 + bases : 0;
        int firstPos = First ? 1 + bases : 0;

        First = Second = Third = false;

        PlaceRunner(thirdPos);
        PlaceRunner(secondPos);
        PlaceRunner(firstPos);
    }

    private void PlaceRunner(int pos)
    {
        if (pos == 0) return;
        if (pos >= 4) { RunsThisInning++; return; }
        if (pos == 3) Third = true;
        if (pos == 2) Second = true;
        if (pos == 1) First = true;
    }

    private void WalkBatter()
    {
        if (First && Second && Third) RunsThisInning++;
        else if (First && Second) Third = true;
        else if (First) Second = true;
        First = true;
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
