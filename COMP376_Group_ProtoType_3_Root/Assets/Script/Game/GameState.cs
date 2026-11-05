/// <summary>
/// Tiny shared flag so player scripts know when the game is on a menu (intro, pause, win)
/// and should ignore input. Set by CheeseGameManager.
/// </summary>
public static class GameState
{
    public static bool InputBlocked { get; set; }
}
