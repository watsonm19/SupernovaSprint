// ═════════════════════════════════════════════════════════════════════════════
//  PauseManager.cs
//  Pause menu with Resume, Restart, Difficulty, and Controls screens.
//
//  CONTROLS (while paused):
//    ↑ / ↓  or  D-pad      → Navigate menu
//    A  /  Space  /  Enter  → Confirm selection
//    B  /  Escape           → Back / Close
//    Start  /  Escape       → Toggle pause
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    // ── Audio ─────────────────────────────────────────────────────────────────
    public AudioClip switchClip;

    // ── Private state ──────────────────────────────────────────────────────────

    private bool        _isPaused;
    private int         _screen;           // 0=menu, 1=controls, 2=difficulty
    private int         _selectedIndex;
    private AudioSource _audio;

    private GameObject         _root;
    private GameObject         _menuPanel;
    private GameObject         _controlsPanel;
    private GameObject         _difficultyPanel;
    private TextMeshProUGUI[]  _menuLabels;
    private TextMeshProUGUI[]  _difficultyLabels;

    private float _navCooldown;
    private const float NAV_DELAY = 0.18f;

    private const int SCREEN_MENU       = 0;
    private const int SCREEN_CONTROLS   = 1;
    private const int SCREEN_DIFFICULTY = 2;

    private static readonly string[] MenuItems = { "RESUME", "RESTART", "DIFFICULTY", "CONTROLS", "MAIN MENU" };
    private static readonly Color    ColorSelected   = Color.white;
    private static readonly Color    ColorUnselected = new Color(0.45f, 0.45f, 0.45f);
    private static readonly Color    ColorActive     = Color.yellow;   // current difficulty

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        _root.SetActive(false);

        _audio                     = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake         = false;
        _audio.spatialBlend        = 0f;
        _audio.ignoreListenerPause = true;
    }

    private void Update()
    {
        // ── Pause toggle ──────────────────────────────────────────────────────
        bool togglePause = false;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) togglePause = true;
        if (Gamepad.current  != null && Gamepad.current.startButton.wasPressedThisFrame) togglePause = true;

        if (togglePause)
        {
            if (_screen != SCREEN_MENU) { ShowMenu(); return; }
            SetPaused(!_isPaused);
            return;
        }

        if (!_isPaused) return;

        // ── Input ─────────────────────────────────────────────────────────────
        bool up      = false;
        bool down    = false;
        bool confirm = false;
        bool back    = false;

        _navCooldown -= Time.unscaledDeltaTime;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed   || Keyboard.current.wKey.isPressed) up      = true;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) down    = true;
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) confirm = true;
            if (Keyboard.current.backspaceKey.wasPressedThisFrame) back = true;
        }
        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.up.isPressed    || Gamepad.current.leftStick.up.isPressed)   up      = true;
            if (Gamepad.current.dpad.down.isPressed  || Gamepad.current.leftStick.down.isPressed) down    = true;
            if (Gamepad.current.buttonSouth.wasPressedThisFrame) confirm = true;
            if (Gamepad.current.buttonEast.wasPressedThisFrame)  back    = true;
        }

        // ── Sub-screen back ───────────────────────────────────────────────────
        if (_screen != SCREEN_MENU)
        {
            if (back) ShowMenu();

            // Difficulty screen: navigate and select
            if (_screen == SCREEN_DIFFICULTY)
            {
                if (_navCooldown <= 0f)
                {
                    if (up)
                    {
                        _selectedIndex = (_selectedIndex - 1 + GameDifficulty.Names.Length) % GameDifficulty.Names.Length;
                        RefreshDifficultySelection();
                        _navCooldown = NAV_DELAY;
                    }
                    else if (down)
                    {
                        _selectedIndex = (_selectedIndex + 1) % GameDifficulty.Names.Length;
                        RefreshDifficultySelection();
                        _navCooldown = NAV_DELAY;
                    }
                }

                if (confirm)
                {
                    GameDifficulty.Current = _selectedIndex;
                    // Restart so the new time limit takes effect immediately
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                }
            }

            return;
        }

        // ── Menu navigation ───────────────────────────────────────────────────
        if (_navCooldown <= 0f)
        {
            if (up)
            {
                _selectedIndex = (_selectedIndex - 1 + MenuItems.Length) % MenuItems.Length;
                RefreshMenuSelection();
                _navCooldown = NAV_DELAY;
            }
            else if (down)
            {
                _selectedIndex = (_selectedIndex + 1) % MenuItems.Length;
                RefreshMenuSelection();
                _navCooldown = NAV_DELAY;
            }
        }

        if (confirm)
        {
            switch (_selectedIndex)
            {
                case 0: SetPaused(false);    break;
                case 1: Restart();           break;
                case 2: ShowDifficulty();    break;
                case 3: ShowControls();      break;
                case 4: MainMenu();          break;
            }
        }
    }

    // ── Actions ────────────────────────────────────────────────────────────────

    private void SetPaused(bool paused)
    {
        if (paused)
        {
            _isPaused      = true;
            Time.timeScale = 0f;
            _root.SetActive(true);
            ShowMenu();
        }
        else
        {
            StartCoroutine(ResumeNextFrame());
        }
    }

    private IEnumerator ResumeNextFrame()
    {
        // Keep timeScale = 0 for the rest of this frame so GatherInput skips
        // the confirm press, then unpause on the next frame when it's gone.
        yield return null;
        _isPaused      = false;
        Time.timeScale = 1f;
        _root.SetActive(false);
    }

    private void ShowMenu()
    {
        _screen = SCREEN_MENU;
        _menuPanel.SetActive(true);
        _controlsPanel.SetActive(false);
        _difficultyPanel.SetActive(false);
        _selectedIndex = 0;
        RefreshMenuSelection();
    }

    private void ShowControls()
    {
        _screen = SCREEN_CONTROLS;
        _menuPanel.SetActive(false);
        _controlsPanel.SetActive(true);
        _difficultyPanel.SetActive(false);
    }

    private void ShowDifficulty()
    {
        _screen = SCREEN_DIFFICULTY;
        _menuPanel.SetActive(false);
        _controlsPanel.SetActive(false);
        _difficultyPanel.SetActive(true);
        _selectedIndex = GameDifficulty.Current;
        RefreshDifficultySelection();
    }

    private static void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private static void MainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    private void RefreshMenuSelection()
    {
        for (int i = 0; i < _menuLabels.Length; i++)
            _menuLabels[i].color = i == _selectedIndex ? ColorSelected : ColorUnselected;
        if (switchClip != null) _audio.PlayOneShot(switchClip);
    }

    private void RefreshDifficultySelection()
    {
        for (int i = 0; i < _difficultyLabels.Length; i++)
        {
            bool isCursor  = i == _selectedIndex;
            bool isCurrent = i == GameDifficulty.Current;
            _difficultyLabels[i].color = isCursor  ? ColorSelected
                                       : isCurrent ? ColorActive
                                       :             ColorUnselected;
        }
        if (switchClip != null) _audio.PlayOneShot(switchClip);
    }

    // ── UI Builder ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("PauseCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;   // Above HUD and finish screen

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        _root = canvasGO;

        // ── Dark overlay ──────────────────────────────────────────────────────
        var bg    = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        var img   = bg.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.78f);
        var bgRT  = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // ── Panels ────────────────────────────────────────────────────────────
        _menuPanel       = BuildMenuPanel(canvasGO.transform);
        _controlsPanel   = BuildControlsPanel(canvasGO.transform);
        _difficultyPanel = BuildDifficultyPanel(canvasGO.transform);
        _controlsPanel.SetActive(false);
        _difficultyPanel.SetActive(false);
    }

    private GameObject BuildMenuPanel(Transform parent)
    {
        var panel = new GameObject("MenuPanel");
        panel.transform.SetParent(parent, false);

        MakeLabel(panel.transform, "Mission", "Escape the planet before it explodes!",
            36f, FontStyles.Normal, 340f, 800f, Color.yellow);

        MakeLabel(panel.transform, "Title", "PAUSED",
            88f, FontStyles.Bold, 230f, 700f, Color.white);

        _menuLabels = new TextMeshProUGUI[MenuItems.Length];
        float[] yOffsets = { 100f, 0f, -100f, -200f, -300f };

        for (int i = 0; i < MenuItems.Length; i++)
            _menuLabels[i] = MakeLabel(panel.transform, MenuItems[i], MenuItems[i],
                52f, FontStyles.Normal, yOffsets[i], 500f,
                i == 0 ? ColorSelected : ColorUnselected);

        MakeLabel(panel.transform, "Hint",
            "↑ ↓  Navigate     A / SPACE  Select     START / ESC  Close",
            22f, FontStyles.Normal, -455f, 1000f,
            new Color(0.4f, 0.4f, 0.4f));

        return panel;
    }

    private GameObject BuildControlsPanel(Transform parent)
    {
        var panel = new GameObject("ControlsPanel");
        panel.transform.SetParent(parent, false);

        MakeLabel(panel.transform, "Title", "CONTROLS",
            72f, FontStyles.Bold, 230f, 900f, Color.white);

        var controls = new (string action, string input)[]
        {
            ("Move",                  "Left Stick  /  WASD"),
            ("Camera",                "Right Stick  /  Mouse"),
            ("Jump",                  "A  /  Space"),
            ("Homing Attack",         "A  /  Space  (while airborne)"),
            ("Toggle Rocket Mode",    "LB or RB  /  Shift"),
            ("Pause",                 "Start  /  Escape"),
        };

        float startY  = 120f;
        float spacing = 72f;

        for (int i = 0; i < controls.Length; i++)
        {
            float y = startY - i * spacing;

            MakeLabelAt(panel.transform, $"Action_{i}", controls[i].action,
                36f, y, -280f, 380f,
                new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Right);

            MakeLabelAt(panel.transform, $"Sep_{i}", "—",
                36f, y, 0f, 50f,
                new Color(0.35f, 0.35f, 0.35f), TextAlignmentOptions.Center);

            MakeLabelAt(panel.transform, $"Input_{i}", controls[i].input,
                36f, y, 280f, 480f,
                Color.white, TextAlignmentOptions.Left);
        }

        MakeLabel(panel.transform, "BackHint", "B / ESC  Back",
            22f, FontStyles.Normal, -290f, 600f,
            new Color(0.4f, 0.4f, 0.4f));

        return panel;
    }

    private GameObject BuildDifficultyPanel(Transform parent)
    {
        var panel = new GameObject("DifficultyPanel");
        panel.transform.SetParent(parent, false);

        MakeLabel(panel.transform, "Title", "DIFFICULTY",
            72f, FontStyles.Bold, 230f, 900f, Color.white);

        MakeLabel(panel.transform, "Sub", "Select a difficulty and press A / SPACE to apply",
            24f, FontStyles.Normal, 150f, 900f, new Color(0.5f, 0.5f, 0.5f));

        _difficultyLabels = new TextMeshProUGUI[GameDifficulty.Names.Length];
        float startY  = 60f;
        float spacing = 72f;

        for (int i = 0; i < GameDifficulty.Names.Length; i++)
        {
            float y    = startY - i * spacing;
            string label = $"{GameDifficulty.Names[i]}  —  {GameDifficulty.TimeLabels[i]}";
            bool isCurrent = i == GameDifficulty.Current;

            _difficultyLabels[i] = MakeLabel(panel.transform, $"Diff_{i}", label,
                48f, FontStyles.Normal, y, 800f,
                isCurrent ? ColorActive : ColorUnselected);
        }

        MakeLabel(panel.transform, "BackHint", "B / ESC  Back     A / SPACE  Apply & Restart",
            22f, FontStyles.Normal, -290f, 900f,
            new Color(0.4f, 0.4f, 0.4f));

        return panel;
    }

    // ── Label helpers ──────────────────────────────────────────────────────────

    private static TextMeshProUGUI MakeLabel(Transform parent, string name, string text,
        float fontSize, FontStyles style, float yOffset, float width, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp        = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = text;
        tmp.fontSize   = fontSize;
        tmp.fontStyle  = style;
        tmp.color      = color;
        tmp.alignment  = TextAlignmentOptions.Center;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta        = new Vector2(width, fontSize * 1.5f);

        return tmp;
    }

    private static void MakeLabelAt(Transform parent, string name, string text,
        float fontSize, float yOffset, float xOffset, float width,
        Color color, TextAlignmentOptions alignment)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp        = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = text;
        tmp.fontSize   = fontSize;
        tmp.fontStyle  = FontStyles.Normal;
        tmp.color      = color;
        tmp.alignment  = alignment;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(xOffset, yOffset);
        rt.sizeDelta        = new Vector2(width, fontSize * 1.5f);
    }
}
