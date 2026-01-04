using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Collision;
using Gondwana.Drawing.Coordinates;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class Scene : IEnumerable<SceneLayer>, IDisposable
{
    [JsonProperty]
    private readonly List<SceneLayer> _sceneLayers = [];
    
    #region Scene events

    public event Action<SceneLayer>? SceneLayerAdded;

    public event Action<SceneLayer>? SceneLayerRemoved;

    public event Action<Scene>? SceneDisposing;

    #endregion Scene events

    #region constructors / finalizer

    public Scene()
    {
        _sceneLayers = new List<SceneLayer>();
        Init();
    }

    ~Scene()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        Init();
    }

    private void Init()
    {
        CollisionWorld = new CollisionWorld();   // ensure new instance on deserialization too

        SetSceneLayerEventDelegates();

        foreach (var sceneLayer in _sceneLayers)
            OnSceneLayerAdded(sceneLayer);

        _allScenes.Add(this);
    }

    #endregion constructors / finalizer

    #region public properties

    [JsonProperty]
    public TypedValueBag ValueBag { get; } = new();

    [JsonProperty]
    public string ID { get; protected internal set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public int Count => _sceneLayers?.Count ?? 0;

    [JsonIgnore]
    public bool FullRefreshNeeded { get; set; }

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> SceneLayers => _sceneLayers.AsReadOnly();

    private ReadOnlyCollection<SceneLayer>? _visibleSortedCache;
    private bool _visibleSortedDirty = true;

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> VisibleSceneLayers
    {
        get
        {
            if (_visibleSortedDirty || _visibleSortedCache == null)
            {
                _visibleSortedCache = _sceneLayers
                    .Where(sl => sl.Visible)
                    .OrderBy(sl => sl.ZOrder)
                    .ToList()
                    .AsReadOnly();
                _visibleSortedDirty = false;
            }
            return _visibleSortedCache;
        }
    }

    [JsonIgnore]
    public int CountOfVisibleLayers => VisibleSceneLayers?.Count ?? 0;

    [JsonIgnore]
    public CollisionWorld CollisionWorld { get; private set; } = new();

    #endregion public properties

    #region internal properties and methods

    internal bool IsDirty
    {
        get
        {
            for (int i = 0; i < CountOfVisibleLayers; i++)
            {
                if (VisibleSceneLayers[i].RefreshQueue.IsDirty)
                    return true;
            }

            return false;
        }
    }

    #endregion internal properties and methods

    #region public methods

    public SceneLayer AddLayer(int columnCount,
                               int rowCount,
                               int width = 32,
                               int height = 32,
                               int zOrder = 0,
                               float parallax = 1f,
                               CoordinateSystemTypes coordinateSystem = CoordinateSystemTypes.SquareIso)
    {
        var sceneLayer = new SceneLayer(columnCount, rowCount, width, height, parallax, coordinateSystem);
        sceneLayer.ZOrder = zOrder;

        _sceneLayers.Add(sceneLayer);
        OnSceneLayerAdded(sceneLayer);

        FullRefreshNeeded = true;

        return sceneLayer;
    }

    public void RemoveAllLayers()
    {
        // raise "remove" event for each SceneLayer
        foreach (SceneLayer sceneLayer in this)
            OnSceneLayerRemoved(sceneLayer);

        _sceneLayers.Clear();

        FullRefreshNeeded = true;
    }

    public void RemoveLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Remove(sceneLayer);
        OnSceneLayerRemoved(sceneLayer);

        FullRefreshNeeded = true;
    }

    public SceneLayer? GetSceneLayerByID(string id)
    {
        foreach (var sceneLayer in _sceneLayers)
        {
            if (sceneLayer.ID == id)
                return sceneLayer;
        }

        return null;
    }

    /// <summary>
    /// Computes a world-space pixel bounding rectangle that encloses all layers
    /// in the Scene. Each layer reports its own bounds via GetLayerBoundsPx(),
    /// and this method unions them together.
    /// </summary>
    public RectangleF GetWorldBoundsPx()
    {
        if (_sceneLayers.Count == 0)
            return RectangleF.Empty;

        RectangleF result = RectangleF.Empty;
        bool hasBounds = false;

        foreach (var layer in _sceneLayers)
        {
            var lb = layer.GetLayerBoundsPx();
            if (lb.IsEmpty)
                continue;

            if (!hasBounds)
            {
                result = lb;
                hasBounds = true;
            }
            else
            {
                result = RectangleF.Union(result, lb);
            }
        }

        return result;
    }

    #endregion public methods

    #region raise Scene events

    protected virtual void OnSceneLayerAdded(SceneLayer sceneLayer)
    {
        sceneLayer.Scene = this;

        sceneLayer.Disposing += sceneLayerDisposing;
        sceneLayer.VisibleChanged += visChgDel;
        sceneLayer.SceneLayerTileSizeChanged += sceneLayerTileSizeDel;
        sceneLayer.WrappingChanged += wrappingDel;
        sceneLayer.ShowGridLinesChanged += gridLinesShowChanged;
        sceneLayer.ShowCollisionBoxesChanged += showCollisionBoxesChanged;
        sceneLayer.ZOrderChanged += zOrderChangedDel;
        sceneLayer.ParallaxChanged += parallaxChangedDel;
        sceneLayer.OriginPxChanged += zeroPixelChangedDel;

        _visibleSortedDirty = true;
        SceneLayerAdded?.Invoke(sceneLayer);
    }

    protected virtual void OnSceneLayerRemoved(SceneLayer sceneLayer)
    {
        sceneLayer.Scene = null;

        sceneLayer.Disposing -= sceneLayerDisposing;
        sceneLayer.VisibleChanged -= visChgDel;
        sceneLayer.SceneLayerTileSizeChanged -= sceneLayerTileSizeDel;
        sceneLayer.WrappingChanged -= wrappingDel;
        sceneLayer.ShowGridLinesChanged -= gridLinesShowChanged;
        sceneLayer.ShowCollisionBoxesChanged -= showCollisionBoxesChanged;
        sceneLayer.ZOrderChanged -= zOrderChangedDel;
        sceneLayer.ParallaxChanged -= parallaxChangedDel;
        sceneLayer.OriginPxChanged -= zeroPixelChangedDel;

        _visibleSortedDirty = true;
        SceneLayerRemoved?.Invoke(sceneLayer);
    }

    protected virtual void OnSceneDisposing() => SceneDisposing?.Invoke(this);

    #endregion raise handle Scene events

    #region handle SceneLayer events

    private Action<SceneLayer> sceneLayerDisposing;
    private Action<SceneLayer> visChgDel;
    private Action<SceneLayer> sceneLayerTileSizeDel;
    private Action<SceneLayer> wrappingDel;
    private Action<SceneLayer> gridLinesShowChanged;
    private Action<SceneLayer> showCollisionBoxesChanged;
    private Action<SceneLayer> zOrderChangedDel;
    private Action<SceneLayer> parallaxChangedDel;
    private Action<SceneLayer> zeroPixelChangedDel;

    private void SetSceneLayerEventDelegates()
    {
        sceneLayerDisposing = (sceneLayer) => RemoveLayer(sceneLayer);
        visChgDel = (sceneLayer) => _SceneLayerVisibleChanged();
        sceneLayerTileSizeDel = (sceneLayer) => _SceneLayerTileSizeChanged();
        wrappingDel = (sceneLayer) => _SceneLayerWrappingChanged();
        gridLinesShowChanged = (sceneLayer) => _SceneLayerGridLinesShowChanged();
        showCollisionBoxesChanged = (sceneLayer) => _SceneLayerShowCollisionBoxChanged();
        zOrderChangedDel = (sceneLayer) => _SceneLayerZOrderChanged();
        parallaxChangedDel = (sceneLayer) => _SceneLayerParallaxChanged();
        zeroPixelChangedDel = (sceneLayer) => _SceneLayerZeroPixelChanged();
    }

    private void _SceneLayerVisibleChanged()
    {
        _visibleSortedDirty = true;
        FullRefreshNeeded = true;
    }

    private void _SceneLayerTileSizeChanged() => FullRefreshNeeded = true;

    private void _SceneLayerWrappingChanged() => FullRefreshNeeded = true;

    private void _SceneLayerGridLinesShowChanged() => FullRefreshNeeded = true;

    private void _SceneLayerShowCollisionBoxChanged() => FullRefreshNeeded = true;

    private void _SceneLayerZOrderChanged() => FullRefreshNeeded = true;

    private void _SceneLayerParallaxChanged() => FullRefreshNeeded = true;

    private void _SceneLayerZeroPixelChanged() => FullRefreshNeeded = true;

    #endregion handle SceneLayer events

    #region indexers

    public SceneLayer? this[int i] => (i >= 0 && i < _sceneLayers.Count) ? _sceneLayers[i] : null;

    public SceneLayer? this[string id] => GetSceneLayerByID(id);

    #endregion indexers

    #region enumerable code

    public IEnumerator GetEnumerator() => ((IEnumerable<SceneLayer>)this).GetEnumerator();

    IEnumerator<SceneLayer> IEnumerable<SceneLayer>.GetEnumerator()
    {
        for (int i = 0; i < _sceneLayers.Count; i++)
        {
            yield return _sceneLayers[i];
        }
    }

    #endregion enumerable code

    #region IDisposable Members

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        OnSceneDisposing();

        RemoveAllLayers();

        _allScenes.Remove(this);

        // cancel all subscriptions to this object
        SceneLayerAdded = null;
        SceneLayerRemoved = null;
        SceneDisposing = null;
    }

    #endregion IDisposable Members

    #region static helpers

    internal readonly static List<Scene> _allScenes = [];

    public static Scene? GetSceneByID(string id) => _allScenes.Find(s => s.ID == id);

    public static List<string> GetAllSceneIDs() => _allScenes.FindAll(s => s != null).ConvertAll(s => s.ID);

    public static ReadOnlyCollection<Scene> GetAllScenes() => _allScenes.AsReadOnly();

    public static void ClearAllScenes()
    {
        var tmp = new List<Scene>(_allScenes);
        foreach (var scene in tmp)
            scene.Dispose();
    }

    #endregion static helpers
}