namespace RetroRally.Core
{
    /// <summary>The two ends of the court: player 1 defends the left goal, player 2 the right.</summary>
    public enum Side
    {
        Left = 0,
        Right = 1,
    }

    public static class Sides
    {
        public static Side Other(this Side side) => side == Side.Left ? Side.Right : Side.Left;

        /// <summary>+1 for the left paddle (it hits towards +X), -1 for the right paddle.</summary>
        public static float Facing(this Side side) => side == Side.Left ? 1f : -1f;
    }
}
