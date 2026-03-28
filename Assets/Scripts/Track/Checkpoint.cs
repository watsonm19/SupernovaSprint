// ═════════════════════════════════════════════════════════════════════════════
//  Checkpoint.cs
//  Place anywhere on the track. When the player walks/runs through it,
//  their current position is saved to CheckpointManager.
//  If they fall off the ledge afterwards they respawn here instead of dying.
//
//  SETUP:
//    1. Create a GameObject with a Box/Sphere Collider — enable Is Trigger.
//    2. Add this component.
//    3. Assign Respawn Point to an empty child GameObject placed where the player
//       should appear after dying. If left empty, falls back to the player's
//       position at the moment they hit the checkpoint.
//    4. Optionally add a Renderer for a visual indicator (it turns cyan on activate).
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Tooltip("Where the player spawns after dying past this checkpoint. " +
             "Assign an empty child GameObject. If left empty, uses the player's position at activation.")]
    public Transform respawnPoint;

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

        if (respawnPoint != null)
            CheckpointManager.Set(respawnPoint.position, respawnPoint.rotation);
        else
            CheckpointManager.Set(other.transform.position, other.transform.rotation);

        if (_renderer != null)
            _renderer.material.color = activeColor;

        Debug.Log($"[Checkpoint] Activated: {gameObject.name}");
    }
}
