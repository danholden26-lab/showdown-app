using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Draft screen. Presents draftChoices cards per round for
/// config.rosterSize rounds, then transitions to the Game scene.
///
/// Scene hierarchy:
///   Canvas
///   └── DraftPanel
///       ├── RoundText          (TextMeshProUGUI) "Pick 1 of 9"
///       ├── RosterText         (TextMeshProUGUI) shows current roster names
///       └── CardChoicesGroup   (Horizontal Layout Group)
///           └── [CardChoiceUI x4] — instantiated at runtime from cardChoicePrefab
///
/// CardChoicePrefab needs:
///   ├── CardArtImage   (Image)
///   ├── NameText       (TextMeshProUGUI)
///   ├── StatsText      (TextMeshProUGUI)
///   └── PickButton     (Button)
/// </summary>
public class DraftManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI rosterText;
    public Transform       cardChoicesGroup;
    public GameObject      cardChoicePrefab;

    [Header("Dev Tools")]
    [Tooltip("Lets you pick which boss to test against during draft")]
    public TMP_Dropdown bossDropdown;

    // Runtime
    private GameManager          gm;
    private GameConfig           config;
    private List<ShowdownCardData> remainingPool;
    private int                  currentRound = 0;
    private List<DraftTier>      schedule;



    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Start()
    {
        gm     = GameManager.Instance;
        config = gm.config;

        // Clone pool so we don't mutate the original list
        remainingPool = new List<ShowdownCardData>(gm.batterPool);
        schedule = config.BuildDraftSchedule();
        LogPoolSummary();
        SetupBossDropdown();

        PresentRound();
    }

      
    // -------------------------------------------------------------------------
    // Round flow
    // -------------------------------------------------------------------------

    private void PresentRound()
    {
        currentRound++;

        DraftTier tier = CurrentTier();
        bool isCaptainPick = config.captainPickFirst && currentRound == 1 && tier == DraftTier.Epic;
        string tierLabel = isCaptainPick ? "Captain Pick" : $"{tier} Tier";
        roundText.text = $"Pick {currentRound} of {config.rosterSize}  -  {tierLabel}";
        UpdateRosterText();

        // Clear previous choices
        foreach (Transform child in cardChoicesGroup)
            Destroy(child.gameObject);

        // Pick random cards from pool
        var choices = DrawChoices(tier, config.draftChoices);

        foreach (var card in choices)
        {
            var go = Instantiate(cardChoicePrefab, cardChoicesGroup);
            Debug.Log($"Instantiated: {go.name}");

            var nameT = go.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
            var statsT = go.transform.Find("StatsText")?.GetComponent<TextMeshProUGUI>();
            var btn = go.transform.Find("PickButton")?.GetComponent<Button>();

            Debug.Log($"NameText found: {nameT != null}, StatsText found: {statsT != null}, Button found: {btn != null}");

            if (nameT)
            {
                nameT.text = card.playerName;
                Debug.Log($"Set name to: {card.playerName}");
            }
            if (statsT)
            {
                DraftTier cardTier = config.GetTier(card.points);
                statsT.text = $"<color={TierHex(cardTier)}>{cardTier.ToString().ToUpper()}</color>  {card.points} pts\nOB: {card.onBase}";
                Debug.Log($"Set stats for {card.playerName}: {cardTier}, {card.points} pts, OB {card.onBase}");
            }

            var captured = card;
            btn?.onClick.AddListener(() => OnCardPicked(captured));
        }
    }

    private void OnCardPicked(ShowdownCardData card)
    {
        Debug.Log($"OnCardPicked fired: {card.playerName}");
        bool added = gm.CurrentRun.AddPlayer(card, config.rosterSize);
        if (!added) return;

        // Remove picked card from pool so it can't appear again
        remainingPool.Remove(card);

        Debug.Log($"[Draft] Round {currentRound}: Picked {card.playerName}. " +
                  $"Roster: {gm.CurrentRun.roster.Count}/{config.rosterSize}");

        if (gm.CurrentRun.RosterFull(config.rosterSize))
        {
            Debug.Log("[Draft] Roster full. Heading to game.");
            gm.GoToGame();
        }
        else
        {
            PresentRound();
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private DraftTier CurrentTier()
    {
        if (schedule == null || schedule.Count == 0) return DraftTier.Mid;
        return schedule[Mathf.Clamp(currentRound - 1, 0, schedule.Count - 1)];
    }

    private static string TierHex(DraftTier tier)
    {
        switch (tier)
        {
            case DraftTier.Epic: return "#FFC933";   // gold
            case DraftTier.Mid:  return "#6CB8FF";   // blue
            default:             return "#B8B8B8";   // grey
        }
    }

    /// <summary>
    /// Draws up to `count` cards from the requested tier. If that tier has run
    /// short, fills the rest from the nearest tiers (lower tier wins ties).
    /// Unpicked cards stay in the pool, so offers can repeat until picked.
    /// </summary>
    private List<ShowdownCardData> DrawChoices(DraftTier tier, int count)
    {
        var choices = new List<ShowdownCardData>();

        var order = new List<DraftTier> { DraftTier.Low, DraftTier.Mid, DraftTier.Epic };
        order.Sort((a, b) =>
        {
            int da = Mathf.Abs((int)a - (int)tier);
            int db = Mathf.Abs((int)b - (int)tier);
            return da != db ? da.CompareTo(db) : ((int)a).CompareTo((int)b);
        });

        foreach (var t in order)
        {
            var candidates = new List<ShowdownCardData>();
            foreach (var c in remainingPool)
                if (config.GetTier(c.points) == t) candidates.Add(c);

            while (choices.Count < count && candidates.Count > 0)
            {
                int idx = Random.Range(0, candidates.Count);
                choices.Add(candidates[idx]);
                candidates.RemoveAt(idx);
            }

            if (t == tier && choices.Count < count)
                Debug.LogWarning($"[Draft] Only {choices.Count} {tier} card(s) left in the pool " +
                                 $"(wanted {count}). Filling from neighbouring tiers.");

            if (choices.Count >= count) break;
        }

        return choices;
    }

    /// <summary>Logs pool size per tier and warns when a tier is too small to fill every offer.</summary>
    private void LogPoolSummary()
    {
        int epic = 0, mid = 0, low = 0, unrated = 0;
        foreach (var c in remainingPool)
        {
            if (c.points <= 0) unrated++;
            switch (config.GetTier(c.points))
            {
                case DraftTier.Epic: epic++; break;
                case DraftTier.Mid:  mid++;  break;
                default:             low++;  break;
            }
        }

        Debug.Log($"[Draft] Pool: {epic} Epic / {mid} Mid / {low} Low. Schedule: {string.Join(", ", schedule)}");

        if (unrated > 0)
            Debug.LogWarning($"[Draft] {unrated} batter(s) have 0 points and count as Low. Set points on their cards.");

        // To fill every offer without borrowing, a tier needs (picks from it) + (choices per offer) - 1 cards.
        WarnIfThin(DraftTier.Epic, epic, config.epicPicks);
        WarnIfThin(DraftTier.Mid,  mid,  config.midPicks);
        WarnIfThin(DraftTier.Low,  low,  config.lowPicks);
    }

    private void WarnIfThin(DraftTier tier, int have, int picks)
    {
        if (picks <= 0) return;
        int need = picks + config.draftChoices - 1;
        if (have < need)
            Debug.LogWarning($"[Draft] {tier} tier has {have} card(s) but needs at least {need} " +
                             $"({picks} picks x {config.draftChoices} choices) to fill every offer without borrowing.");
    }

    private void UpdateRosterText()
    {
        if (gm.CurrentRun.roster.Count == 0)
        {
            rosterText.text = "Roster: empty";
            return;
        }
        var names = new System.Text.StringBuilder("Roster:\n");
        int totalPoints = 0;
        foreach (var p in gm.CurrentRun.roster)
        {
            names.AppendLine($"  {p.playerName}  ({p.points})");
            totalPoints += p.points;
        }
        names.AppendLine($"Team total: {totalPoints} pts");
        rosterText.text = names.ToString();
    }

    private void SetupBossDropdown()
    {
        if (bossDropdown == null) return; // dropdown not wired in scene yet, skip safely

        bossDropdown.ClearOptions();

        var options = new List<string>();
        foreach (var boss in gm.bossPitchers)
            options.Add(boss.playerName);

        bossDropdown.AddOptions(options);

        // Reflect whatever boss is already selected (defaults to index 0 on a fresh run)
        bossDropdown.value = gm.CurrentRun.currentBossIndex;
        bossDropdown.RefreshShownValue();

        bossDropdown.onValueChanged.AddListener(OnBossSelected);
    }

    private void OnBossSelected(int index)
    {
        gm.CurrentRun.currentBossIndex = index;
        Debug.Log($"[Draft] Boss selection changed to: {gm.bossPitchers[index].playerName}");
    }
}
