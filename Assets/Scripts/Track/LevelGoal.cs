// ═════════════════════════════════════════════════════════════════════════════
//  LevelGoal.cs
//  Attach to the spaceship (or any end-of-level trigger volume).
//
//  SETUP:
//    1. Add a Collider to the spaceship and enable Is Trigger.
//    2. Add this component and assign the SupernovaHUD reference.
//    3. Tag the player GameObject as "Player".
//
//  ON TRIGGER:
//    • Timer stops.
//    • Game pauses (Time.timeScale = 0).
//    • Finish screen appears.
//    • A / Space restarts the scene.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class LevelGoal : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SupernovaHUD in the scene — timer is frozen when the goal is reached.")]
    public SupernovaHUD hud;

    [Header("Audio")]
    public AudioClip successResolutionClip;
    public AudioClip correctClip;

    // ── Private state ──────────────────────────────────────────────────────────

    private bool        _goalReached;
    private GameObject  _panel;
    private AudioSource _audio;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        BuildFinishScreen();

        _audio                     = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake         = false;
        _audio.spatialBlend        = 0f;
        _audio.ignoreListenerPause = true;
    }

    private void Update()
    {
        if (!_goalReached) return;

        // wasPressedThisFrame is frame-based and works even when timeScale = 0.
        bool restart = false;
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            restart = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            restart = true;

        if (restart)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    // ── Trigger ────────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (_goalReached || !other.CompareTag("Player")) return;

        _goalReached = true;

        if (hud != null) hud.StopTimer();
        foreach (var src in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (src.loop && src.isPlaying) src.Stop();
        StartCoroutine(PlaySuccessAudio());

        Time.timeScale = 0f;
        _panel.SetActive(true);
    }

    // ── Audio ──────────────────────────────────────────────────────────────────

    private IEnumerator PlaySuccessAudio()
    {
        if (successResolutionClip != null)
        {
            _audio.PlayOneShot(successResolutionClip, 2f);
            yield return new WaitForSecondsRealtime(successResolutionClip.length);
        }
        if (correctClip != null)
            _audio.PlayOneShot(correctClip, 2f);
    }

    // ── Finish Screen ──────────────────────────────────────────────────────────

    private void BuildFinishScreen()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("FinishScreen");

        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // Renders above the HUD

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Dark overlay ──────────────────────────────────────────────────────
        var bg    = new GameObject("Background");
        bg.transform.SetParent(canvasGO.transform, false);
        var img   = bg.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.80f);
        var bgRT  = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;

        // ── "SUCCESS" ─────────────────────────────────────────────────────────
        MakeLabel(canvasGO.transform, "SuccessText",
            "SUCCESS",
            fontSize:  88f,
            style:     FontStyles.Bold,
            yOffset:   120f,
            width:     1000f,
            color:     new Color(1f, 0.85f, 0f));  // gold

        // ── "Escaped the Supernova" ───────────────────────────────────────────
        MakeLabel(canvasGO.transform, "SubtitleText",
            "Escaped the Supernova",
            fontSize:  44f,
            style:     FontStyles.Normal,
            yOffset:   30f,
            width:     1000f,
            color:     Color.white);

        // ── "Restart?" prompt ─────────────────────────────────────────────────
        MakeLabel(canvasGO.transform, "RestartText",
            "Restart?   [ A  /  SPACE ]",
            fontSize:  30f,
            style:     FontStyles.Normal,
            yOffset:  -90f,
            width:     800f,
            color:     new Color(0.65f, 0.65f, 0.65f));

        _panel = canvasGO;
        _panel.SetActive(false);
    }

    private static void MakeLabel(Transform parent, string name, string text,
                                   float fontSize, FontStyles style,
                                   float yOffset, float width, Color color)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp           = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.fontStyle     = style;
        tmp.color         = color;
        tmp.alignment     = TextAlignmentOptions.Center;

        var rt                = go.GetComponent<RectTransform>();
        rt.anchorMin          = new Vector2(0.5f, 0.5f);
        rt.anchorMax          = new Vector2(0.5f, 0.5f);
        rt.pivot              = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition   = new Vector2(0f, yOffset);
        rt.sizeDelta          = new Vector2(width, fontSize * 1.4f);
    }
}
