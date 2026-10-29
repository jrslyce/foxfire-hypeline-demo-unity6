using System;
namespace Foxfire
{
// Pure deterministic tuning and fan geometry shared by the simulation and
// development fixtures. No random state or renderer behavior belongs here.
public static class EnemyCombat
{
    public static float Positive(float value, float fallback)
    {
        return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
    }

    public static int Positive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    public static float ShotTell(Balance b, EnemyKind kind)
    {
        return kind == EnemyKind.Boar ? Positive(b.boarShotTell, .55f) :
               kind == EnemyKind.Sprig ? Positive(b.sprigShotTell, .45f) :
               Positive(b.wispShotTell, .55f);
    }

    public static float ShotCooldown(Balance b, EnemyKind kind)
    {
        return kind == EnemyKind.Boar ? Positive(b.boarShotCooldown, 3.8f) :
               kind == EnemyKind.Sprig ? Positive(b.sprigShotCooldown, 3.8f) :
               Positive(b.wispShotCooldown, 2.8f);
    }

    public static float ProjectileSpeed(Balance b, EnemyKind kind)
    {
        return kind == EnemyKind.Boar ? Positive(b.boarProjectileSpeed, 4.2f) :
               kind == EnemyKind.Sprig ? Positive(b.sprigProjectileSpeed, 4.2f) :
               Positive(b.wispProjectileSpeed, 3.5f);
    }

    public static float ProjectileSpread(Balance b, EnemyKind kind)
    {
        return kind == EnemyKind.Boar ? Positive(b.boarProjectileSpread, .28f) :
               kind == EnemyKind.Sprig ? Positive(b.sprigProjectileSpread, .24f) :
               Positive(b.wispProjectileSpread, .22f);
    }

    public static int ProjectileCount(Balance b, EnemyKind kind)
    {
        int count = kind == EnemyKind.Boar ? Positive(b.boarProjectileCount, 3) :
                    kind == EnemyKind.Sprig ? Positive(b.sprigProjectileCount, 3) :
                    Positive(b.wispProjectileCount, 3);
        return Math.Max(1, Math.Min(5, count));
    }

    public static float LungeTell(Balance b)
    {
        return Positive(b.mushroomLungeTell, .55f);
    }

    public static float LungeSpeed(Balance b)
    {
        return Positive(b.mushroomLungeSpeed, 4.2f);
    }

    public static float LungeDuration(Balance b)
    {
        return Positive(b.mushroomLungeDuration, .32f);
    }

    public static float LungeCooldown(Balance b)
    {
        return Positive(b.mushroomLungeCooldown, 3.6f);
    }

    public static float ProjectileDamageScale(Balance b)
    {
        return Positive(b.enemyProjectileDamageScale, .15f);
    }

    public static V FanVelocity(V direction, int index, int count, float spread, float speed)
    {
        float center = (float)Math.Atan2(direction.y, direction.x);
        float offset = count <= 1 ? 0 : ((float)index / (count - 1) * 2 - 1) * spread;
        return V.Heading(center + offset) * speed;
    }
}
}
