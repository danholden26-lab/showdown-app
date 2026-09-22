using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTeam", menuName = "Showdown/Team")]
public class TeamData : ScriptableObject
{
    public string teamName;

    [Tooltip("Batting order — slot 0 is leadoff, slot 8 is ninth.")]
    public List<ShowdownCardData> battingOrder = new List<ShowdownCardData>(9);

    public int PlayerCount => battingOrder.Count;

    public ShowdownCardData GetBatter(int lineupIndex)
    {
        if (battingOrder.Count == 0)
        {
            Debug.LogError($"[Showdown] Team '{teamName}' has no batters!");
            return null;
        }
        return battingOrder[lineupIndex % battingOrder.Count];
    }
}
