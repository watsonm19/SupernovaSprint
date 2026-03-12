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
    // ── Audio ─────────────────────────────────────────────────────────────────
    public AudioClip switchClip;
    public AudioClip startGameClip;

    // ── Private state ──────────────────────────────────────────────────────────

    private int         _selectedIndex;
    private bool        _onCredits;
    private AudioSource _audio;

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

        _audio             = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
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
                case 0:
                    if (startGameClip != null) _audio.PlayOneShot(startGameClip);
                    StartCoroutine(LoadGameDelayed(0.72f));
                    break;
                case 1: ShowCredits(); break;
            }
        }
    }

    private System.Collections.IEnumerator LoadGameDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(1);
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
        if (switchClip != null) _audio.PlayOneShot(switchClip);
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
            72f, FontStyles.Bold, 460f, 700f, Color.white);

        MakeLabel(panel.transform, "GameName", "Supernova Sprint",
            56f, FontStyles.Bold, 360f, 1000f, Color.white);

        MakeLabel(panel.transform, "Tagline", "A High-Speed Project by Mark Watson",
            34f, FontStyles.Normal, 270f, 1000f, new Color(0.65f, 0.65f, 0.65f));

        MakeLabel(panel.transform, "Role1", "Programming & Design  —  Mark Watson",
            30f, FontStyles.Normal, 180f, 1000f, new Color(0.5f, 0.5f, 0.5f));

        // ── Visuals & Environment ──────────────────────────────────────────────
        var catColor  = new Color(1f, 0.85f, 0.3f);
        var itemColor = new Color(0.55f, 0.55f, 0.55f);

        MakeLabel(panel.transform, "CatVisuals", "VISUALS & ENVIRONMENT",
            26f, FontStyles.Bold, 122f, 1100f, catColor);
        MakeLabel(panel.transform, "V1", "Character  —  Stylized Astronaut · PULSAR BYTES (Asset Store)",
            24f, FontStyles.Normal, 94f, 1200f, itemColor);
        MakeLabel(panel.transform, "V2", "Starfield  —  Real Stars Skybox Lite · Geoff Dallimore (Asset Store)",
            24f, FontStyles.Normal, 70f, 1200f, itemColor);
        MakeLabel(panel.transform, "V3", "Ground Textures  —  cuboy's grass pack Vol. 1 - Low poly textures *modified* · cuboy (itch.io)",
            24f, FontStyles.Normal, 46f, 1200f, itemColor);
        MakeLabel(panel.transform, "V4", "Nature Assets  —  Little Low Poly World - LITE SRP/URP · RRFreelance (Asset Store)",
            24f, FontStyles.Normal, 22f, 1200f, itemColor);
        MakeLabel(panel.transform, "V5", "Spaceship  —  FREE Low Poly Spaceships · Gastikara (Asset Store)",
            24f, FontStyles.Normal, -2f, 1200f, itemColor);

        // ── Music ─────────────────────────────────────────────────────────────
        MakeLabel(panel.transform, "CatMusic", "MUSIC",
            26f, FontStyles.Bold, -44f, 1100f, catColor);
        MakeLabel(panel.transform, "M1", "'Off The Wall (Upbeat Rock)'  —  AlexGrohl via Pixabay",
            24f, FontStyles.Normal, -72f, 1200f, itemColor);

        // ── Sound Effects ─────────────────────────────────────────────────────
        MakeLabel(panel.transform, "CatSFX", "SOUND EFFECTS",
            26f, FontStyles.Bold, -114f, 1100f, catColor);
        MakeLabel(panel.transform, "S1", "General SFX  —  Various Sound Packs · Kenney (kenney.nl)",
            24f, FontStyles.Normal, -142f, 1200f, itemColor);
        MakeLabel(panel.transform, "S2", "Earthquake SFX  —  'earth rumble' · Reitanna via Pixabay",
            24f, FontStyles.Normal, -166f, 1200f, itemColor);
        MakeLabel(panel.transform, "S3", "Time Failure SFX 1  —  'aggressive bang' · unknown via Pixabay",
            24f, FontStyles.Normal, -190f, 1200f, itemColor);
        MakeLabel(panel.transform, "S4", "Time Failure SFX 2  —  'Nuclear Explosion' · DRAGON-STUDIO via Pixabay",
            24f, FontStyles.Normal, -214f, 1200f, itemColor);

        MakeLabel(panel.transform, "BackHint", "B / ESC  Back",
            22f, FontStyles.Normal, -260f, 600f,
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
