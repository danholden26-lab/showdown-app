using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Manages the roster panel — 9 card slots, current batter highlight,
/// and drag-to-reorder during shop phase.
/// Attach to the same GameUI GameObject.
/// </summary>
public class RosterUI : MonoBehaviour
{
    [Header("Roster Panel")]
    public Transform rosterContainer;  // Vertical Layout Group parent
    public GameObject rosterCardPrefab; // NameText, OBText, background Image

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.2f);
    public Color activeColor = new Color(0.15f, 0.5f, 0.85f); // blue highlight
    public Color draggableColor = new Color(0.25f, 0.25f, 0.25f);




    [Header("Hover Tooltip")]
    public GameObject tooltipPanel;       // small panel, positioned near cursor or roster area
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipStatsText;
    public TextMeshProUGUI tooltipChartText; // breakdown of chart odds
    public Canvas parentCanvas;           // drag your main Canvas here
    public Vector2 tooltipOffset = new Vector2(20f, -20f); // offset from cursor

    private bool tooltipActive = false;

    // Runtime
    private List<GameObject> slots = new List<GameObject>();
    private List<ShowdownCardData> rosterOrder = new List<ShowdownCardData>();
    private int currentBatterIndex = 0;
    private bool isDraggable = false;

    // Drag state
    private GameObject draggedSlot;
    private int draggedFromIndex = -1;

    private InningManager inningManager;

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Start()
    {
        rosterOrder = new List<ShowdownCardData>(GameManager.Instance.CurrentRun.roster);
        inningManager = FindAnyObjectByType<InningManager>();
        BuildSlots();
        tooltipPanel?.SetActive(false);
    }

    private void Update()
    {
        if (tooltipActive && tooltipPanel != null)
            PositionTooltipAtCursor();
    }
    private void ShowTooltip(ShowdownCardData card)
    {
        if (tooltipPanel == null) return;

        tooltipActive = true;
        tooltipPanel.SetActive(true);
        tooltipNameText.text = card.playerName;

        var inningManager = FindAnyObjectByType<InningManager>();
        var activeUpgrades = inningManager != null
            ? inningManager.GetActiveUpgrades()
            : new List<PendingUpgrade>();

        var modifiedChart = ChartModifier.BuildChart(card.chart, card, activeUpgrades);
        int effectiveOnBase = ChartModifier.GetEffectiveOnBase(card, activeUpgrades);

        string obLine = effectiveOnBase == card.onBase
            ? $"On Base: {card.onBase}"
            : $"On Base: {card.onBase} -> {effectiveOnBase}";

        tooltipStatsText.text = $"{obLine}   Cost: {card.cost}";
        tooltipChartText.text = BuildChartBreakdown(card.chart, modifiedChart);

        PositionTooltipAtCursor();
    }


    private void HideTooltip()
    {
        tooltipActive = false;
        tooltipPanel?.SetActive(false);
    }

    private void PositionTooltipAtCursor()
    {
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            mousePos,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera,
            out Vector2 localPoint);

        var tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        tooltipRect.anchoredPosition = localPoint + tooltipOffset;
    }

    private string BuildChartBreakdown(List<ChartEntry> baseChart, List<ChartEntry> modifiedChart)
    {
        var sb = new System.Text.StringBuilder();

        var baseSlots = CountAllSlots(baseChart);
        var modSlots = CountAllSlots(modifiedChart);

        // Union of every result type appearing in either chart, in ladder order
        var ladder = new[] {
        AtBatResult.SO, AtBatResult.GB, AtBatResult.FB, AtBatResult.PU,
        AtBatResult.BB, AtBatResult.Single, AtBatResult.SinglePlus,
        AtBatResult.Double, AtBatResult.Triple, AtBatResult.HR
    };

        foreach (var result in ladder)
        {
            int baseCount = baseSlots.ContainsKey(result) ? baseSlots[result] : 0;
            int modCount = modSlots.ContainsKey(result) ? modSlots[result] : 0;

            if (baseCount == 0 && modCount == 0) continue; // skip results that never appear

            string line = baseCount == modCount
                ? $"{result,-12} {modCount,2}/20"
                : $"{result,-12} {baseCount,2} -> {modCount,2}/20";

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private Dictionary<AtBatResult, int> CountAllSlots(List<ChartEntry> chart)
    {
        var counts = new Dictionary<AtBatResult, int>();
        foreach (var entry in chart)
        {
            int slots = entry.rollMax - entry.rollMin + 1;
            if (!counts.ContainsKey(entry.result)) counts[entry.result] = 0;
            counts[entry.result] += slots;
        }
        return counts;
    }

    // -------------------------------------------------------------------------
    // Build / Refresh
    // -------------------------------------------------------------------------

    private void BuildSlots()
    {
        foreach (Transform child in rosterContainer)
            Destroy(child.gameObject);
        slots.Clear();

        for (int i = 0; i < rosterOrder.Count; i++)
        {
            var card = rosterOrder[i];
            var go = Instantiate(rosterCardPrefab, rosterContainer);

            SetSlotData(go, card);
            AddDragHandlers(go, i);
            slots.Add(go);
        }
    }

    public void RefreshRoster()
    {
        rosterOrder = new List<ShowdownCardData>(GameManager.Instance.CurrentRun.roster);
        BuildSlots();
        HighlightBatter(currentBatterIndex);
    }

    private void SetSlotData(GameObject go, ShowdownCardData card)
    {
        var nameT = go.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        var obT = go.transform.Find("OBText")?.GetComponent<TextMeshProUGUI>();
        var bg = go.GetComponent<Image>();
        var portraitT = go.transform.Find("PortraitImage")?.GetComponent<Image>();

        if (nameT) nameT.text = card.playerName;
        if (obT) obT.text = $"OB: {card.onBase}";
        if (bg) bg.color = normalColor;
        if (portraitT)
        {
            portraitT.sprite = card.portrait;
            portraitT.enabled = card.portrait != null;
        }
    }

    // -------------------------------------------------------------------------
    // Highlight current batter
    // -------------------------------------------------------------------------

    public void HighlightBatter(int batterIndex)
    {
        currentBatterIndex = batterIndex;
        int highlighted = batterIndex % rosterOrder.Count;

        for (int i = 0; i < slots.Count; i++)
        {
            var bg = slots[i].GetComponent<Image>();
            if (bg) bg.color = i == highlighted ? activeColor : normalColor;
        }
    }


    // -------------------------------------------------------------------------
    // Draggable toggle (only during shop)
    // -------------------------------------------------------------------------

    public void SetDraggable(bool draggable)
    {
        isDraggable = draggable;
    }

    // -------------------------------------------------------------------------
    // Drag handlers
    // -------------------------------------------------------------------------

    private void AddDragHandlers(GameObject go, int index)
    {
        int capturedIndex = index;

        // Click to target (used by ShopUI when buying a Batter-targeted card)
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            var shopUI = GetComponent<ShopUI>();
            if (shopUI != null && shopUI.IsTargeting)
                shopUI.OnBatterTargeted(rosterOrder[capturedIndex]);
        });

        var trigger = go.AddComponent<EventTrigger>();

        // Begin drag
        var beginEntry = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener((data) =>
        {
            if (!isDraggable) return;
            draggedSlot = go;
            draggedFromIndex = capturedIndex;
            go.GetComponent<Image>().color = draggableColor;
        });
        trigger.triggers.Add(beginEntry);

        // Drop — handles two cases: 1) reordering roster (lineup phase),
        // 2) a shop upgrade card dragged from ShopUI onto this batter
        var dropEntry = new EventTrigger.Entry { eventID = EventTriggerType.Drop };
        dropEntry.callback.AddListener((data) =>
        {
            var shopUI = GetComponent<ShopUI>();

            // Case 1: a shop card is being dragged onto this roster slot
            if (shopUI != null && shopUI.IsDraggingCard)
            {
                shopUI.OnCardDroppedOnBatter(rosterOrder[capturedIndex]);
                return;
            }

            // Case 2: normal roster reordering (lineup phase only)
            if (!isDraggable || draggedSlot == null) return;
            if (draggedFromIndex == capturedIndex) return;

            var temp = rosterOrder[draggedFromIndex];
            rosterOrder[draggedFromIndex] = rosterOrder[capturedIndex];
            rosterOrder[capturedIndex] = temp;

            GameManager.Instance.CurrentRun.roster = new List<ShowdownCardData>(rosterOrder);

            draggedSlot = null;
            draggedFromIndex = -1;

            BuildSlots();
            HighlightBatter(currentBatterIndex);
        });
        trigger.triggers.Add(dropEntry);

        // Hover — show/hide stat tooltip
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) => ShowTooltip(rosterOrder[capturedIndex]));
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => HideTooltip());
        trigger.triggers.Add(exitEntry);
    }
}
