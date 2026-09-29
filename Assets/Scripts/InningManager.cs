using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Drives the game loop: lineup phase -> (sim -> result -> shop) per inning.
/// Attach to your GameManager GameObject alongside AtBatSimulator.
/// </summary>
public class InningManager : MonoBehaviour
{
    // Resolved at runtime from GameManager — no Inspector assignment needed
    private TeamData battingTeam;
    private ShowdownCardData pitcherCard;
    private AtBatSimulator simulator;

    [Header("Shop")]
    [Tooltip("Pool of all upgrade cards available to appear in the shop")]
    public List<UpgradeCard> upgradeCardPool = new List<UpgradeCard>();
    [Tooltip("How many cards appear in the shop each inning")]
    public int shopSlots = 3;

    [Header("Stealing")]
    [Tooltip("Off: ask only about the batter who just reached base. On: ask about every eligible runner before each at-bat.")]
    public bool promptEveryAtBat = false;

    [Header("Settings")]
    public int totalInnings = 9;
    [Tooltip("Seconds to pause between each at-bat during the sim")]
    public float secondsPerAtBat = 1.0f;

    // Runtime
    private GameState state;
    private List<UpgradeCard> currentShop = new List<UpgradeCard>();
    private bool shopReady = false;
    private bool resultAcknowledged = false;
    private bool isFirstInning = true;
    private bool hasGameStarted = false;

    public List<PendingUpgrade> GetActiveUpgrades() => state?.ActiveUpgrades?.ToList() ?? new List<PendingUpgrade>();

    // -------------------------------------------------------------------------
    // Entry point
    // -------------------------------------------------------------------------

    private void Awake()
    {
        simulator = GetComponent<AtBatSimulator>();
        if (simulator == null)
            Debug.LogError("[InningManager] No AtBatSimulator found on this GameObject!");

        pitcherCard = GameManager.Instance.GetCurrentBoss();
        battingTeam = GameManager.Instance.CurrentRun.BuildTeamData("My Team"); // safe placeholder, order refreshed below
    }

    private void Start()
    {
        state = new GameState();
        StartCoroutine(RunGame());
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            SimInningNow();
    }

    // -------------------------------------------------------------------------
    // Public hooks for UI
    // -------------------------------------------------------------------------
    public void SimInningNow()
    {
        if (!hasGameStarted)
        {
            battingTeam = GameManager.Instance.CurrentRun.BuildTeamData("My Team");
            hasGameStarted = true;
        }

        shopReady = true;
    }

    public void AcknowledgeResult()
    {
        resultAcknowledged = true;
    }

    /// <summary>Returns the current shop card list for ShopUI to display.</summary>
    public List<UpgradeCard> GetCurrentShop() => currentShop;

    // -------------------------------------------------------------------------
    // Game loop
    // -------------------------------------------------------------------------

    private IEnumerator RunGame()
    {
        Debug.Log($"GAME START: {battingTeam.teamName} vs {pitcherCard.playerName}");

        // --- Lineup phase before game starts ---
        GameUI.Instance?.ShowPregame();
        shopReady = false;
        yield return new WaitUntil(() => shopReady);
        isFirstInning = false;

        for (int inning = 1; inning <= totalInnings; inning++)
        {
            yield return StartCoroutine(SimInning());

            resultAcknowledged = false;
            yield return new WaitUntil(() => resultAcknowledged);

            state.StartNewInning();
            GameManager.Instance.CurrentRun.gold = state.Gold;

            if (inning == totalInnings)
                break;

            yield return StartCoroutine(ShopPhase());
        }

        int finalScore = state.TotalRuns;
        bool won = finalScore >= pitcherCard.runsToBeat;

        GameManager.Instance.CurrentRun.totalRunsScored = finalScore;

        Debug.Log(won
            ? $"GAME OVER — WIN! Final score: {finalScore} runs (needed {pitcherCard.runsToBeat})"
            : $"GAME OVER — LOSS. Final score: {finalScore} runs (needed {pitcherCard.runsToBeat})");

        GameUI.Instance?.ShowGameOver(finalScore, won);
    }

    // -------------------------------------------------------------------------
    // Shop phase
    // -------------------------------------------------------------------------

    private IEnumerator ShopPhase()
    {
        RollShop();
        GameUI.Instance?.ShowShop();

        Debug.Log($"\n--- SHOP (Inning {state.CurrentInning}) | Gold: {state.Gold} ---");
        for (int i = 0; i < currentShop.Count; i++)
        {
            var card = currentShop[i];
            Debug.Log($"  [{i}] {card.cardName} ({card.cost}g) — " +
                      $"{card.scope} / {card.targetType} — {card.flavorText}");
        }
        Debug.Log("Call BuyUpgrade(shopIndex, targetBatter) to purchase. Press SPACE (or call SimInningNow()) when ready.\n");

        shopReady = false;
        yield return new WaitUntil(() => shopReady);
    }

    /// <summary>
    /// Called by UI (or test code) to purchase a card and immediately target it.
    /// Pass null for targetBatter on Team/Field cards.
    /// </summary>
    public bool BuyUpgrade(int shopIndex, ShowdownCardData targetBatter = null)
    {
        if (shopIndex < 0 || shopIndex >= currentShop.Count)
        {
            Debug.LogWarning($"[Shop] Invalid shop index {shopIndex}.");
            return false;
        }

        var card = currentShop[shopIndex];

        if (!state.TrySpend(card.cost)) return false;

        // Validate targeting
        if (card.targetType == UpgradeTargetType.Batter && targetBatter == null)
        {
            Debug.LogWarning($"[Shop] '{card.cardName}' requires a batter target.");
            return false;
        }

        var pending = new PendingUpgrade(card, targetBatter);
        state.PlayUpgrade(pending);

        currentShop.RemoveAt(shopIndex); // can't buy the same card twice

        // Sync gold back to RunState so ShopUI can read it
        GameManager.Instance.CurrentRun.gold = state.Gold;

        return true;
    }

    // -------------------------------------------------------------------------
    // Sim phase
    // -------------------------------------------------------------------------
    private void ResolveAtBatEvent(
    AtBatEventTrigger effect,
    AtBatOutcome outcome,
    ShowdownCardData batter,
    GameState state)
    {
        // Did the result match this event's trigger?
        if (!effect.triggerResults.Contains(outcome.result))
            return;

        // Did the random chance succeed?
        if (Random.value > effect.chance)
            return;

        switch (effect.action)
        {
            case AtBatEventAction.AddShadowClone:

                for (int i = 0; i < effect.amount; i++)
                {
                    state.AddShadowClone(batter);
                }

                Debug.Log(
                    $"[Artifact] Shadow Cleats triggered! " +
                    $"{batter.playerName} gained " +
                    $"{effect.amount} Shadow Clone(s). " +
                    $"Total: {state.GetShadowClones(batter)}");

                break;
        }
    }
    private IEnumerator SimInning()
    {
        GameUI.Instance?.ShowSim();
        GameUI.Instance?.ClearLog();
        Debug.Log($"\n=== INNING {state.CurrentInning} — {battingTeam.teamName} batting ===");

        int atBatNum = 0;

        while (!state.HalfInningOver)
        {
            atBatNum++;
            ShowdownCardData batter = battingTeam.GetBatter(state.CurrentBatterIndex);

            AnnounceLiveUpgrades();

            var activeUpgrades = new List<PendingUpgrade>(state.ActiveUpgrades);
            var modifiedChart = ChartModifier.BuildChart(batter.chart, batter, activeUpgrades);
            var rollModifiers = ChartModifier.GetRollModifiers(batter, activeUpgrades);
            var resultEffects = ChartModifier.GetResultEffects(batter, activeUpgrades);
            int effectiveOnBase = ChartModifier.GetEffectiveOnBase(batter, activeUpgrades);

            AtBatOutcome outcome = simulator.SimulateAtBatWithChart(
                pitcherCard, batter, modifiedChart, effectiveOnBase, rollModifiers);

            foreach (var effect in resultEffects)
                outcome.result = effect.Apply(outcome.result);

            int runsBefore = state.RunsThisInning;

            state.ApplyResult(outcome.result, batter);

            // Fire reactive artifact effects AFTER the baseball result resolves.
            var eventEffects = ChartModifier.GetAtBatEventEffects(
                batter,
                activeUpgrades);

            foreach (var effect in eventEffects)
            {
                ResolveAtBatEvent(
                    effect,
                    outcome,
                    batter,
                    state);
            }

            state.ConsumeAtBatUpgrades();

            int runsScored = state.RunsThisInning - runsBefore;
            LogAtBat(atBatNum, batter, outcome, runsScored,effectiveOnBase);

            // Update UI after each at-bat
            GameUI.Instance?.UpdateGameState(state, batter);

            state.AdvanceBatterIndex();

            yield return StartCoroutine(StealPhase(batter));

            yield return new WaitForSeconds(secondsPerAtBat);
        }

        Debug.Log($"--- Inning {state.CurrentInning} done: {state.RunsThisInning} run(s) ---\n");

        // Show result panel before shop
        int goldEarned = 3 + state.RunsThisInning;
        GameUI.Instance?.ShowResult(state.RunsThisInning, state.TotalRuns + state.RunsThisInning, goldEarned);
    }

    // -------------------------------------------------------------------------
    // Conditional upgrades
    // -------------------------------------------------------------------------

    // Conditional upgrades that have already announced themselves (so we log once, not every at-bat).
    private readonly HashSet<PendingUpgrade> liveUpgrades = new HashSet<PendingUpgrade>();

    /// <summary>
    /// Logs when a conditional upgrade switches on. Called at the start of each at-bat;
    /// the upgrade itself is applied through GameState.ActiveUpgrades.
    /// </summary>
    private void AnnounceLiveUpgrades()
    {
        foreach (var u in state.AllUpgrades)
        {
            if (u == null || u.card == null || u.card.condition == null) continue;
            if (!u.card.condition.AnnouncesActivation) continue;   // situational conditions aren't logged

            bool met = u.card.condition.IsMet(state);

            if (met && liveUpgrades.Add(u))
            {
                Debug.Log($"  [Upgrade] '{u.card.cardName}' is now active ({u.card.condition.Describe()})");
                GameUI.Instance?.AddLogEntry($"{u.card.cardName} is active!");
            }
            else if (!met)
            {
                liveUpgrades.Remove(u);   // re-arms for next time (e.g. next inning)
            }
        }
    }

    // -------------------------------------------------------------------------
    // Steal phase
    // -------------------------------------------------------------------------

    // Used only if GameManager has no GameConfig assigned.
    private const int FallbackCatcherFielding = 2;
    private const int FallbackStealThirdBonus = 5;

    private int CatcherFielding()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        int standard = cfg != null ? cfg.defaultCatcherFielding : FallbackCatcherFielding;
        return standard + pitcherCard.fieldingBonus;   // bosses with strong defense raise this
    }

    private int StealThirdBonus()
    {
        var cfg = GameManager.Instance != null ? GameManager.Instance.config : null;
        return cfg != null ? cfg.stealThirdDefenseBonus : FallbackStealThirdBonus;
    }

    /// <summary>
    /// Runs between at-bats. Asks the player whether to send each eligible runner,
    /// rolls the steal, and applies the result. Lead runner is handled first so a
    /// successful 2nd->3rd steal can open the bag for a 1st->2nd attempt.
    /// </summary>
    private IEnumerator StealPhase(ShowdownCardData justBatted)
    {
        if (GameUI.Instance == null) yield break;

        for (int fromBase = 2; fromBase >= 1; fromBase--)
        {
            if (state.HalfInningOver) yield break;
            if (!state.CanSteal(fromBase)) continue;

            ShowdownCardData runner = state.GetRunner(fromBase);

            // Default: only ask about the batter who just reached base.
            if (!promptEveryAtBat && runner != justBatted) continue;

            int targetBase = fromBase + 1;
            int catcher = CatcherFielding();
            int thirdBonus = StealThirdBonus();
            float safeChance = AtBatSimulator.StealSafeChance(
                runner.speed, catcher, targetBase, thirdBonus);

            bool? decision = null;
            GameUI.Instance.ShowStealPrompt(runner, targetBase, safeChance, go => decision = go);
            yield return new WaitUntil(() => decision.HasValue);

            if (decision != true) continue;

            StealOutcome o = simulator.SimulateSteal(runner.speed, catcher, targetBase, thirdBonus);
            string baseName = targetBase == 2 ? "2nd" : "3rd";
            string logMsg;

            if (o.safe)
            {
                state.ApplyStealSuccess(fromBase);
                logMsg = $"{runner.playerName} steals {baseName}!";
            }
            else
            {
                state.ApplyCaughtStealing(fromBase);
                logMsg = $"{runner.playerName} caught stealing {baseName}!";
            }

            Debug.Log($"  STEAL {baseName}: {runner.playerName} (SPD {o.runnerSpeed}) | " +
                      $"Defense d20({o.defenseRoll})+{o.defenseValue}={o.defenseTotal} " +
                      $"{(o.safe ? "<=" : ">")} {o.runnerSpeed} -> {(o.safe ? "SAFE" : "OUT")}");
            Debug.Log($"         {state}");

            GameUI.Instance.AddLogEntry(logMsg);
            GameUI.Instance.UpdateGameState(state, justBatted);

            yield return new WaitForSeconds(secondsPerAtBat * 0.5f);
        }
    }

    // -------------------------------------------------------------------------
    // Shop roll
    // -------------------------------------------------------------------------

    private void RollShop()
    {
        currentShop.Clear();
        var pool = new List<UpgradeCard>(upgradeCardPool);

        for (int i = 0; i < shopSlots && pool.Count > 0; i++)
        {
            int idx = Random.Range(0, pool.Count);
            currentShop.Add(pool[idx]);
            pool.RemoveAt(idx); // no duplicates in the same shop
        }
    }

    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    private void LogAtBat(
    int num,
    ShowdownCardData batter,
    AtBatOutcome o,
    int runsScored,
    int effectiveOnBase)
    {
        string advLabel = o.pitcherHadAdvantage ? "PITCHER" : "BATTER ";
        string runNote = runsScored > 0 ? $"  <- {runsScored} RUN(S)!" : "";

        string swingPart = o.rawSwingRoll == o.swingRoll
                ? $"Swing d20({o.swingRoll,2})"
                : $"Swing d20({o.rawSwingRoll,2}->{o.swingRoll,2})";

        string consoleMsg = $"  AB {num,2}: {batter.playerName,-18} | " +
                  $"Pitch d20({o.pitchRoll,2})+{pitcherCard.control}={o.pitchTotal,2} " +
                  $"vs OB {effectiveOnBase,2} -> {advLabel} | " +
                  $"{swingPart} -> {o.result,-14}{runNote}";

        Debug.Log(consoleMsg);
        Debug.Log($"         {state}");

        // Shorter, friendlier message for the in-game log
        string logMsg = $"{batter.playerName}: {FormatResultShort(o.result)}{runNote}";
        GameUI.Instance?.AddLogEntry(logMsg);
    }

    private string FormatResultShort(AtBatResult result) => result switch
    {
        AtBatResult.SO => "Strikeout",
        AtBatResult.GB => "Ground Out",
        AtBatResult.FB => "Fly Out",
        AtBatResult.PU => "Pop Up",
        AtBatResult.BB => "Walk",
        AtBatResult.Single => "Single",
        AtBatResult.SinglePlus => "Single+",
        AtBatResult.Double => "Double",
        AtBatResult.Triple => "Triple",
        AtBatResult.HR => "HOME RUN!",
        _ => result.ToString()
    };
}
