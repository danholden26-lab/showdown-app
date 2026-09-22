using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class ConfirmDialog : MonoBehaviour
{
    public static ConfirmDialog Instance { get; private set; }

    [Header("UI")]
    public GameObject dialogPanel;
    public TextMeshProUGUI messageText;
    public Button yesButton;
    public Button noButton;

    private Action onConfirm;

    private void Awake()
    {
        Instance = this;
        dialogPanel.SetActive(false);

        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);
    }

    /// <summary>Show a confirm popup. Calls onConfirmed only if the user clicks Yes.</summary>
    public void Show(string message, Action onConfirmed)
    {
        messageText.text = message;
        onConfirm = onConfirmed;
        dialogPanel.SetActive(true);
    }

    private void OnYesClicked()
    {
        dialogPanel.SetActive(false);
        onConfirm?.Invoke();
        onConfirm = null;
    }

    private void OnNoClicked()
    {
        dialogPanel.SetActive(false);
        onConfirm = null;
    }
}