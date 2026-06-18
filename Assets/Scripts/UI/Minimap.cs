using AditusBelli.Buildings;
using AditusBelli.CameraControl;
using AditusBelli.Economy;
using AditusBelli.Map;
using AditusBelli.Teams;
using AditusBelli.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AditusBelli.UI
{
    /// <summary>
    /// Top-right minimap. Renders units, buildings and resource nodes as colored
    /// blips into a Texture2D (world positions mapped through the GameGrid bounds),
    /// draws the camera viewport rectangle, and lets the player click/drag to
    /// re-center the camera.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private const int Size = 128;       // texture resolution
        private const float FrameSize = 212f;
        private const float Inset = 6f;
        private const float RefreshInterval = 0.1f;

        private static readonly Color32 Terrain = new Color32(38, 64, 44, 255);
        private static readonly Color32 ExploredTerrain = new Color32(22, 38, 26, 255);
        private static readonly Color32 Unexplored = new Color32(8, 8, 10, 255);
        private static readonly Color32 Neutral = new Color32(150, 150, 150, 255);
        private static readonly Color32 ViewRect = new Color32(255, 255, 255, 255);

        private Texture2D _tex;
        private Color32[] _buffer;
        private RectTransform _rawRt;

        private GameGrid _grid;
        private TeamDef _localTeam;
        private Camera _cam;
        private RtsCameraController _camController;
        private float _timer;

        private void Start()
        {
            RectTransform root = HudController.Instance != null ? HudController.Instance.Root : null;
            _grid = GameGrid.Instance;
            if (root == null || _grid == null) { enabled = false; return; }

            _localTeam = TeamManager.Instance != null ? TeamManager.Instance.LocalPlayer : null;
            _cam = Camera.main;
            _camController = _cam != null ? _cam.GetComponent<RtsCameraController>() : null;

            _tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _buffer = new Color32[Size * Size];

            RectTransform frame = UiFactory.Panel(root, "Minimap", UiFactory.PanelColor);
            UiFactory.Place(frame, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(FrameSize, FrameSize));

            var rawGo = new GameObject("MinimapImage",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(MinimapInput));
            _rawRt = (RectTransform)rawGo.transform;
            _rawRt.SetParent(frame, false);
            UiFactory.Stretch(_rawRt, Inset, Inset, Inset, Inset);

            rawGo.GetComponent<RawImage>().texture = _tex;
            rawGo.GetComponent<MinimapInput>().minimap = this;

            Redraw();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;
            Redraw();
        }

        private void Redraw()
        {
            FogOfWar fog = FogOfWar.Instance;
            bool useFog = fog != null && fog.IsEnabled;

            FillTerrain(fog, useFog);

            foreach (ResourceNode node in ResourceNode.All)
                if (node != null && BlipVisible(fog, useFog, node.gameObject))
                    Plot(node.transform.position, 1, ResourceColor(node.resourceType));

            foreach (Unit u in UnitSelectionManager.AllUnits)
                if (u != null && BlipVisible(fog, useFog, u.gameObject))
                    Plot(u.transform.position, 1, BlipColor(u.gameObject));

            foreach (Building b in Building.All)
                if (b != null && BlipVisible(fog, useFog, b.gameObject))
                    Plot(b.transform.position, 2, BlipColor(b.gameObject));

            DrawCameraRect();

            _tex.SetPixels32(_buffer);
            _tex.Apply(false);
        }

        private void FillTerrain(FogOfWar fog, bool useFog)
        {
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                Color32 col = Terrain;
                if (useFog)
                {
                    var n = new Vector2(px / (float)(Size - 1), py / (float)(Size - 1));
                    Visibility v = fog.VisibilityAtWorld(_grid.NormalizedToWorld(n));
                    col = v == Visibility.Visible ? Terrain
                        : v == Visibility.Explored ? ExploredTerrain
                        : Unexplored;
                }
                _buffer[py * Size + px] = col;
            }
        }

        /// <summary>
        /// Fog rules for a blip: local always shown; enemies only when currently in
        /// sight; neutral terrain (resources/walls) once explored.
        /// </summary>
        private bool BlipVisible(FogOfWar fog, bool useFog, GameObject go)
        {
            if (!useFog) return true;

            var owner = go.GetComponent<Owner>();
            bool hasTeam = owner != null && owner.Team != null;
            if (hasTeam && owner.Team == _localTeam) return true;

            Visibility v = fog.VisibilityAtWorld(go.transform.position);
            bool enemy = hasTeam && owner.Team != _localTeam;
            return enemy ? v == Visibility.Visible : v != Visibility.Unseen;
        }

        /// <summary>Re-centers the camera on the world point under the minimap cursor.</summary>
        public void NavigateTo(PointerEventData e)
        {
            if (_rawRt == null || _grid == null || _camController == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rawRt, e.position, e.pressEventCamera, out Vector2 local)) return;

            Rect r = _rawRt.rect;
            var n = new Vector2(
                Mathf.Clamp01((local.x - r.xMin) / r.width),
                Mathf.Clamp01((local.y - r.yMin) / r.height));
            _camController.CenterOn(_grid.NormalizedToWorld(n));
        }

        // ------------------------------------------------------------ drawing

        private void Plot(Vector3 world, int radius, Color32 col)
        {
            Vector2 n = _grid.WorldToNormalized(world);
            int cx = Mathf.RoundToInt(n.x * (Size - 1));
            int cy = Mathf.RoundToInt(n.y * (Size - 1));
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                Set(cx + x, cy + y, col);
        }

        private void DrawCameraRect()
        {
            if (_cam == null) return;

            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            Vector3 c = _cam.transform.position;

            Vector2 mn = _grid.WorldToNormalized(new Vector3(c.x - halfW, c.y - halfH, 0f));
            Vector2 mx = _grid.WorldToNormalized(new Vector3(c.x + halfW, c.y + halfH, 0f));

            int x0 = Mathf.Clamp(Mathf.RoundToInt(mn.x * (Size - 1)), 0, Size - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(mn.y * (Size - 1)), 0, Size - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(mx.x * (Size - 1)), 0, Size - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(mx.y * (Size - 1)), 0, Size - 1);

            for (int x = x0; x <= x1; x++) { Set(x, y0, ViewRect); Set(x, y1, ViewRect); }
            for (int y = y0; y <= y1; y++) { Set(x0, y, ViewRect); Set(x1, y, ViewRect); }
        }

        private void Set(int x, int y, Color32 col)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size) return;
            _buffer[y * Size + x] = col;
        }

        private static Color32 BlipColor(GameObject go)
        {
            var owner = go.GetComponent<Owner>();
            return owner != null && owner.Team != null ? (Color32)owner.Team.color : Neutral;
        }

        private static Color32 ResourceColor(ResourceType type) => type switch
        {
            ResourceType.Food => new Color32(200, 70, 80, 255),
            ResourceType.Wood => new Color32(60, 135, 72, 255),
            ResourceType.Gold => new Color32(230, 200, 60, 255),
            ResourceType.Stone => new Color32(160, 160, 172, 255),
            _ => Neutral,
        };
    }

    /// <summary>Forwards pointer clicks/drags on the minimap image to the Minimap.</summary>
    public class MinimapInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Minimap minimap;

        public void OnPointerDown(PointerEventData e) { if (minimap != null) minimap.NavigateTo(e); }
        public void OnDrag(PointerEventData e) { if (minimap != null) minimap.NavigateTo(e); }
    }
}
