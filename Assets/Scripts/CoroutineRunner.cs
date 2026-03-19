// ═════════════════════════════════════════════════════════════════════════════
//  CoroutineRunner.cs
//  Persistent singleton that can run coroutines on behalf of objects that
//  may be disabled (e.g. homing targets waiting to respawn).
//  Created automatically on first use — no scene setup needed.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;

public class CoroutineRunner : MonoBehaviour
{
    private static CoroutineRunner _instance;

    private static CoroutineRunner Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[CoroutineRunner]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CoroutineRunner>();
            }
            return _instance;
        }
    }

    public static void Run(IEnumerator coroutine)
    {
        Instance.StartCoroutine(coroutine);
    }
}
