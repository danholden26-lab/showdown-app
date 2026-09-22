using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that persists for the entire session.
/// Owns RunState and drives scene transitions.
/// 
/// Scene names (add these in File -> Build Settings):
///   0 - Title
///   1 - Draft
///   2 - Game
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Config")]
    public GameConfig config;

    [Header("Card Pool")]
    [Tooltip("All ShowdownCardData batter assets available to draft")]
    public List<ShowdownCardData> batterPool = new List<ShowdownCardData>();

    [Tooltip("Boss Rush pitcher sequence — index 0 is the first boss")]
    public List<ShowdownCardData> bossPitchers = new List<ShowdownCardData>();

    // Live run state — null between runs
    public RunState CurrentRun { get; private set; }

    // -------------------------------------------------------------------------
    // Singleton setup
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.LoadScene("Title");
    }

    // -------------------------------------------------------------------------
    // Run lifecycle
    // -------------------------------------------------------------------------

    public void StartRun(GameMode mode)
    {
        CurrentRun = new RunState { mode = mode };
        Debug.Log($"[GameManager] Starting run: {mode}");
        LoadScene("Draft");
    }

    public void EndRun()
    {
        if (CurrentRun == null)
        {
            Debug.Log("[GameManager] EndRun() called with no active run — just returning to Title.");
            LoadScene("Title");
            return;
        }

        Debug.Log($"[GameManager] Run over. Total runs scored: {CurrentRun.totalRunsScored}");
        CurrentRun = null;
        LoadScene("Title");
    }

    // -------------------------------------------------------------------------
    // Scene transitions
    // -------------------------------------------------------------------------

    public void LoadScene(string sceneName) => SceneManager.LoadScene(sceneName);

    public void GoToDraft()  => LoadScene("Draft");
    public void GoToGame()   => LoadScene("Game");
    public void GoToTitle()  => LoadScene("Title");

    // -------------------------------------------------------------------------
    // Go To Title Screen
    // -------------------------------------------------------------------------

    public void GoToMap()
    {
        // STUB: map/campaign screen not built yet.
        // Swap this out once the map scene exists — this is the one call site to update.
        Debug.Log("[GameManager] GoToMap() stub — map screen not implemented yet.");
        EndRun();
    }

    // -------------------------------------------------------------------------
    // Boss Rush helpers
    // -------------------------------------------------------------------------

    public ShowdownCardData GetCurrentBoss()
    {
        if (bossPitchers.Count == 0)
        {
            Debug.LogError("[GameManager] No boss pitchers assigned!");
            return null;
        }
        // Wrap so it works even if run goes past the defined list
        return bossPitchers[CurrentRun.currentBossIndex % bossPitchers.Count];
    }

    public void AdvanceBoss() => CurrentRun.currentBossIndex++;
}
