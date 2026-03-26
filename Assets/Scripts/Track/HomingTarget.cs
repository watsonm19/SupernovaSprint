using System.Collections;
using UnityEngine;

public class HomingTarget : MonoBehaviour
{
    [Tooltip("Seconds before the target reappears after being hit.")]
    public float respawnDelay = 3f;

    [Tooltip("If true, hitting this target instantly recharges the player's Nova Surge.")]
    public bool rechargeSurge = false;

    private void OnHomingHit()
    {
        if (rechargeSurge)
        {
            var player = Object.FindFirstObjectByType<SupernovaSprintController>();
            if (player != null) player.RechargeSurge();
        }

        // Coroutine must run on a persistent object because disabling this
        // GameObject would pause any coroutine started on it.
        CoroutineRunner.Run(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnDelay);
        gameObject.SetActive(true);
    }
}
