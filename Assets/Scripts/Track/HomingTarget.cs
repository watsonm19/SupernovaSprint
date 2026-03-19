using System.Collections;
using UnityEngine;

public class HomingTarget : MonoBehaviour
{
    [Tooltip("Seconds before the target reappears after being hit.")]
    public float respawnDelay = 3f;

    private void OnHomingHit()
    {
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
