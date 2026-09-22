using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Showdown/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Economy")]
    public int goldPerInning = 3;
    public int goldPerRun = 1;
    public int shopSlots = 3;

    [Header("Sell Back")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of card cost returned when selling a player. 0.5 = 50% back.")]
    public float playerSellBackRate = 0.5f;

    [Header("Draft")]
    public int rosterSize = 9;
    public int draftChoices = 4;

    [Header("Boss Rush")]
    public int inningsPerBoss = 9;

    [Header("Classic Mode")]
    public int classicInnings = 9;

    /// <summary>Calculate sell price for a card based on its cost and the sell back rate.</summary>
    public int GetSellPrice(int cardCost)
    {
        return Mathf.Max(1, Mathf.FloorToInt(cardCost * playerSellBackRate));
    }
}
