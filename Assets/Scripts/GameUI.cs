using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls which panel is active and updates all display elements.
/// Attach to a GameUI GameObject in the Game scene.
/// References ShopUI and RosterUI which live on the same GameObject.
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Panels")]
    public GameObject simPanel;
    public GameObject resultPanel;
    public GameObject shopPanel;

    [Header("Pitcher Panel (left)")]
    public TextMeshProUGUI pitcherNameText;
    public TextMeshProUGUI pitcherControlText;

    [Header("Game State Panel (center)")]
    public TextMeshProUGUI inningText;
    public TextMeshProUGUI scoreText;
    public Image[] outBoxes;       // 3 Image components, filled = out recorded
    public Image firstBase;
    public Image secondBase;
    public Image thirdBase;
    public Image homeBase;

    [Header("Base Colors")]
    public Color baseEmptyColor = new Color(0.3f, 0.3f, 0.3f);
    public Color baseFilledColor = new Color(1f, 0.8f, 0f);    // gold
    public Color outEmptyColor = new Color(0.3f, 0.3f, 0.3f);
    public Color outFilledColor = new Color(0.9f, 0.2f, 0.2f); // red

    [Header("Current Batter Panel")]
    public TextMeshProUGUI batterNameText;
    public TextMeshProUGUI batterOnBaseText;
    public TextMeshProUGUI batterUpgradesText; // lists any active upgrades on this batter

    [Header("In-Game Log")]
    public Transform logContainer;     // Vertical Layout Group parent
    public GameObject logEntryPrefab;    // simple TMP text prefab
    public ScrollRect logScrollRect;
    [Tooltip("Max log entries kept before trimming oldest")]
    public int maxLogEntries = 50;

    [Header("Result Panel")]
    public TextMeshProUGUI resultRunsText;
    public TextMeshProUGUI resultTotalText;
    public TextMeshProUGUI resultGoldText;
    public Button continueButton;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI gameOverScoreText;
    public Button backToTitleButton;

    [Header("Pregame Panel")]
    public GameObject pregamePanel;
    public TextMeshProUGUI pregameInstructionsText;

    // Sub-controllers
    private ShopUI shopUI;
    private RosterUI rosterUI;

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        shopUI = GetComponent<ShopUI>();
        rosterUI = GetComponent<RosterUI>();
    }

    private void Start()
    {
        // Start in sim panel, everything else hidden
        ShowSim();

        // Wire pitcher info from GameManager
        var pitcher = GameManager.Instance.GetCurrentBoss();
        if (pitcher != null)
        {
            pitcherNameText.text = pitcher.playerName;
            pitcherControlText.text = $"Control: {pitcher.control}";
        }

        continueButton.onClick.AddListener(OnContinueClicked);
    }

    // -------------------------------------------------------------------------
    // Panel control
    // -------------------------------------------------------------------------

    public void ShowSim()
    {
        simPanel.SetActive(true);
        resultPanel.SetActive(false);
        shopPanel.SetActive(false);
        rosterUI?.SetDraggable(false);
        gameOverPanel.SetActive(false);
        pregamePanel.SetActive(false);
    }

    public void ShowResult(int runsThisInning, int totalRuns, int goldEarned)
    {
        simPanel.SetActive(false);
        resultPanel.SetActive(true);
        shopPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        pregamePanel.SetActive(false);

        resultRunsText.text = $"Runs scored: {runsThisInning}";
        resultTotalText.text = $"Total runs: {totalRuns}";
        resultGoldText.text = $"+{goldEarned} gold earned";
    }

    public void ShowShop(bool isLineupPhase = false)
    {
        simPanel.SetActive(false);
        resultPanel.SetActive(false);
        shopPanel.SetActive(true);
        gameOverPanel.SetActive(false);
        pregamePanel.SetActive(false);

        //shopUI?.SetLineupPhase(isLineupPhase); old bits
        shopUI?.RefreshShop();
        rosterUI?.SetDraggable(isLineupPhase); // only draggable during lineup
        rosterUI?.RefreshRoster();
    }

    public void ShowGameOver(int finalScore, bool won)
    {
        simPanel.SetActive(false);
        resultPanel.SetActive(false);
        shopPanel.SetActive(false);
        gameOverPanel.SetActive(true);
        pregamePanel.SetActive(false);

        gameOverScoreText.text = won
            ? $"You beat the boss!\nFinal Score: {finalScore} runs"
            : $"Game Over\nFinal Score: {finalScore} runs";

        rosterUI?.SetDraggable(false);

        backToTitleButton.onClick.RemoveAllListeners();
        if (won)
        {
            var btnText = backToTitleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText) btnText.text = "Continue";
            backToTitleButton.onClick.AddListener(() => GameManager.Instance.GoToMap());
        }
        else
        {
            var btnText = backToTitleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText) btnText.text = "Back to Title";
            backToTitleButton.onClick.AddListener(() => GameManager.Instance.EndRun());
        }
    }
    // Pre-game Panel
    public void ShowPregame()
    {
        simPanel.SetActive(false);
        resultPanel.SetActive(false);
        shopPanel.SetActive(false);
        pregamePanel.SetActive(true);

        if (pregameInstructionsText)
            pregameInstructionsText.text = "Drag players to set your batting order, then hit Play Ball!";

        rosterUI?.SetDraggable(true);
        rosterUI?.RefreshRoster();
    }

    private void OnContinueClicked()
    {
        var inningManager = FindAnyObjectByType<InningManager>();
        inningManager.AcknowledgeResult();
        // ShowShop is called by InningManager.ShopPhase after gold syncs
    }

    // -------------------------------------------------------------------------
    // Game state updates — called by InningManager each at-bat
    // -------------------------------------------------------------------------

    public void UpdateGameState(GameState state, ShowdownCardData currentBatter)
    {
        inningText.text = $"Inning {state.CurrentInning}";
        scoreText.text = $"Runs: {state.RunsThisInning}  Total: {state.TotalRuns}";

        UpdateOuts(state.Outs);
        UpdateBases(state.First, state.Second, state.Third);
        UpdateBatterPanel(currentBatter, state);
        rosterUI?.HighlightBatter(state.CurrentBatterIndex);
    }

    private void UpdateBatterPanel(ShowdownCardData batter, GameState state)
    {
        if (batter == null) return;

        if (batterNameText) batterNameText.text = batter.playerName;
        if (batterOnBaseText) batterOnBaseText.text = $"OB: {batter.onBase}";

        if (batterUpgradesText)
        {
            var activeOnThisBatter = state.ActiveUpgrades
                .Where(u => u.AppliesToBatter(batter))
                .Select(u => u.card.cardName)
                .ToList();

            batterUpgradesText.text = activeOnThisBatter.Count > 0
                ? "Upgrades: " + string.Join(", ", activeOnThisBatter)
                : "";
        }
    }

    // -------------------------------------------------------------------------
    // In-game log
    // -------------------------------------------------------------------------

    public void AddLogEntry(string message)
    {
        if (logContainer == null || logEntryPrefab == null) return;

        var entry = Instantiate(logEntryPrefab, logContainer);
        var text = entry.GetComponent<TextMeshProUGUI>() ?? entry.GetComponentInChildren<TextMeshProUGUI>();
        if (text) text.text = message;

        // Trim oldest entries if over the cap
        if (logContainer.childCount > maxLogEntries)
            Destroy(logContainer.GetChild(0).gameObject);

        // Auto-scroll to bottom
        Canvas.ForceUpdateCanvases();
        if (logScrollRect) logScrollRect.verticalNormalizedPosition = 0f;
    }

    public void ClearLog()
    {
        if (logContainer == null) return;
        foreach (Transform child in logContainer)
            Destroy(child.gameObject);
    }

    private void UpdateOuts(int outs)
    {
        for (int i = 0; i < outBoxes.Length; i++)
            outBoxes[i].color = i < outs ? outFilledColor : outEmptyColor;
    }

    private void UpdateBases(bool first, bool second, bool third)
    {
        firstBase.color = first ? baseFilledColor : baseEmptyColor;
        secondBase.color = second ? baseFilledColor : baseEmptyColor;
        thirdBase.color = third ? baseFilledColor : baseEmptyColor;
        homeBase.color = baseEmptyColor; // home only lights up briefly on a score — future
    }



}
