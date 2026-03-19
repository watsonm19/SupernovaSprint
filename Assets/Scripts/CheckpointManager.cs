// ═════════════════════════════════════════════════════════════════════════════
//  CheckpointManager.cs
//  Static registry for the last activated checkpoint.
//  Automatically resets at the start of every scene load so a fresh run
//  always begins with no active checkpoint.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.SceneManagement;

public static class CheckpointManager
{
    public static bool HasCheckpoint =>
        _hasCheckpoint && GameDifficulty.Current != GameDifficulty.RocketSprintIndex;

    private static bool _hasCheckpoint;
    public static Vector3    Position      { get; private set; }
    public static Quaternion Rotation      { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        _hasCheckpoint = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _hasCheckpoint = false;
    }

    /// <summary>Saves a new checkpoint. Called by Checkpoint.cs on player contact.</summary>
    public static void Set(Vector3 position, Quaternion rotation)
    {
        _hasCheckpoint = true;
        Position       = position;
        Rotation       = rotation;
    }
}
