using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>Macro layout of the generated world (chosen before generating).</summary>
    public enum WorldType
    {
        Islands,     // central landmass ringed by sea, deep sea at the far edges
        Continental, // noise-driven landmasses that can run off the map edges
        Lakes,       // mostly land with scattered lakes and mountain ranges
    }

    /// <summary>Inputs to <see cref="WorldMap.Generate"/> (filled from the generator component).</summary>
    public struct WorldGenSettings
    {
        public int seed;
        public int width;
        public int height;
        public WorldType worldType;

        public float noiseScale;   // larger = bigger, smoother features
        public int octaves;        // fractal detail layers
        public float persistence;  // amplitude falloff per octave
        public float lacunarity;   // frequency growth per octave
        public float islandFalloff; // Islands only: higher = larger landmass

        // Ascending elevation thresholds in [0,1]; everything above hillLevel is Mountain.
        public float deepSeaLevel;
        public float seaLevel;
        public float beachLevel;
        public float plainLevel;
        public float hillLevel;
    }

    /// <summary>
    /// Pure (scene-independent) procedural terrain map. Produces a grid of
    /// <see cref="TerrainType"/> from a seed using fractal Perlin noise, a
    /// world-type shaping pass, and elevation-band classification.
    /// </summary>
    public class WorldMap
    {
        public int Width { get; }
        public int Height { get; }

        private readonly TerrainType[] _cells;

        private WorldMap(int width, int height, TerrainType[] cells)
        {
            Width = width;
            Height = height;
            _cells = cells;
        }

        public TerrainType Get(int x, int y) =>
            (x >= 0 && x < Width && y >= 0 && y < Height) ? _cells[y * Width + x] : TerrainType.DeepSea;

        public static WorldMap Generate(WorldGenSettings s)
        {
            int w = Mathf.Max(8, s.width);
            int h = Mathf.Max(8, s.height);
            int octaves = Mathf.Max(1, s.octaves);
            float scale = Mathf.Max(0.01f, s.noiseScale);

            // Mathf.PerlinNoise isn't seedable, so we offset the sample coordinates per
            // octave with seeded pseudo-random offsets.
            var rng = new System.Random(s.seed);
            var offX = new float[octaves];
            var offY = new float[octaves];
            for (int o = 0; o < octaves; o++)
            {
                offX[o] = (float)rng.NextDouble() * 200000f - 100000f;
                offY[o] = (float)rng.NextDouble() * 200000f - 100000f;
            }

            // Pass 1: fractal Brownian motion height, tracking range for normalization.
            var raw = new float[w * h];
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float amp = 1f, freq = 1f, sum = 0f, ampSum = 0f;
                for (int o = 0; o < octaves; o++)
                {
                    float sx = (x / scale) * freq + offX[o];
                    float sy = (y / scale) * freq + offY[o];
                    sum += Mathf.PerlinNoise(sx, sy) * amp;
                    ampSum += amp;
                    amp *= s.persistence;
                    freq *= s.lacunarity;
                }
                float v = sum / Mathf.Max(0.0001f, ampSum);
                raw[y * w + x] = v;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            // Pass 2: normalize to [0,1], apply world shaping, classify into bands.
            float invRange = 1f / Mathf.Max(0.0001f, max - min);
            var cells = new TerrainType[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = (raw[y * w + x] - min) * invRange;
                float nx = w > 1 ? x / (float)(w - 1) : 0.5f;
                float ny = h > 1 ? y / (float)(h - 1) : 0.5f;
                v = Shape(v, nx, ny, s);
                cells[y * w + x] = Classify(v, s);
            }

            return new WorldMap(w, h, cells);
        }

        /// <summary>Bends the normalized height according to the chosen world type.</summary>
        private static float Shape(float v, float nx, float ny, WorldGenSettings s)
        {
            switch (s.worldType)
            {
                case WorldType.Islands:
                {
                    // Radial mask: 1 at the center, falling to 0 at the edges/corners,
                    // so the landmass is ringed by sea and deep sea at the borders.
                    float dx = nx * 2f - 1f;
                    float dy = ny * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float mask = 1f - Mathf.Pow(Mathf.Clamp01(d), Mathf.Max(0.1f, s.islandFalloff));
                    return v * Mathf.Clamp01(mask);
                }
                case WorldType.Lakes:
                    // Push most cells onto land; only local minima stay below sea level.
                    return Mathf.Clamp01(v * 0.65f + 0.4f);
                case WorldType.Continental:
                default:
                    // Open coast: pure noise, land can reach the map edges.
                    return v;
            }
        }

        private static TerrainType Classify(float v, WorldGenSettings s)
        {
            if (v < s.deepSeaLevel) return TerrainType.DeepSea;
            if (v < s.seaLevel) return TerrainType.Sea;
            if (v < s.beachLevel) return TerrainType.Beach;
            if (v < s.plainLevel) return TerrainType.Plain;
            if (v < s.hillLevel) return TerrainType.Hill;
            return TerrainType.Mountain;
        }
    }
}
