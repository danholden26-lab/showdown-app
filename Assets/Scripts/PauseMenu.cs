// PauseManager.cs — attach to the GameManager GameObject
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause UI")]
    public GameObject pausePanel; // assign a persistent overlay panel

    private bool isPaused = false;

    private void Awake()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        pausePanel?.SetActive(isPaused);
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public void OnResumeClicked() => TogglePause();

    public void OnQuitClicked()
    {
        pausePanel?.SetActive(false);
        isPaused = false;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }
    public void OnTitleClicked()
    {
        pausePanel?.SetActive(false);
        isPaused = false;
        Time.timeScale = 1f;
        GameManager.Instance.EndRun();
    }
}