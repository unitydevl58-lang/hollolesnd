using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies a dark glassmorphism theme to the existing HoloLens UI Canvas at runtime.
/// Attach this to any persistent GameObject and assign the Canvas reference in the Inspector.
/// </summary>
[AddComponentMenu("HoloLens App/UI Theme Controller")]
public class UIThemeController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Canvas")]
    [Tooltip("The World-Space canvas that contains all UI elements.")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Theme Colors")]
    [SerializeField] private Color panelBackground   = new Color(0.05f, 0.06f, 0.10f, 0.88f);
    [SerializeField] private Color panelBorder       = new Color(0.25f, 0.55f, 0.95f, 0.45f);
    [SerializeField] private Color primaryAccent     = new Color(0.20f, 0.55f, 1.00f, 1.00f);
    [SerializeField] private Color dangerColor       = new Color(0.90f, 0.22f, 0.22f, 1.00f);
    [SerializeField] private Color textPrimary       = new Color(0.94f, 0.96f, 1.00f, 1.00f);
    [SerializeField] private Color textSecondary     = new Color(0.60f, 0.68f, 0.80f, 1.00f);
    [SerializeField] private Color inputBackground   = new Color(0.10f, 0.12f, 0.18f, 0.95f);

    [Header("Named UI References (auto-found if blank)")]
    [SerializeField] private TMP_InputField userInput;
    [SerializeField] private Button         sendButton;
    [SerializeField] private Button         listenButton;
    [SerializeField] private TMP_Text       statusText;
    [SerializeField] private TMP_Text       hypothesisText;
    [SerializeField] private TMP_Text       errorText;
    [SerializeField] private TMP_Text       infoText;

    [Header("Animation")]
    [SerializeField] private float pulseSpeed      = 2.0f;
    [SerializeField] private float pulseAmplitude  = 0.08f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Image listenButtonImage;
    private bool  isListening;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (targetCanvas == null)
        {
            Debug.LogError("[UIThemeController] No Canvas found. Assign targetCanvas in Inspector.");
            return;
        }

        AutoFindReferences();
        ApplyTheme();
        StartCoroutine(PulseListenButton());
    }

    // ── Auto-find ─────────────────────────────────────────────────────────────

    private void AutoFindReferences()
    {
        if (userInput     == null) userInput     = FindInCanvas<TMP_InputField>("Input");
        if (sendButton    == null) sendButton    = FindButtonByName("Button", "Gönder", "Send");
        if (listenButton  == null) listenButton  = FindButtonByName("Listen Button", "Dinle");
        if (statusText    == null) statusText    = FindTMPByName("Status Text", "Status");
        if (hypothesisText== null) hypothesisText= FindTMPByName("Hypothesis Text", "Hypothesis");
        if (errorText     == null) errorText     = FindTMPByName("Hata", "Error");
        if (infoText      == null) infoText      = FindTMPByName("Bilgi", "Info");
    }

    // ── Theme Application ─────────────────────────────────────────────────────

    private void ApplyTheme()
    {
        // Background panel for the whole canvas group
        StyleCanvasBackground();

        // Input field
        if (userInput != null)
        {
            Image bg = userInput.GetComponent<Image>();
            if (bg != null) { bg.color = inputBackground; }
            if (userInput.textComponent != null)
                userInput.textComponent.color = textPrimary;
            if (userInput.placeholder is TMP_Text ph)
                ph.color = textSecondary;
        }

        // Send button
        StyleButton(sendButton, primaryAccent, textPrimary, "Gönder");

        // Listen button
        if (listenButton != null)
        {
            listenButtonImage = listenButton.GetComponent<Image>();
            StyleButton(listenButton, primaryAccent, textPrimary, "Dinle");
        }

        // Text elements
        StyleText(statusText,     textSecondary, 14);
        StyleText(hypothesisText, textPrimary,   16);
        StyleText(errorText,      dangerColor,   14);
        StyleText(infoText,       textSecondary, 13);

        // Tint all remaining Images that haven't been touched
        TintUnstyledImages();
    }

    private void StyleCanvasBackground()
    {
        // Find the root Image on the Canvas itself (if any) and tint it.
        // Do NOT create a child panel — that can push other elements out of place.
        Image rootImg = targetCanvas.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.color = panelBackground;
            return;
        }

        // Only add a background panel if there's genuinely nothing styling the canvas root.
        Transform existing = targetCanvas.transform.Find("__ThemeBackground__");
        if (existing != null) return;

        GameObject bg = new GameObject("__ThemeBackground__");
        bg.transform.SetParent(targetCanvas.transform, false);
        bg.transform.SetAsFirstSibling();

        RectTransform rt = bg.AddComponent<RectTransform>();
        // Use a comfortable inset so other layout elements are not squished.
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = bg.AddComponent<Image>();
        img.color = panelBackground;
        img.raycastTarget = false; // don't block input
    }

    private void StyleButton(Button btn, Color accent, Color textColor, string label)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null) img.color = accent;

        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.color    = textColor;
            txt.fontSize = Mathf.Max(txt.fontSize, 14);
            if (!string.IsNullOrEmpty(label) && txt.text.Trim() == "")
                txt.text = label;
        }

        ColorBlock cb = btn.colors;
        cb.normalColor      = accent;
        cb.highlightedColor = Color.Lerp(accent, Color.white, 0.25f);
        cb.pressedColor     = Color.Lerp(accent, Color.black, 0.20f);
        cb.selectedColor    = accent;
        btn.colors          = cb;
    }

    private static void StyleText(TMP_Text t, Color color, float minSize)
    {
        if (t == null) return;
        t.color    = color;
        t.fontSize = Mathf.Max(t.fontSize, minSize);
    }

    private void TintUnstyledImages()
    {
        foreach (Image img in targetCanvas.GetComponentsInChildren<Image>(true))
        {
            // Skip our own background, skip images already styled, skip fully transparent.
            if (img.name == "__ThemeBackground__") continue;
            if (img.color.a < 0.01f) continue;  // fully transparent → leave alone
            if (img.color != Color.white) continue; // already coloured → leave alone

            // Only tint true white placeholders
            img.color = new Color(0.08f, 0.10f, 0.16f, 0.80f);
        }
    }

    // ── Pulse animation for listen button ─────────────────────────────────────

    private IEnumerator PulseListenButton()
    {
        while (true)
        {
            if (listenButtonImage != null && isListening)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
                Color active = Color.Lerp(dangerColor, Color.white, t * pulseAmplitude);
                listenButtonImage.color = active;
            }
            yield return null;
        }
    }

    // ── Public API (call from SpeechInputManager) ─────────────────────────────

    /// <summary>Call this when the microphone starts / stops listening.</summary>
    public void SetListeningState(bool listening)
    {
        isListening = listening;

        if (listenButtonImage != null)
            listenButtonImage.color = listening ? dangerColor : primaryAccent;

        TMP_Text label = listenButton != null
            ? listenButton.GetComponentInChildren<TMP_Text>() : null;
        if (label != null)
            label.text = listening ? "Dur" : "Dinle";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private T FindInCanvas<T>(params string[] names) where T : Component
    {
        foreach (string name in names)
        {
            Transform t = targetCanvas.transform.Find(name);
            if (t != null)
            {
                T comp = t.GetComponent<T>();
                if (comp != null) return comp;
            }
        }
        return targetCanvas.GetComponentInChildren<T>(true);
    }

    private Button FindButtonByName(params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = targetCanvas.transform.Find(name);
            if (t != null)
            {
                Button b = t.GetComponent<Button>();
                if (b != null) return b;
            }
        }
        return null;
    }

    private TMP_Text FindTMPByName(params string[] names)
    {
        foreach (string name in names)
        {
            Transform t = targetCanvas.transform.Find(name);
            if (t != null)
            {
                TMP_Text txt = t.GetComponent<TMP_Text>();
                if (txt != null) return txt;
            }
        }
        return null;
    }
}
