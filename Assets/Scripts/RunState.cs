using System.Collections.Generic;
using UnityEngine;

public enum GameMode { BossRush, Classic, Multiplayer }

/// <summary>
/// All data that persists for the duration of a single run.
/// Owned by GameManager and survives scene loads.
/// </summary>
public class RunState
{
    public GameMode mode;
    public List<ShowdownCardData> roster = new List<ShowdownCardData>();
    public ShowdownCardData       activePitcher;  // Classic/Multiplayer only
    public int                    gold = 0;
    public int                    currentBossIndex = 0;
    public int                    totalRunsScored = 0;

    public bool RosterFull(int rosterSize) => roster.Count >= rosterSize;

    public bool HasPlayer(ShowdownCardData card) => roster.Contains(card);



    /// <summary>Add a player to the roster. Returns false if full.</summary>
    public bool AddPlayer(ShowdownCardData card, int rosterSize)
    {
        if (RosterFull(rosterSize))
        {
            Debug.LogWarning($"[RunState] Roster is full ({rosterSize} players).");
            return false;
        }
        roster.Add(card);
        return true;
    }

    /// <summary>Sell a player, returning gold based on config sell rate.</summary>
    public bool SellPlayer(ShowdownCardData card, GameConfig config)
    {
        if (!roster.Contains(card))
        {
            Debug.LogWarning($"[RunState] {card.playerName} is not on the roster.");
            return false;
        }
        int sellPrice = config.GetSellPrice(card.cost);
        roster.Remove(card);
        gold += sellPrice;
        Debug.Log($"[RunState] Sold {card.playerName} for {sellPrice}g. Gold: {gold}");
        return true;
    }

    public TeamData BuildTeamData(string teamName)
    {
        var team = ScriptableObject.CreateInstance<TeamData>();
        team.teamName     = teamName;
        team.battingOrder = new List<ShowdownCardData>(roster);
        return team;
    }
}
