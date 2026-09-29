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
    private Action onDecline;

    private void Awake()
    {
        Instance = this;
        dialogPanel.SetActive(false);

        yesButton.onClick.AddListener(OnYesClicked);
        noButton.onClick.AddListener(OnNoClicked);
    }

    /// <summary>Show a confirm popup. Calls onConfirmed only if the user clicks Yes.</summary>
    public void Show(string message, Action onConfirmed) => Show(message, onConfirmed, null);

    /// <summary>Show a confirm popup with a callback for each answer.</summary>
    public void Show(string message, Action onConfirmed, Action onDeclined)
    {
        messageText.text = message;
        onConfirm = onConfirmed;
        onDecline = onDeclined;
        dialogPanel.SetActive(true);
    }

    private void OnYesClicked()
    {
        dialogPanel.SetActive(false);
        var callback = onConfirm;
        onConfirm = null;
        onDecline = null;
        callback?.Invoke();
    }

    private void OnNoClicked()
    {
        dialogPanel.SetActive(false);
        var callback = onDecline;
        onConfirm = null;
        onDecline = null;
        callback?.Invoke();
    }
}
