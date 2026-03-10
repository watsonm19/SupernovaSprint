// ═════════════════════════════════════════════════════════════════════════════
//  TimeFailScreen.cs
//
//  Driven by SupernovaHUD each frame:
//    • Last 10 s: thin white border pulses in at the screen edges
//    • Time hits 0: screen fades to white, then shows FAIL + subtitle
//    • A / Space / South button → restart
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TimeFailScreen : MonoBehaviour
{
    // ── How many seconds before expiry the border starts appearing ────────────
    public float warningWindow = 10f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Image[]     _borderImages;   // 12 strips (3 layers × 4 edges)
    private float[]     _borderFactors;  // Per-strip alpha multiplier for soft gradient
    private CanvasGroup _whiteGroup;
    private GameObject  _failRoot;

    private bool _failTriggered;
    private bool _awaitingInput;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    // Awake (not Start) so _borderImages is ready before any other script's Update.
    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        if (!_awaitingInput) return;

        bool restart = false;
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame))
            restart = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            restart = true;

        if (restart)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every frame by SupernovaHUD while the countdown is running.
    /// Fades the edge border in as time runs low.
    /// </summary>
    public void OnTimerTick(float remaining)
    {
        if (_failTriggered || _borderImages == null) return;

        float t         = 1f - Mathf.Clamp01(remaining / warningWindow);
        float baseAlpha = t * 0.45f;
        for (int i = 0; i < _borderImages.Length; i++)
            _borderImages[i].color = new Color(1f, 1f, 1f, baseAlpha * _borderFactors[i]);
    }

    /// <summary>
    /// Called by SupernovaHUD when the countdown reaches zero.
    /// Starts the fade-to-white then shows the FAIL screen.
    /// </summary>
    public void TriggerFail()
    {
        if (_failTriggered) return;
        _failTriggered = true;
        StartCoroutine(FailSequence());
    }

    // ── Sequence ──────────────────────────────────────────────────────────────

    private IEnumerator FailSequence()
    {
        // Freeze gameplay immediately — time is up, no extra movement allowed
        Time.timeScale = 0f;

        // Snap border to max so the fade-to-white starts from a clear state
        if (_borderImages != null)
            for (int i = 0; i < _borderImages.Length; i++)
                _borderImages[i].color = new Color(1f, 1f, 1f, 0.45f * _borderFactors[i]);

        // Fade to white over 2 seconds using unscaled time so it plays despite timeScale = 0
        const float fadeDuration = 2f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            if (_whiteGroup != null)
                _whiteGroup.alpha = elapsed / fadeDuration;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (_whiteGroup != null) _whiteGroup.alpha = 1f;

        // Show FAIL text and wait for input
        if (_failRoot != null) _failRoot.SetActive(true);
        _awaitingInput = true;
    }

    // ── UI Builder ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGO        = new GameObject("TimeFailCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;   // Above HUD (10), below pause (50) and kill-fail (60)

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Edge border strips (3 layers per edge for soft gradient falloff) ────
        //
        //  Each edge gets 3 overlapping strips anchored to that edge.
        //  Layer 0 (outermost): thinnest,  full factor  → hard edge
        //  Layer 1 (middle):    medium,     half factor  → blends inward
        //  Layer 2 (innermost): widest,     quarter factor → soft fade
        //
        //  All strips start transparent; OnTimerTick drives alpha each frame.
        var borderRoot = new GameObject("BorderRoot");
        borderRoot.transform.SetParent(canvasGO.transform, false);
        var borderRootRT       = borderRoot.AddComponent<RectTransform>();
        borderRootRT.anchorMin = Vector2.zero;
        borderRootRT.anchorMax = Vector2.one;
        borderRootRT.sizeDelta = Vector2.zero;

        // Layer sizes (px) and alpha factors — same for every edge
        float[] sizes   = {  7f, 13f, 20f };
        float[] factors = { 1f, 0.45f, 0.18f };

        var images  = new System.Collections.Generic.List<Image>();
        var fList   = new System.Collections.Generic.List<float>();

        // Top, Bottom, Left, Right — each as (anchorMin, anchorMax, pivot, horizontal)
        var edges = new (Vector2 aMin, Vector2 aMax, Vector2 pivot, bool horiz)[]
        {
            (new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), true),
            (new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), true),
            (new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), false),
            (new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), false),
        };

        foreach (var edge in edges)
        {
            for (int layer = 0; layer < sizes.Length; layer++)
            {
                Image img;
                var rt = MakeBorderStrip(borderRoot.transform,
                    $"Layer{layer}", new Color(1f, 1f, 1f, 0f), out img);
                rt.anchorMin        = edge.aMin;
                rt.anchorMax        = edge.aMax;
                rt.pivot            = edge.pivot;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = edge.horiz
                    ? new Vector2(0f,        sizes[layer])
                    : new Vector2(sizes[layer], 0f);
                images.Add(img);
                fList.Add(factors[layer]);
            }
        }

        _borderImages  = images.ToArray();
        _borderFactors = fList.ToArray();

        // ── White overlay (fade-to-white) ─────────────────────────────────────
        var whiteGO = new GameObject("WhiteOverlay");
        whiteGO.transform.SetParent(canvasGO.transform, false);
        _whiteGroup                = whiteGO.AddComponent<CanvasGroup>();
        _whiteGroup.alpha          = 0f;
        _whiteGroup.blocksRaycasts = false;

        var whiteImg   = whiteGO.AddComponent<Image>();
        whiteImg.color = Color.white;
        var whiteRT    = whiteGO.GetComponent<RectTransform>();
        whiteRT.anchorMin = Vector2.zero;
        whiteRT.anchorMax = Vector2.one;
        whiteRT.sizeDelta = Vector2.zero;

        // ── FAIL text (shown after fade completes) ────────────────────────────
        _failRoot = new GameObject("FailRoot");
        _failRoot.transform.SetParent(canvasGO.transform, false);
        _failRoot.SetActive(false);

        MakeLabel(_failRoot.transform, "Title", "FAIL",
            120f, FontStyles.Bold, 80f, 600f, Color.red);

        MakeLabel(_failRoot.transform, "Subtitle", "Unable to escape the supernova",
            38f, FontStyles.Normal, -40f, 960f, new Color(0.12f, 0.12f, 0.12f));

        MakeLabel(_failRoot.transform, "Prompt", "A  /  SPACE  —  Restart",
            30f, FontStyles.Normal, -155f, 700f, new Color(0.38f, 0.38f, 0.38f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RectTransform MakeBorderStrip(Transform parent, string name,
                                                   Color color, out Image image)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        image       = go.AddComponent<Image>();
        image.color = color;
        return go.GetComponent<RectTransform>();
    }

    private static void MakeLabel(Transform parent, string name, string text,
        float fontSize, FontStyles style, float yOffset, float width, Color color)
    {
        var go        = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta        = new Vector2(width, fontSize * 1.5f);
    }
}
