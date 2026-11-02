using RetroRally.Core;
using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>The neon colours shared by the court, the effects and the HUD style sheet.</summary>
    public static class Palette
    {
        public static readonly Color Background = Hex("#0a0d1c");
        public static readonly Color Grid = Hex("#121a35");
        public static readonly Color Wall = Hex("#3a4a8c");
        public static readonly Color Line = Hex("#27336a");
        public static readonly Color Player1 = Hex("#38e8ff");
        public static readonly Color Player2 = Hex("#ff4f9a");
        public static readonly Color Ball = Hex("#fff4d6");
        public static readonly Color GrowColor = Hex("#63ff8f");
        public static readonly Color ShrinkColor = Hex("#ffb03b");
        public static readonly Color MultiColor = Hex("#b48cff");

        public static Color Of(Side side) => side == Side.Left ? Player1 : Player2;

        public static Color Of(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.Grow: return GrowColor;
                case PowerUpKind.Shrink: return ShrinkColor;
                default: return MultiColor;
            }
        }

        public static Color WithAlpha(this Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
    }
}
