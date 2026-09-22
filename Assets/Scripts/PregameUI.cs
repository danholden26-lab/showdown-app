using UnityEngine;
using UnityEngine.UI;

public class PregameUI : MonoBehaviour
{
    public Button playBallButton;
    private InningManager inningManager;
    public Button quitRunButton;

    private void Start()
    {
        inningManager = FindAnyObjectByType<InningManager>();
        playBallButton.onClick.AddListener(OnPlayBall);
        quitRunButton.onClick.AddListener(OnQuitRun);
    }

    private void OnPlayBall()
    {
        inningManager.SimInningNow();
        GameUI.Instance.ShowSim();
    }

    private void OnQuitRun()
    {
        ConfirmDialog.Instance.Show(
            "Are you sure you want to quit this run? Your progress will be lost.",
            () => GameManager.Instance.EndRun()
        );
    }
}