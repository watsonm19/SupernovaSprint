// ═════════════════════════════════════════════════════════════════════════════
//  GameDifficulty.cs
//  Static difficulty registry — persists across scene reloads.
//  Read by SupernovaHUD (time limit) and PauseManager (difficulty screen).
// ═════════════════════════════════════════════════════════════════════════════

public static class GameDifficulty
{
    public static readonly string[] Names      = { "Jog", "Run", "Sprint", "Super Sprint", "Rocket Sprint" };
    public static readonly float[]  TimeLimits = { 138f, 127f, 119f, 115f, 113f };
    public static readonly string[] TimeLabels = { "2:18", "2:07", "1:59", "1:55", "1:53" };

    /// <summary>Index of the currently selected difficulty (0 = Jog).</summary>
    public static int Current = 0;

    public static float  TimeLimit => TimeLimits[Current];
    public static string Name      => Names[Current];
}
