// ═════════════════════════════════════════════════════════════════════════════
//  CheckpointRespawnVFX.cs
//  Small white particles that drift upward from the bottom 1/3 of the screen
//  during the checkpoint respawn fade-in. Spawned as UI Image quads so they
//  sit on the same overlay canvas as the fade.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CheckpointRespawnVFX : MonoBehaviour
{
    [Header("Particles")]
    [Tooltip("Number of particles spawned per Play() call.")]
    public int count = 200;

    [Tooltip("Min/max particle size in canvas units.")]
    public float minSize = 6f;
    public float maxSize = 14f;

    [Tooltip("Min/max upward drift speed in canvas units per second.")]
    public float minSpeed = 150f;
    public float maxSpeed = 320f;

    [Tooltip("How long each particle lives (seconds).")]
    public float lifetime = 0.9f;

    [Tooltip("Particles are spawned over this fraction of their lifetime.")]
    public float spawnWindow = 0.2f;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Play() => StartCoroutine(SpawnRoutine());

    // ── Internals ─────────────────────────────────────────────────────────────

    private IEnumerator SpawnRoutine()
    {
        float interval = (lifetime * spawnWindow) / count;
        for (int i = 0; i < count; i++)
        {
            SpawnParticle();
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnParticle()
    {
        var go  = new GameObject("RespawnParticle");
        go.transform.SetParent(transform, false);

        var img   = go.AddComponent<Image>();
        img.color = Color.white;

        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = new Vector2(0.5f, 0.5f);
        rt.anchorMax  = new Vector2(0.5f, 0.5f);
        rt.pivot      = new Vector2(0.5f, 0.5f);

        float size   = Random.Range(minSize, maxSize);
        rt.sizeDelta = new Vector2(size, size);

        // Spawn randomly across the full width, within the bottom 1/3 of screen.
        // Reference resolution is 1920×1080 — half-extents from center anchor:
        //   x: -960 → 960,  bottom third y: -540 → -180
        rt.anchoredPosition = new Vector2(
            Random.Range(-960f, 960f),
            Random.Range(-540f, -180f));

        StartCoroutine(AnimateParticle(rt, img, Random.Range(minSpeed, maxSpeed)));
    }

    private IEnumerator AnimateParticle(RectTransform rt, Image img, float speed)
    {
        Vector2 startPos = rt.anchoredPosition;
        float   elapsed  = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;

            rt.anchoredPosition = startPos + Vector2.up * (speed * elapsed);

            // Fade in quickly, then ease out
            float alpha = t < 0.15f
                ? t / 0.15f
                : Mathf.Pow(1f - (t - 0.15f) / 0.85f, 1.5f);
            img.color = new Color(1f, 1f, 1f, alpha);

            yield return null;
        }

        Destroy(rt.gameObject);
    }
}
