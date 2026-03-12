// ═════════════════════════════════════════════════════════════════════════════
//  MissionStatement.cs
//  Displays a mission statement at the top of the screen on level start.
//  Fades out after a few seconds — game is never paused.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionStatement : MonoBehaviour
{
    [Tooltip("How long the text stays fully visible before fading.")]
    public float holdTime  = 2f;

    [Tooltip("How long the fade-out takes.")]
    public float fadeTime  = 1f;

    private CanvasGroup _group;

    private void Awake()
    {
        BuildUI();
    }

    private void Start()
    {
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(holdTime);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            _group.alpha = 1f - elapsed / fadeTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        _group.alpha = 0f;
        Destroy(gameObject);
    }

    private void BuildUI()
    {
        var canvasGO        = new GameObject("MissionStatementCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // Above nothing, below HUD

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        _group             = canvasGO.AddComponent<CanvasGroup>();
        _group.alpha       = 1f;
        _group.blocksRaycasts = false;

        var go  = new GameObject("MissionText");
        go.transform.SetParent(canvasGO.transform, false);

        var tmp           = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = "Find your ship before the SUPERNOVA!";
        tmp.fontSize      = 44f;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.color         = Color.white;
        tmp.alignment     = TextAlignmentOptions.Center;

        var rt            = go.GetComponent<RectTransform>();
        rt.anchorMin      = new Vector2(0f, 1f);
        rt.anchorMax      = new Vector2(1f, 1f);
        rt.pivot          = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta      = new Vector2(0f, 80f);
    }
}
