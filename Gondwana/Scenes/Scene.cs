using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Gondwana.Rendering;
using Microsoft.Extensions.Logging;
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

    public Scene(SceneLayer sceneLayer)
    {
        _sceneLayers = new List<SceneLayer>();
        _sceneLayers.Add(sceneLayer);
        Init();
    }

    public Scene(List<SceneLayer> sceneLayers)
    {
        _sceneLayers = sceneLayers;
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
        SetSceneLayerEventDelegates();

        foreach (var sceneLayer in _sceneLayers)
            OnSceneLayerAdded(sceneLayer);

        _allScenes.Add(this);
    }

    #endregion constructors / finalizer

    #region public properties

    [JsonProperty]
    public object Tag { get; set; }

    [JsonProperty]
    public string ID { get; protected internal set; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public int Count => _sceneLayers?.Count ?? 0;

    [JsonIgnore]
    public SceneRefreshType RefreshNeeded { get; set; }

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

    #endregion public properties

    #region public methods

    public SceneLayer AddLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Add(sceneLayer);
        OnSceneLayerAdded(sceneLayer);

        RefreshNeeded = SceneRefreshType.All;

        return sceneLayer;
    }

    public void RemoveAllLayers()
    {
        // raise "remove" event for each SceneLayer
        foreach (SceneLayer sceneLayer in this)
            OnSceneLayerRemoved(sceneLayer);

        _sceneLayers.Clear();

        RefreshNeeded = SceneRefreshType.All;
    }

    public void RemoveLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Remove(sceneLayer);
        OnSceneLayerRemoved(sceneLayer);

        RefreshNeeded = SceneRefreshType.All;
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

    #endregion public methods

    #region handle / raise Scene events

    protected virtual void OnSceneLayerAdded(SceneLayer sceneLayer)
    {
        sceneLayer.Scene = this;

        sceneLayer.Disposing += sceneLayerDisposing;
        sceneLayer.VisibleChanged += visChgDel;
        sceneLayer.SceneLayerTileSizeChanged += sceneLayerTileSizeDel;
        sceneLayer.RefreshQueueAreaAdded += refQueueDel;
        sceneLayer.WrappingChanged += wrappingDel;
        sceneLayer.ZOrderChanged += zOrderChangedDel;
        sceneLayer.ParallaxChanged += parallaxChangedDel;

        _visibleSortedDirty = true;
        SceneLayerAdded?.Invoke(sceneLayer);
    }

    protected virtual void OnSceneLayerRemoved(SceneLayer sceneLayer)
    {
        sceneLayer.Scene = null;

        sceneLayer.Disposing -= sceneLayerDisposing;
        sceneLayer.VisibleChanged -= visChgDel;
        sceneLayer.SceneLayerTileSizeChanged -= sceneLayerTileSizeDel;
        sceneLayer.RefreshQueueAreaAdded -= refQueueDel;
        sceneLayer.WrappingChanged -= wrappingDel;
        sceneLayer.ZOrderChanged -= zOrderChangedDel;
        sceneLayer.ParallaxChanged -= parallaxChangedDel;

        _visibleSortedDirty = true;
        SceneLayerRemoved?.Invoke(sceneLayer);
    }

    protected virtual void OnSceneDisposing() => SceneDisposing?.Invoke(this);

    #endregion handle / raise Scene events

    #region handle SceneLayer events

    private Action<RefreshQueueAreaAddedEventArgs> refQueueDel;
    private Action<SceneLayer> sceneLayerDisposing;
    private Action<SceneLayer> visChgDel;
    private Action<SceneLayer> sceneLayerTileSizeDel;
    private Action<SceneLayer> wrappingDel;
    private Action<SceneLayer> zOrderChangedDel;
    private Action<SceneLayer> parallaxChangedDel;

    private void SetSceneLayerEventDelegates()
    {
        refQueueDel = (eventArgs) => _RefreshQueueNewArea(eventArgs);
        sceneLayerDisposing = (sceneLayer) => RemoveLayer(sceneLayer);
        visChgDel = (sceneLayer) => _SceneLayerVisibleChanged();
        sceneLayerTileSizeDel = (sceneLayer) => _SceneLayerTileSizeChanged();
        wrappingDel = (sceneLayer) => _SceneLayerWrappingChanged();
        zOrderChangedDel = (sceneLayer) => _SceneLayerZOrderChanged();
        parallaxChangedDel = (sceneLayer) => _SceneLayerParallaxChanged();
    }

    private void _SceneLayerVisibleChanged()
    {
        _visibleSortedDirty = true;
        RefreshNeeded = SceneRefreshType.All;
    }

    private void _SceneLayerTileSizeChanged() => RefreshNeeded = SceneRefreshType.All;

    private void _RefreshQueueNewArea(RefreshQueueAreaAddedEventArgs e)
    {
        // set refresh to Queue if no refresh required
        if (RefreshNeeded == SceneRefreshType.None)
            RefreshNeeded = SceneRefreshType.Queue;

        // if SceneLayer that added Tile to queue is visible...
        if (e.layer.Visible)
        {
            // refresh all other visible SceneLayers
            for (int i = VisibleSceneLayers.Count - 1; i >= 0; i--)
            {
                SceneLayer otherSceneLayer = VisibleSceneLayers[i];

                // refresh other SceneLayers; no need to do the calling one again
                if (e.layer != otherSceneLayer)
                    // only refresh e.tileAdded.DrawLocationRefresh rectangle; do not raise cascading events
                    otherSceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(e.area, false);
            }
        }
    }

    private void _SceneLayerWrappingChanged() => RefreshNeeded = SceneRefreshType.All;

    private void _SceneLayerParallaxChanged() => RefreshNeeded = SceneRefreshType.All;

    private void _SceneLayerZOrderChanged() => RefreshNeeded = SceneRefreshType.All;

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