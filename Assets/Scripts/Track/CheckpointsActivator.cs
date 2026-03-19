// ═════════════════════════════════════════════════════════════════════════════
//  CheckpointsActivator.cs
//  Attach to the parent GameObject that contains all Checkpoint objects.
//  Disables the entire group on load if the player has turned checkpoints off.
//
//  SETUP:
//    1. Place all Checkpoint GameObjects under a single parent (e.g. "Checkpoints").
//    2. Add this component to that parent.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class CheckpointsActivator : MonoBehaviour
{
    private void Awake()
    {
        if (!GameDifficulty.CheckpointsEnabled ||
            GameDifficulty.Current == GameDifficulty.RocketSprintIndex)
            gameObject.SetActive(false);
    }
}
