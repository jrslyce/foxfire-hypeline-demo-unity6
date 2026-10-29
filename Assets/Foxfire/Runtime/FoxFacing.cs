using System;

namespace Foxfire
{
    // Visual facing only: movement, aiming and collisions keep their simulation heading.
    public sealed class FoxFacing
    {
        public const int DirectionCount = 32;
        const double FullTurn = 2 * Math.PI;
        const double Sector = FullTurn / DirectionCount;
        const double TurnRate = 2 * Math.PI; // 360 degrees/second; a reversal spans ~15 streamed frames.
        const double Hysteresis = Math.PI / 180; // One degree beyond a cell boundary.
        double angle = -Math.PI / 2;
        int direction = 3 * DirectionCount / 4;
        bool initialized;

        public int Sample(double elapsed, float x, float y, bool moving)
        {
            if (!moving || (x == 0 && y == 0)) return direction;
            double target = Math.Atan2(y, x);
            if (!initialized)
            {
                angle = target;
                direction = DirectionIndex(angle);
                initialized = true;
                return direction;
            }
            double delta = Wrap(target - angle);
            double step = Math.Max(0, elapsed) * TurnRate;
            angle = Wrap(angle + Math.Max(-step, Math.Min(step, delta)));
            // Keep small AI heading fluctuations from flickering between neighboring views.
            if (Math.Abs(Wrap(angle - direction * Sector)) > Sector / 2 + Hysteresis)
                direction = DirectionIndex(angle);
            return direction;
        }

        public static int DirectionIndex(double radians) =>
            ((int)Math.Floor(Wrap(radians) / Sector + .5) + DirectionCount) % DirectionCount;

        static double Wrap(double radians) =>
            radians - FullTurn * Math.Floor((radians + Math.PI) / FullTurn);
    }
}
