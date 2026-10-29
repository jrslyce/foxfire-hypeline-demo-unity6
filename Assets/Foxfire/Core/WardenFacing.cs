using System;

namespace Foxfire
{
// Pure direction selection shared by the deterministic presentation contract and the Unity atlas.
public static class WardenFacing
{
    public const int DirectionCount = 8;

    public static int DirectionIndex(V direction)
    {
        if (direction.Length <= .0001f)
            return 0;
        double degrees = Math.Atan2(direction.y, direction.x) * 180 / Math.PI;
        degrees = (degrees % 360 + 360) % 360;
        return ((int)Math.Floor(degrees / (360.0 / DirectionCount) + .5) % DirectionCount + DirectionCount) % DirectionCount;
    }
}
}
