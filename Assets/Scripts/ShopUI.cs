using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the shop overlay — displays available upgrade cards,
/// handles buying, targeting, selling players, and the Sim Inning button.
/// Attach to the same GameUI GameObject.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("Shop Panel")]
    public TextMeshProUGUI goldText;
    public Transform shopSlotsContainer;  // Horizontal Layout Group
    public GameObject shopCardPrefab;       // CardNameText, CostText, EffectText, BuyButton
    public Button simInningButton;

    [Header("Targeting")]
    [Tooltip("Panel that appears asking you to pick a batter target")]
    public GameObject targetingPanel;
    public TextMeshProUGUI targetingPromptText;

    // Runtime
    private InningManager inningManager;
    private UpgradeCard pendingCard;
    private int pendingShopIndex = -1;

    public bool IsTargeting => pendingCard != null;

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    private void Start()
    {
        inningManager = FindAnyObjectByType<InningManager>();
        simInningButton.onClick.AddListener(OnSimInning);

        if (targetingPanel) targetingPanel.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Refresh shop display
    // -------------------------------------------------------------------------

    public void RefreshShop()
    {
        UpdateGold();

        foreach (Transform child in shopSlotsContainer)
            Destroy(child.gameObject);

        var shop = inningManager.GetCurrentShop();

        for (int i = 0; i < shop.Count; i++)
        {
            var card = shop[i];
            var go = Instantiate(shopCardPrefab, shopSlotsContainer);

            var nameT = go.transform.Find("CardNameText")?.GetComponent<TextMeshProUGUI>();
            var costT = go.transform.Find("CostText")?.GetComponent<TextMeshProUGUI>();
            var effectT = go.transform.Find("EffectText")?.GetComponent<TextMeshProUGUI>();
            var buyBtn = go.transform.Find("BuyButton")?.GetComponent<Button>();

            if (nameT) nameT.text = card.cardName;
            if (costT) costT.text = $"{card.cost}g";
            if (effectT) effectT.text = $"{card.GetEffectDescription()}\n<i>{card.flavorText}</i>";
            // if (effectT) effectT.text = $"{card.scope} | {card.targetType}\n{card.GetEffectDescription()}\n<i>{card.flavorText}</i>";

            // Grey out if can't afford
            bool canAfford = GameManager.Instance.CurrentRun.gold >= card.cost;
            if (buyBtn) buyBtn.interactable = canAfford;

            int capturedIndex = i;
            buyBtn?.onClick.AddListener(() => OnBuyClicked(capturedIndex, card));

            // Drag support — only meaningful for Batter-targeted cards,
            // but we add it regardless so the card always feels draggable
            AddShopCardDragHandlers(go, capturedIndex, card);
        }
    }

    // -------------------------------------------------------------------------
    // Drag-to-target support
    // -------------------------------------------------------------------------

    private UpgradeCard draggedCard;
    private int draggedShopIndex = -1;

    /// <summary>True while a shop card is being dragged — RosterUI checks this on drop.</summary>
    public bool IsDraggingCard => draggedCard != null;

    private void AddShopCardDragHandlers(GameObject go, int shopIndex, UpgradeCard card)
    {
        var trigger = go.GetComponent<UnityEngine.EventSystems.EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var beginEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.BeginDrag };
        beginEntry.callback.AddListener((data) =>
        {
            // Only Batter-targeted cards make sense to drag onto a player
            if (card.targetType != UpgradeTargetType.Batter) return;
            if (GameManager.Instance.CurrentRun.gold < card.cost) return;

            draggedCard = card;
            draggedShopIndex = shopIndex;
        });
        trigger.triggers.Add(beginEntry);

        var endEntry = new UnityEngine.EventSystems.EventTrigger.Entry
        { eventID = UnityEngine.EventSystems.EventTriggerType.EndDrag };
        endEntry.callback.AddListener((data) =>
        {
            draggedCard = null;
            draggedShopIndex = -1;
        });
        trigger.triggers.Add(endEntry);
    }

    /// <summary>Called by RosterUI when a dragged shop card is dropped on a roster slot.</summary>
    public void OnCardDroppedOnBatter(ShowdownCardData batter)
    {
        if (draggedCard == null) return;

        inningManager.BuyUpgrade(draggedShopIndex, batter);

        draggedCard = null;
        draggedShopIndex = -1;

        UpdateGold();
        RefreshShop();
    }

    // -------------------------------------------------------------------------
    // Buying
    // -------------------------------------------------------------------------

    private void OnBuyClicked(int shopIndex, UpgradeCard card)
    {
        if (card.targetType == UpgradeTargetType.Batter)
        {
            // Need to pick a target — show targeting panel
            pendingCard = card;
            pendingShopIndex = shopIndex;
            ShowTargetingPanel(card);
        }
        else
        {
            // Team or Field — no target needed, buy immediately
            inningManager.BuyUpgrade(shopIndex);
            UpdateGold();
            RefreshShop();
        }
    }

    /// <summary>Called by roster card buttons when in targeting mode.</summary>
    public void OnBatterTargeted(ShowdownCardData batter)
    {
        if (pendingCard == null) return;

        inningManager.BuyUpgrade(pendingShopIndex, batter);
        pendingCard = null;
        pendingShopIndex = -1;

        if (targetingPanel) targetingPanel.SetActive(false);
        UpdateGold();
        RefreshShop();
    }

    public void CancelTargeting()
    {
        pendingCard = null;
        pendingShopIndex = -1;
        if (targetingPanel) targetingPanel.SetActive(false);
    }

    private void ShowTargetingPanel(UpgradeCard card)
    {
        if (targetingPanel)
        {
            targetingPanel.SetActive(true);
            if (targetingPromptText)
                targetingPromptText.text = $"Target a batter for:\n{card.cardName}";
        }
    }

    // -------------------------------------------------------------------------
    // Sim button
    // -------------------------------------------------------------------------

    private void OnSimInning()
    {
        inningManager.SimInningNow();
        GameUI.Instance.ShowSim();
    }
    public void SetLineupPhase(bool isLineup)
    {
        var btnText = simInningButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText) btnText.text = isLineup ? "Start Game" : "Sim Inning";

        // Hide shop cards during lineup phase
        shopSlotsContainer.gameObject.SetActive(!isLineup);
        if (goldText) goldText.gameObject.SetActive(!isLineup);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void UpdateGold()
    {
        if (goldText)
            goldText.text = $"Gold: {GameManager.Instance.CurrentRun.gold}";
    }
}
