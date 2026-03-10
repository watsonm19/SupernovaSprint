// ═════════════════════════════════════════════════════════════════════════════
//  EarthquakeIntro.cs
//  Plays a camera rumble at scene start, then repeats every interval seconds.
//  Wired up automatically by SceneBootstrapper.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;

public class EarthquakeIntro : MonoBehaviour
{
    [Tooltip("How many seconds each rumble lasts.")]
    public float duration  = 5f;

    [Tooltip("Peak shake magnitude (world units). Decays linearly to zero.")]
    public float magnitude = 0.35f;

    [Tooltip("Seconds between the end of one shake and the start of the next.")]
    public float interval  = 23f;

    private void Start()
    {
        StartCoroutine(ShakeLoop());
    }

    private IEnumerator ShakeLoop()
    {
        var cam = Object.FindFirstObjectByType<ThirdPersonCamera>();
        if (cam == null) yield break;

        while (true)
        {
            cam.StartShake(duration, magnitude);
            yield return new WaitForSeconds(duration + interval);
        }
    }
}
