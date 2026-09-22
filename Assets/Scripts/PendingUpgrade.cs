/// <summary>
/// Runtime pairing of an UpgradeCard asset with the target it was played on.
/// Created by the game manager when the player buys + targets a card.
/// Never stored as an asset — lives only in memory during a game.
/// </summary>
public class PendingUpgrade
{
    public UpgradeCard card;

    /// <summary>
    /// For Batter-targeted cards: the specific card this upgrade is attached to.
    /// Null for Team or Field targeted cards.
    /// </summary>
    public ShowdownCardData targetBatter;

    public PendingUpgrade(UpgradeCard card, ShowdownCardData targetBatter = null)
    {
        this.card          = card;
        this.targetBatter  = targetBatter;
    }

    /// <summary>
    /// Returns true if this upgrade should apply to the given batter this at-bat.
    /// </summary>
    public bool AppliesToBatter(ShowdownCardData batter)
    {
        return card.targetType switch
        {
            UpgradeTargetType.Batter => targetBatter == batter,
            UpgradeTargetType.Team   => true,
            UpgradeTargetType.Field  => true,
            _                        => false
        };
    }
}
