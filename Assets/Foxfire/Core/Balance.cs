using System;
namespace Foxfire
{
[Serializable]
public class Balance
{
    public float introSeconds = 5, bossAt = 240, maxSeconds = 300, resultsSeconds = 10;
    public float foxHP = 100, foxSpeed = 3.6f, foxRadius = .35f, immunity = .6f, attackInterval = .45f,
                 boltDamage = 10, boltSpeed = 12, attackRange = 7;
    public float sprigHP = 12, sprigSpeed = 1.7f, sprigDamage = 6, boarHP = 45, boarSpeed = 1.1f,
                 boarDamage = 14, chargeSpeed = 4.2f, chargeTell = 1, chargeDuration = 1.8f, chargeCooldown = 4;
    // Enemy ranged attacks and mushroom lunges are optional so old balance JSON
    // remains playable; EnemyCombat supplies positive fallbacks when absent or zero.
    [System.Runtime.Serialization.OptionalField]
    public float boarShotTell = .55f, boarShotCooldown = 3.8f,
                 boarProjectileSpeed = 4.2f, boarProjectileSpread = .28f;
    [System.Runtime.Serialization.OptionalField]
    public int boarProjectileCount = 3;
    [System.Runtime.Serialization.OptionalField]
    public float sprigShotTell = .45f, sprigShotCooldown = 3.8f,
                 sprigProjectileSpeed = 4.2f, sprigProjectileSpread = .24f;
    [System.Runtime.Serialization.OptionalField]
    public int sprigProjectileCount = 3;
    [System.Runtime.Serialization.OptionalField]
    public float mushroomLungeTell = .55f, mushroomLungeSpeed = 4.2f,
                 mushroomLungeDuration = .32f, mushroomLungeCooldown = 3.6f;
    public float wispHP = 22, wispSpeed = 1.2f, wispDamage = 8, wispInterval = 2.5f,
                 wispProjectileSpeed = 3.5f;
    [System.Runtime.Serialization.OptionalField]
    public float wispShotTell = .55f, wispShotCooldown = 2.8f, wispProjectileSpread = .22f;
    [System.Runtime.Serialization.OptionalField]
    public int wispProjectileCount = 3;
    [System.Runtime.Serialization.OptionalField]
    public float enemyProjectileDamageScale = .15f;
    public float bossHP = 650, bossInterval = 4, bossEnrageInterval = 3, bossProjectileSpeed = 2.8f,
                 bossDamage = 10, rootTell = 1.2f, rootDuration = 2;
    // Warden encounter tuning. Older Balance assets deserialize these as zero;
    // WardenEncounter supplies its documented defaults for non-positive values.
    [System.Runtime.Serialization.OptionalField]
    public float wardenChaseDuration = 2.6f, wardenStage2ChaseDuration = 2.2f,
                 wardenStage3ChaseDuration = 1.8f, wardenMoveSpeed = 1.4f,
                 wardenStage2MoveSpeed = 2.15f, wardenStage3MoveSpeed = 2.5f;
    [System.Runtime.Serialization.OptionalField]
    public float wardenSlashDamage = 18, wardenSlashRadius = 2.7f,
                 wardenSlashHalfAngle = .85f, wardenSlamDamage = 12,
                 wardenSpinDamage = 10;
    [System.Runtime.Serialization.OptionalField]
    public float wardenProjectileSpeed = 3.4f, wardenProjectileGapHalfAngle = .42f,
                 wardenRingSpeed = 4.8f, wardenRingMaxRadius = 30,
                 wardenRingHalfWidth = .42f, wardenRingDamage = 14;
    [System.Runtime.Serialization.OptionalField]
    public float wardenSummonTell = 1.25f, wardenSummonSafeDistance = 5.5f;
    [System.Runtime.Serialization.OptionalField]
    public int wardenProjectileCount = 14, wardenSummonCount = 8, wardenSummonCap = 16,
               wardenShockwaveCap = 6;
    [System.Runtime.Serialization.OptionalField]
    public float wardenStage2Health = .70f, wardenStage3Health = .35f;
    public float portalTell = .8f, spawnSafeDistance = 4;
    public float[] spawnRates = { .6f, .9f, 1.2f, 1.5f };
    public int[] phaseCaps = { 16, 24, 32, 40 };
    public int enemyCap = 40, projectileCap = 200, xpCap = 80, vfxCap = 60;
    public int[] thresholds = { 8, 20, 36, 56, 80, 110 };
    public float healAmount = 30, healExpiry = 12, laserDuration = 8, laserDPS = 45, starTell = 1,
                 starDamage = 70, starRadius = 3, thornRadius = 2, thornDuration = 6, thornDamage = 6,
                 rushDuration = 8, rushMultiplier = 1.25f;
    [System.Runtime.Serialization.OptionalField]
    public float championHealthMultiplier = 3;
    [System.Runtime.Serialization.OptionalField]
    public float championDamageMultiplier = 1.25f;
    // The first eight entries are the original catalog. New action entries are
    // appended so old serialized balances with a shorter array remain valid;
    // Actions.Cooldown supplies the documented fallback for those saves.
    public float[] actionCooldowns = { 20, 35, 5, 5, 5, 5, 5, 10,
                                      10, 10, 10, 10, 20, 30, 15, 12, 10 };
    public float sessionCooldown = 5, dispatchInterval = 1, queueExpiry = 10;
    public int queueCap = 5, journalCap = 10000;
}
public struct V
{
    public float x, y;
    public V(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
    public float Length
    {
        get {
            return (float)Math.Sqrt(x * x + y * y);
        }
    }
    public V Unit
    {
        get {
            float n = Length;
            return n > .0001f? this / n : new V();
        }
    }
    public static V operator +(V a, V b)
    {
        return new V(a.x + b.x, a.y + b.y);
    }
    public static V operator -(V a, V b)
    {
        return new V(a.x - b.x, a.y - b.y);
    }
    public static V operator *(V a, float b)
    {
        return new V(a.x * b, a.y * b);
    }
    public static V operator /(V a, float b)
    {
        return new V(a.x / b, a.y / b);
    }
    public static float Dist(V a, V b)
    {
        return (a - b).Length;
    }
    public static float Dot(V a, V b)
    {
        return a.x * b.x + a.y * b.y;
    }
    public static V Heading(float a)
    {
        return new V((float)Math.Cos(a), (float)Math.Sin(a));
    }
    public V Clamp()
    {
        return new V(Math.Max(-13.6f, Math.Min(13.6f, x)), Math.Max(-6.6f, Math.Min(6.6f, y)));
    }
}
public sealed class SeededRandom
{
    uint state;
    public SeededRandom(int seed)
    {
        state = (uint)seed + 1;
        if (state == 0)
            state = 1;
    }
    public float Next()
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xffffff) / 16777216f;
    }
    public int Int(int n)
    {
        return (int)(Next() * n);
    }
}
}
