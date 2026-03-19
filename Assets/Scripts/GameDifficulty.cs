// ═════════════════════════════════════════════════════════════════════════════
//  GameDifficulty.cs
//  Static difficulty registry — persists across scene reloads.
//  Read by SupernovaHUD (time limit) and PauseManager (difficulty screen).
// ═════════════════════════════════════════════════════════════════════════════

public static class GameDifficulty
{
    public const  int      RocketSprintIndex = 5;
    public static readonly string[] Names      = { "Stroll", "Jog", "Run", "Sprint", "Super Sprint", "Rocket Sprint" };
    public static readonly float[]  TimeLimits = { 1800f, 150f, 130f, 120f, 116f, 113f };
    public static readonly string[] TimeLabels = { "30:00", "2:30", "2:10", "2:00", "1:56", "1:53" };

    /// <summary>Index of the currently selected difficulty (1 = Jog).</summary>
    public static int  Current            = 1;
    public static bool CheckpointsEnabled = true;

    public static float  TimeLimit => TimeLimits[Current];
    public static string Name      => Names[Current];
}
