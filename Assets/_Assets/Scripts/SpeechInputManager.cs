using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if WINDOWS_UWP || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

/// <summary>
/// Captures free-form speech via <see cref="DictationRecognizer"/> and forwards the
/// recognized text to <see cref="GeminiConnection.SubmitSpeechInput"/>.
///
/// Setup:
///   1. Attach this component to any GameObject in the scene.
///   2. Assign the <see cref="geminiConnection"/> field in the Inspector.
///   3. Optionally assign <see cref="listenButton"/>, <see cref="statusText"/>,
///      and <see cref="hypothesisText"/> for UI feedback.
///   4. Enable the Microphone capability in Project Settings → Player → Capabilities.
///
/// Usage:
///   - Press / tap the listen button (or call <see cref="StartListening"/> from any script).
///   - Speak your command in Turkish or English.
///   - After silence or max recording time, the recognized text is sent automatically.
///   - Call <see cref="StopListening"/> to cancel early.
/// </summary>
public class SpeechInputManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The GeminiConnection component that processes AI requests.")]
    [SerializeField] private GeminiConnection geminiConnection;

    [Header("UI (Optional)")]
    [Tooltip("Button that toggles listening on/off. Leave empty if using gesture or code.")]
    [SerializeField] private Button listenButton;

    [Tooltip("Text shown when idle, listening, or on error.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("Live preview of partial speech hypothesis (what the recognizer thinks so far).")]
    [SerializeField] private TMP_Text hypothesisText;

    [Header("Recognizer Settings")]
    [Tooltip("Seconds of silence before recognition stops automatically.")]
    [SerializeField] private float autoSilenceTimeoutSeconds = 3f;

    [Tooltip("Maximum recording duration regardless of silence.")]
    [SerializeField] private float initialSilenceTimeoutSeconds = 5f;

    [Tooltip("If true, speaking 'iptal' or 'cancel' aborts the current dictation.")]
    [SerializeField] private bool enableVoiceCancel = true;

    // ── State ─────────────────────────────────────────────────────────────────
    private bool isListening;

#if WINDOWS_UWP || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private DictationRecognizer dictationRecognizer;
#endif

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        UpdateListenButtonLabel(listening: false);
        ShowStatus("Ready.");

        if (hypothesisText != null)
            hypothesisText.text = string.Empty;

        if (listenButton != null)
            listenButton.onClick.AddListener(ToggleListening);
    }

    private void OnDestroy()
    {
        DestroyRecognizer();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Stop dictation when the app loses focus to avoid mic conflicts.
        if (!hasFocus && isListening)
            StopListening();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Starts speech recognition. Safe to call when already listening.</summary>
    public void StartListening()
    {
#if WINDOWS_UWP || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (isListening)
            return;

        if (geminiConnection == null)
        {
            ShowStatus("Hata: GeminiConnection atanmamış.", error: true);
            Debug.LogError("[SpeechInputManager] GeminiConnection reference is not set.");
            return;
        }

        CreateRecognizer();
        dictationRecognizer.Start();
        isListening = true;
        UpdateListenButtonLabel(listening: true);
        ShowStatus("Dinleniyor… Komutunuzu söyleyin.");
        Debug.Log("[SpeechInputManager] Dictation started.");
#else
        ShowStatus("Ses tanıma bu platformda desteklenmiyor.", error: true);
        Debug.LogWarning("[SpeechInputManager] DictationRecognizer is only supported on Windows.");
#endif
    }

    /// <summary>Stops speech recognition and discards any partial result.</summary>
    public void StopListening()
    {
#if WINDOWS_UWP || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (!isListening)
            return;

        if (dictationRecognizer != null &&
            dictationRecognizer.Status == SpeechSystemStatus.Running)
            dictationRecognizer.Stop();

        isListening = false;
        UpdateListenButtonLabel(listening: false);

        if (hypothesisText != null)
            hypothesisText.text = string.Empty;

        ShowStatus("Dinleme durduruldu.");
        Debug.Log("[SpeechInputManager] Dictation stopped by user.");
#endif
    }

    /// <summary>Toggles listening on/off — suitable for a single button binding.</summary>
    public void ToggleListening()
    {
        if (isListening)
            StopListening();
        else
            StartListening();
    }

    // ── Recognizer management ─────────────────────────────────────────────────

#if WINDOWS_UWP || UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

    private void CreateRecognizer()
    {
        DestroyRecognizer();

        dictationRecognizer = new DictationRecognizer
        {
            AutoSilenceTimeoutSeconds = autoSilenceTimeoutSeconds,
            InitialSilenceTimeoutSeconds = initialSilenceTimeoutSeconds
        };

        dictationRecognizer.DictationHypothesis  += OnHypothesis;
        dictationRecognizer.DictationResult      += OnResult;
        dictationRecognizer.DictationComplete    += OnComplete;
        dictationRecognizer.DictationError       += OnError;
    }

    private void DestroyRecognizer()
    {
        if (dictationRecognizer == null)
            return;

        dictationRecognizer.DictationHypothesis  -= OnHypothesis;
        dictationRecognizer.DictationResult      -= OnResult;
        dictationRecognizer.DictationComplete    -= OnComplete;
        dictationRecognizer.DictationError       -= OnError;

        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
            dictationRecognizer.Stop();

        dictationRecognizer.Dispose();
        dictationRecognizer = null;
    }

    // ── Recognizer event handlers ─────────────────────────────────────────────

    /// <summary>Called continuously with the live partial transcription.</summary>
    private void OnHypothesis(string text)
    {
        if (hypothesisText != null)
            hypothesisText.text = text;
    }

    /// <summary>Called when a confirmed phrase is recognized.</summary>
    private void OnResult(string text, ConfidenceLevel confidence)
    {
        if (hypothesisText != null)
            hypothesisText.text = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return;

        Debug.Log($"[SpeechInputManager] Recognized (confidence={confidence}): \"{text}\"");

        // Voice cancel shortcut
        if (enableVoiceCancel)
        {
            string lower = text.ToLowerInvariant().Trim();
            if (lower == "iptal" || lower == "cancel" || lower == "dur" || lower == "stop")
            {
                StopListening();
                ShowStatus("İptal edildi.");
                return;
            }
        }

        // Only submit high/medium confidence results to avoid garbled commands.
        if (confidence == ConfidenceLevel.Rejected)
        {
            ShowStatus($"Anlaşılamadı (güven düşük). Tekrar deneyin.", error: true);
            return;
        }

        ShowStatus($"Tanındı: \"{text}\"");

        // Forward to GeminiConnection on the main thread
        geminiConnection.SubmitSpeechInput(text);
    }

    /// <summary>Called when dictation session ends for any reason.</summary>
    private void OnComplete(DictationCompletionCause cause)
    {
        isListening = false;
        UpdateListenButtonLabel(listening: false);

        if (hypothesisText != null)
            hypothesisText.text = string.Empty;

        string message = cause switch
        {
            DictationCompletionCause.Complete              => "Tanıma tamamlandı.",
            DictationCompletionCause.TimeoutExceeded       => "Zaman aşımı. Tekrar deneyin.",
            DictationCompletionCause.PauseLimitExceeded    => "Sessizlik sınırı aşıldı.",
            DictationCompletionCause.Canceled              => "İptal edildi.",
            DictationCompletionCause.MicrophoneUnavailable => "Mikrofon bulunamadı.",
            DictationCompletionCause.NetworkFailure        => "Ağ hatası (internet gerekebilir).",
            DictationCompletionCause.UnknownError          => "Bilinmeyen hata.",
            _                                              => $"Tamamlandı ({cause})."
        };

        bool isError = cause != DictationCompletionCause.Complete &&
                       cause != DictationCompletionCause.Canceled;

        ShowStatus(message, error: isError);
        Debug.Log($"[SpeechInputManager] Dictation complete. Cause: {cause}");
    }

    /// <summary>Called when the recognizer encounters an unrecoverable error.</summary>
    private void OnError(string error, int hresult)
    {
        isListening = false;
        UpdateListenButtonLabel(listening: false);
        ShowStatus($"Ses hatası: {error}", error: true);
        Debug.LogError($"[SpeechInputManager] Dictation error: {error} (HRESULT {hresult})");
    }

#endif

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void ShowStatus(string message, bool error = false)
    {
        if (statusText == null)
            return;

        statusText.text = message;
        statusText.color = error
            ? new Color(0.9f, 0.2f, 0.2f)
            : new Color(0.8f, 0.95f, 1.0f);
    }

    private void UpdateListenButtonLabel(bool listening)
    {
        if (listenButton == null)
            return;

        TMP_Text label = listenButton.GetComponentInChildren<TMP_Text>();
        if (label == null)
            return;

        label.text = listening ? "⏹ Durdur" : "🎤 Dinle";
        listenButton.image.color = listening
            ? new Color(0.9f, 0.2f, 0.2f, 0.85f)
            : new Color(0.15f, 0.47f, 0.90f, 0.85f);
    }
}
