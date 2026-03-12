// ═════════════════════════════════════════════════════════════════════════════
//  FailScreen.cs
//  Shown when the player falls off the level.
//  Call Show() to display it. Press A / Space / Enter to restart.
// ═════════════════════════════════════════════════════════════════════════════

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FailScreen : MonoBehaviour
{
    public AudioClip fallFailClip;

    private GameObject  _root;
    private bool        _isShowing;
    private AudioSource _audio;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        _root.SetActive(false);

        _audio                      = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake          = false;
        _audio.spatialBlend         = 0f;
        _audio.ignoreListenerPause  = true;
    }

    private void Update()
    {
        if (!_isShowing) return;

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
    /// Pauses the game and displays the fail screen.
    /// Safe to call more than once — subsequent calls are ignored.
    /// </summary>
    public void Show()
    {
        if (_isShowing) return;
        _isShowing     = true;
        Time.timeScale = 0f;
        _root.SetActive(true);
        if (fallFailClip != null) _audio.PlayOneShot(fallFailClip, 2f);
    }

    // ── UI Builder ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGO = new GameObject("FailCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;   // Above HUD, pause menu, and finish screen

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        _root = canvasGO;

        // Dark overlay
        var bg    = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        var img   = bg.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.85f);
        var bgRT  = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // "FAIL"
        MakeLabel(canvasGO.transform, "FailTitle", "FAIL",
            120f, FontStyles.Bold, 80f, 600f, Color.red);

        // Message
        MakeLabel(canvasGO.transform, "FailMessage",
            "You fell off the ledge and cannot get back to your ship",
            36f, FontStyles.Normal, -40f, 960f,
            new Color(0.85f, 0.85f, 0.85f));

        // Restart prompt
        MakeLabel(canvasGO.transform, "RestartPrompt",
            "A  /  SPACE  —  Restart",
            30f, FontStyles.Normal, -150f, 700f,
            new Color(0.45f, 0.45f, 0.45f));
    }

    private static void MakeLabel(Transform parent, string name, string text,
        float fontSize, FontStyles style, float yOffset, float width, Color color)
    {
        var go  = new GameObject(name);
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
