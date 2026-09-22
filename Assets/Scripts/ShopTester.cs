using UnityEngine;

public class ShopTester : MonoBehaviour
{
    public UpgradeCard testCard;
    public ShowdownCardData testTargetBatter; // leave empty for Team/Field cards

    private InningManager inningManager;

    private void Awake()
    {
        inningManager = GetComponent<InningManager>();
    }

    private void Start()
    {
        // Manually inject the card into the shop at slot 0 then buy it
        inningManager.upgradeCardPool.Insert(0, testCard);
        inningManager.BuyUpgrade(0, testTargetBatter);
    }
}