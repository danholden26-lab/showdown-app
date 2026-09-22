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



    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Start()
    {
        gm     = GameManager.Instance;
        config = gm.config;

        // Clone pool so we don't mutate the original list
        remainingPool = new List<ShowdownCardData>(gm.batterPool);
        SetupBossDropdown();

        PresentRound();
    }

      
    // -------------------------------------------------------------------------
    // Round flow
    // -------------------------------------------------------------------------

    private void PresentRound()
    {
        currentRound++;
        roundText.text = $"Pick {currentRound} of {config.rosterSize}";
        UpdateRosterText();

        // Clear previous choices
        foreach (Transform child in cardChoicesGroup)
            Destroy(child.gameObject);

        // Pick random cards from pool
        var choices = DrawChoices(config.draftChoices);

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
                statsT.text = $"OB: {card.onBase}";
                Debug.Log($"Set stats to: OB {card.onBase}");
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

    private List<ShowdownCardData> DrawChoices(int count)
    {
        var pool    = new List<ShowdownCardData>(remainingPool);
        var choices = new List<ShowdownCardData>();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            choices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }

        return choices;
    }

    private void UpdateRosterText()
    {
        if (gm.CurrentRun.roster.Count == 0)
        {
            rosterText.text = "Roster: empty";
            return;
        }
        var names = new System.Text.StringBuilder("Roster:\n");
        foreach (var p in gm.CurrentRun.roster)
            names.AppendLine($"  {p.playerName}");
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
