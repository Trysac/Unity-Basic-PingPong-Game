using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>Sprites drawn in code at start-up, so the game ships without image files.</summary>
    public static class Shapes
    {
        static Sprite square;
        static Sprite circle;
        static Sprite softDot;
        static Sprite ring;

        /// <summary>A 1x1 unit white square.</summary>
        public static Sprite Square => square ? square : square = Make("Square", 8, (x, y) => 1f);

        /// <summary>A 1 unit wide white disc with an anti-aliased edge.</summary>
        public static Sprite Circle => circle ? circle : circle = Make("Circle", 64, (x, y) =>
            Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 32f));

        /// <summary>A soft round glow that fades to nothing at its edge.</summary>
        public static Sprite SoftDot => softDot ? softDot : softDot = Make("SoftDot", 64, (x, y) =>
        {
            float a = Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y));
            return a * a;
        });

        /// <summary>A thin ring, for the power-up halo.</summary>
        public static Sprite Ring => ring ? ring : ring = Make("Ring", 96, (x, y) =>
        {
            float d = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 0.88f);
            return Mathf.Clamp01((0.08f - d) * 40f);
        });

        /// <summary>Renders <paramref name="alpha"/> over [-1, 1] squared into a white sprite one unit across.</summary>
        static Sprite Make(string name, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha(u, v));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
