using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drives the Title screen.
/// 
/// Scene hierarchy:
///   Canvas
///   └── TitlePanel
///       ├── TitleText          (TextMeshProUGUI) "SHOWDOWN"
///       ├── SubtitleText       (TextMeshProUGUI) "Baseball Card Auto Battler"
///       └── ModeButtonGroup
///           ├── BossRushButton     -> OnClick: OnBossRush()
///           ├── ClassicButton      -> OnClick: OnClassic()     [greyed out for now]
///           └── MultiplayerButton  -> OnClick: OnMultiplayer() [greyed out for now]
/// </summary>
public class TitleScreenManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button bossRushButton;
    public Button classicButton;
    public Button multiplayerButton;

    [Header("Coming Soon Labels")]
    [Tooltip("TextMeshPro label shown under Classic button")]
    public TextMeshProUGUI classicComingSoonText;
    [Tooltip("TextMeshPro label shown under Multiplayer button")]
    public TextMeshProUGUI multiplayerComingSoonText;

    private void Start()
    {
        // Boss Rush is the only active mode for now
        classicButton.interactable     = false;
        multiplayerButton.interactable = false;

        if (classicComingSoonText)     classicComingSoonText.text     = "Coming Soon";
        if (multiplayerComingSoonText) multiplayerComingSoonText.text = "Coming Soon";

        bossRushButton.onClick.AddListener(OnBossRush);
        classicButton.onClick.AddListener(OnClassic);
        multiplayerButton.onClick.AddListener(OnMultiplayer);
    }

    public void OnBossRush()
    {
        Debug.Log("[Title] Boss Rush selected.");
        GameManager.Instance.StartRun(GameMode.BossRush);
    }

    public void OnClassic()
    {
        Debug.Log("[Title] Classic Mode selected.");
        GameManager.Instance.StartRun(GameMode.Classic);
    }

    public void OnMultiplayer()
    {
        Debug.Log("[Title] Multiplayer selected.");
        GameManager.Instance.StartRun(GameMode.Multiplayer);
    }
}
