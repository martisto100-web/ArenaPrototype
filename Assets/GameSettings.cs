using UnityEngine;

// Player-facing FX toggles. There's no settings-menu UI yet — these are the
// hooks a menu will flip later. Backed by PlayerPrefs so a choice sticks
// between runs. Both default ON.
public static class GameSettings
{
    private const string ShakeKey = "fx.screenShakeOnElimination";
    private const string FlashKey = "fx.screenFlashOnElimination";

    public static bool ScreenShakeOnElimination
    {
        get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
        set => PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
    }

    public static bool ScreenFlashOnElimination
    {
        get => PlayerPrefs.GetInt(FlashKey, 1) == 1;
        set => PlayerPrefs.SetInt(FlashKey, value ? 1 : 0);
    }
}
