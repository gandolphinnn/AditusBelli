using UnityEngine;

namespace AditusBelli.Map
{
    /// <summary>
    /// Macro layout of the generated world (chosen before generating). Each type is a
    /// shaping pass in <see cref="WorldMap"/>; add a new shape by adding an enum value and
    /// a case there (plus a recipe in <see cref="WorldRecipes"/>).
    /// </summary>
    public enum WorldType
    {
        Pangea,     // a single large landmass; water only where the noise dips below sea level
        BigIslands, // several large islands separated by open sea (a low-frequency mask carves them)
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
        public float islandFalloff; // radial edge falloff (higher = larger landmass; map border stays sea)
        public float islandScale;     // BigIslands: island-mask frequency (~ islands across the map; lower = fewer/larger)
        public float islandThreshold; // BigIslands: sea level of the island mask (higher = more open ocean)
        public int beachWaterRadius; // Beach kept only within this many tiles of water (0 = off)

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

            // Low-frequency offsets for the BigIslands continent mask. Drawn after the
            // octave offsets so a given seed produces the same Pangea map as before.
            float islandOffX = (float)rng.NextDouble() * 200000f - 100000f;
            float islandOffY = (float)rng.NextDouble() * 200000f - 100000f;

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
                float islandMask = IslandMask(s, nx, ny, islandOffX, islandOffY);
                v = Shape(v, nx, ny, s.worldType, s.islandFalloff, islandMask);
                cells[y * w + x] = Classify(v, s);
            }

            RemoveInlandBeaches(cells, w, h, s.beachWaterRadius);
            return new WorldMap(w, h, cells);
        }

        /// <summary>
        /// Beach only belongs at the shoreline: any Beach cell with no Sea/DeepSea within
        /// <paramref name="radius"/> tiles becomes Plain (removes inland sand bands that are
        /// just an elevation contour rather than a coast). A radius of 0 disables this.
        /// </summary>
        private static void RemoveInlandBeaches(TerrainType[] cells, int w, int h, int radius)
        {
            if (radius <= 0) return;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                if (cells[i] == TerrainType.Beach && !HasWaterNearby(cells, x, y, w, h, radius))
                    cells[i] = TerrainType.Plain;
            }
        }

        private static bool HasWaterNearby(TerrainType[] cells, int x, int y, int w, int h, int r)
        {
            int r2 = r * r;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r2) continue; // circular reach, not a square box
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                TerrainType t = cells[ny * w + nx];
                if (t == TerrainType.Sea || t == TerrainType.DeepSea) return true;
            }
            return false;
        }

        /// <summary>
        /// BigIslands continent mask: a low-frequency Perlin field thresholded into island
        /// (~1) and open-sea (~0) regions with a soft coast. Returns 1 for other world types
        /// (no extra masking).
        /// </summary>
        private static float IslandMask(WorldGenSettings s, float nx, float ny, float offX, float offY)
        {
            if (s.worldType != WorldType.BigIslands) return 1f;
            float freq = Mathf.Max(0.5f, s.islandScale);
            float cm = Mathf.PerlinNoise(nx * freq + offX, ny * freq + offY);
            float t = Mathf.Clamp01(s.islandThreshold);
            // Soft 0.30-wide coastline centered on the threshold.
            return Mathf.SmoothStep(0f, 1f, (cm - (t - 0.15f)) / 0.30f);
        }

        /// <summary>Bends the normalized height according to the chosen world type.</summary>
        private static float Shape(float v, float nx, float ny, WorldType worldType, float islandFalloff,
            float islandMask)
        {
            // Radial edge mask shared by every shape: pulls the map border down to deep sea
            // so the world is always ringed by ocean. Higher islandFalloff lets land reach
            // farther out before the falloff bites.
            float dx = nx * 2f - 1f;
            float dy = ny * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx + dy * dy); // 0 at center, >=1 at edges/corners
            float edge = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(d), Mathf.Max(0.1f, islandFalloff)));

            switch (worldType)
            {
                // BigIslands: the continent mask carves the noise into several large islands;
                // the edge mask keeps the border as open sea.
                case WorldType.BigIslands:
                    return v * edge * Mathf.Clamp01(islandMask);

                // Pangea: a single central landmass fully ringed by sea.
                case WorldType.Pangea:
                default:
                    return v * edge;
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
