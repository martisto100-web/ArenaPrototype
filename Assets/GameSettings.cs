using UnityEngine;

// Player-facing FX settings. There's no settings-menu UI yet — these are the
// hooks a menu will flip later. Backed by PlayerPrefs so a choice sticks
// between runs.
public static class GameSettings
{
    private const string ShakeKey = "fx.screenShakeOnElimination";
    private const string FlashKey = "fx.screenFlashOnElimination";
    private const string HitVolKey = "fx.bulletHitVolume";

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

    // Loudness of the "your car got hit" metal thud.
    // Gameplay reads BulletHitVolume, a raw AudioSource volume in 0..BulletHitVolumeMax
    // (0 = off). The pause/settings menu binds to BulletHitVolumePercent instead,
    // a 0..100 slider where 100 maps to BulletHitVolumeMax. Both are views of the
    // same stored value; both clamp so nothing pushes past the ceiling.
    public const string BulletHitVolumeLabel = "Projectile Impact Volume"; // settings-menu slider label
    public const float BulletHitVolumeMax = 0.4f;
    private const float BulletHitVolumeDefault = 0.23f; // ~57.5 on the 0..100 slider

    public static float BulletHitVolume
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat(HitVolKey, BulletHitVolumeDefault), 0f, BulletHitVolumeMax);
        set => PlayerPrefs.SetFloat(HitVolKey, Mathf.Clamp(value, 0f, BulletHitVolumeMax));
    }

    public static float BulletHitVolumePercent
    {
        get => BulletHitVolume / BulletHitVolumeMax * 100f;
        set => BulletHitVolume = value / 100f * BulletHitVolumeMax;
    }
}
