// ═════════════════════════════════════════════════════════════════════════════
//  Checkpoint.cs
//  Place anywhere on the track. When the player walks/runs through it,
//  their current position is saved to CheckpointManager.
//  If they fall off the ledge afterwards they respawn here instead of dying.
//
//  SETUP:
//    1. Create a GameObject with a Box/Sphere Collider — enable Is Trigger.
//    2. Add this component.
//    3. Optionally add a Renderer for a visual indicator (it turns cyan on activate).
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Tooltip("Color shown on the checkpoint's Renderer before it is activated.")]
    public Color inactiveColor = new Color(1f, 0.8f, 0f);   // gold

    [Tooltip("Color shown on the checkpoint's Renderer after it is activated.")]
    public Color activeColor   = Color.cyan;

    private bool     _activated;
    private Renderer _renderer;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
            _renderer.material.color = inactiveColor;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_activated || !other.CompareTag("Player")) return;
        _activated = true;

        // Save the player's exact position so the respawn point feels natural.
        CheckpointManager.Set(other.transform.position, other.transform.rotation);

        if (_renderer != null)
            _renderer.material.color = activeColor;

        Debug.Log($"[Checkpoint] Activated: {gameObject.name}");
    }
}
