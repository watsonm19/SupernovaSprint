// ═════════════════════════════════════════════════════════════════════════════
//  TitleScreen.cs
//  Main menu with Start Game and Credits options.
//
//  CONTROLS:
//    ↑ / ↓  or  D-pad      → Navigate
//    A  /  Space  /  Enter  → Confirm
//    B  /  Escape           → Back (from Credits)
// ═════════════════════════════════════════════════════════════════════════════

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleScreen : MonoBehaviour
{
    // ── Private state ──────────────────────────────────────────────────────────

    private int  _selectedIndex;
    private bool _onCredits;

    private GameObject        _titlePanel;
    private GameObject        _creditsPanel;
    private TextMeshProUGUI[] _menuLabels;

    private float _navCooldown;
    private const float NAV_DELAY = 0.18f;

    private static readonly string[] MenuItems      = { "START GAME", "CREDITS" };
    private static readonly Color    ColorSelected   = Color.white;
    private static readonly Color    ColorUnselected = new Color(0.45f, 0.45f, 0.45f);

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildUI();
    }

    private void Update()
    {
        bool back    = false;
        bool up      = false;
        bool down    = false;
        bool confirm = false;

        _navCooldown -= Time.deltaTime;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame ||
                Keyboard.current.backspaceKey.wasPressedThisFrame) back = true;
            if (Keyboard.current.upArrowKey.isPressed   || Keyboard.current.wKey.isPressed) up   = true;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) down = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame) confirm = true;
        }
        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonEast.wasPressedThisFrame) back = true;
            if (Gamepad.current.dpad.up.isPressed    || Gamepad.current.leftStick.up.isPressed)   up   = true;
            if (Gamepad.current.dpad.down.isPressed  || Gamepad.current.leftStick.down.isPressed) down = true;
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) confirm = true;
        }

        // ── Credits screen ────────────────────────────────────────────────────
        if (_onCredits)
        {
            if (back) ShowTitle();
            return;
        }

        // ── Title menu navigation ─────────────────────────────────────────────
        if (_navCooldown <= 0f)
        {
            if (up)
            {
                _selectedIndex = (_selectedIndex - 1 + MenuItems.Length) % MenuItems.Length;
                RefreshSelection();
                _navCooldown = NAV_DELAY;
            }
            else if (down)
            {
                _selectedIndex = (_selectedIndex + 1) % MenuItems.Length;
                RefreshSelection();
                _navCooldown = NAV_DELAY;
            }
        }

        if (confirm)
        {
            switch (_selectedIndex)
            {
                case 0: SceneManager.LoadScene(1); break;
                case 1: ShowCredits();             break;
            }
        }
    }

    // ── Screen switching ───────────────────────────────────────────────────────

    private void ShowTitle()
    {
        _onCredits = false;
        _titlePanel.SetActive(true);
        _creditsPanel.SetActive(false);
        _selectedIndex = 0;
        RefreshSelection();
    }

    private void ShowCredits()
    {
        _onCredits = true;
        _titlePanel.SetActive(false);
        _creditsPanel.SetActive(true);
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < _menuLabels.Length; i++)
            _menuLabels[i].color = i == _selectedIndex ? ColorSelected : ColorUnselected;
    }

    // ── UI Builder ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Ensure the camera shows black
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        var canvasGO        = new GameObject("TitleCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Solid black background
        var bg    = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        bg.AddComponent<Image>().color = Color.black;
        var bgRT  = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        _titlePanel   = BuildTitlePanel(canvasGO.transform);
        _creditsPanel = BuildCreditsPanel(canvasGO.transform);
        _creditsPanel.SetActive(false);
    }

    private GameObject BuildTitlePanel(Transform parent)
    {
        var panel = new GameObject("TitlePanel");
        panel.transform.SetParent(parent, false);

        MakeLabel(panel.transform, "GameTitle", "Supernova Sprint",
            110f, FontStyles.Bold, 130f, 1300f, Color.white);

        MakeLabel(panel.transform, "Subtitle", "A fast-paced, low-poly 3D platformer",
            34f, FontStyles.Normal, 30f, 900f, new Color(0.55f, 0.55f, 0.55f));

        _menuLabels = new TextMeshProUGUI[MenuItems.Length];
        float[] yOffsets = { -120f, -210f };
        for (int i = 0; i < MenuItems.Length; i++)
            _menuLabels[i] = MakeLabel(panel.transform, MenuItems[i], MenuItems[i],
                52f, FontStyles.Normal, yOffsets[i], 500f,
                i == 0 ? ColorSelected : ColorUnselected);

        MakeLabel(panel.transform, "Hint",
            "↑ ↓  Navigate     A / SPACE  Select",
            22f, FontStyles.Normal, -340f, 800f,
            new Color(0.28f, 0.28f, 0.28f));

        return panel;
    }

    private GameObject BuildCreditsPanel(Transform parent)
    {
        var panel = new GameObject("CreditsPanel");
        panel.transform.SetParent(parent, false);

        MakeLabel(panel.transform, "Header", "Credits",
            72f, FontStyles.Bold, 240f, 700f, Color.white);

        MakeLabel(panel.transform, "GameName", "Supernova Sprint",
            56f, FontStyles.Bold, 120f, 1000f, Color.white);

        MakeLabel(panel.transform, "Tagline", "A High-Speed Project by Mark Watson",
            34f, FontStyles.Normal, 30f, 1000f, new Color(0.65f, 0.65f, 0.65f));

        MakeLabel(panel.transform, "Role1", "Programming & Design  —  Mark Watson",
            30f, FontStyles.Normal, -60f, 1000f, new Color(0.5f, 0.5f, 0.5f));

        MakeLabel(panel.transform, "BackHint", "B / ESC  Back",
            22f, FontStyles.Normal, -320f, 600f,
            new Color(0.28f, 0.28f, 0.28f));

        return panel;
    }

    // ── Label helper ───────────────────────────────────────────────────────────

    private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
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
        return tmp;
    }
}
