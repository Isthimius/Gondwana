using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Gondwana.Rendering;
using Gondwana.Scenes.EventArgs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class Scene : IEnumerable, IDisposable
{
    #region private / internal field declarations

    [JsonProperty]
    private List<SceneLayer> _sceneLayers;    // array of SceneLayer objects; each element is one "layer"

    internal List<SceneLayer> _visibleLayers = new List<SceneLayer>();
    internal SceneRefreshType refreshNeeded = SceneRefreshType.All;

    private string _id = Guid.NewGuid().ToString();

    #endregion private / internal field declarations

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

        // discover the list of visible arrays
        _SetVisibleLayersArray();

        _allScenes.Add(this);
    }

    #endregion constructors / finalizer

    #region public properties

    [JsonIgnore]
    public object Tag { get; set; }

    [JsonProperty]
    public string ID
    {
        get { return _id; }
        protected internal set { _id = value; }
    }

    [JsonIgnore]
    public int Count
    {
        get { return _sceneLayers.Count; }
    }

    [JsonIgnore]
    public int CountOfVisibleLayers
    {
        get { return _visibleLayers.Count; }
    }

    [JsonIgnore]
    public SceneRefreshType RefreshNeeded
    {
        get { return refreshNeeded; }
        set { refreshNeeded = value; }
    }

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> SceneLayerList
    {
        get { return _sceneLayers.AsReadOnly(); }
    }

    [JsonIgnore]
    public List<SceneLayer> VisibleSceneLayerList
    {
        get { return _visibleLayers; }
    }

    #endregion public properties

    #region public methods

    public SceneLayer AddLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Add(sceneLayer);
        int newIdx = _sceneLayers.Count - 1;
        OnSceneLayerAdded(this[newIdx]);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;

        return this[newIdx];
    }

    public void RemoveAllLayers()
    {
        // raise "remove" event for each grid
        foreach (SceneLayer grid in this)
            OnSceneLayerRemoved(grid);

        _sceneLayers.Clear();

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;
    }

    public void RemoveLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Remove(sceneLayer);
        OnSceneLayerRemoved(sceneLayer);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;
    }

    public SceneLayer GetSceneLayerByID(string id)
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
        sceneLayer.Parent = this;

        sceneLayer.SceneLayerDisposing += sceneLayerDisposing;
        sceneLayer.FirstColRowChanged += firstCRDel;
        sceneLayer.VisibleChanged += visChgDel;
        sceneLayer.GridPointSizeChanged += sceneLayerTileSizeDel;
        sceneLayer.RefreshQueueAreaAdded += refQueueDel;
        sceneLayer.WrappingChanged += wrappingDel;

        if (SceneLayerAdded != null)
            SceneLayerAdded.Invoke(sceneLayer);
    }

    protected virtual void OnSceneLayerRemoved(SceneLayer sceneLayer)
    {
        sceneLayer.Parent = null;

        sceneLayer.SceneLayerDisposing -= sceneLayerDisposing;
        sceneLayer.FirstColRowChanged -= firstCRDel;
        sceneLayer.VisibleChanged -= visChgDel;
        sceneLayer.GridPointSizeChanged -= sceneLayerTileSizeDel;
        sceneLayer.RefreshQueueAreaAdded -= refQueueDel;
        sceneLayer.WrappingChanged -= wrappingDel;

        if (SceneLayerRemoved != null)
            SceneLayerRemoved.Invoke(sceneLayer);
    }

    protected virtual void OnSceneDisposing()
    {
        if (SceneDisposing != null)
            SceneDisposing.Invoke(this);
    }

    #endregion handle / raise Scene events

    #region handle SceneLayer events

    private Action<SceneLayer> sceneLayerDisposing;
    private Action<SourceGridPointChangedEventArgs> firstCRDel;
    private Action<SceneLayerVisibleChangedEventArgs> visChgDel;
    private Action<SceneLayerTileSizeChangedEventArgs> sceneLayerTileSizeDel;
    private Action<RefreshQueueAreaAddedEventArgs> refQueueDel;
    private Action<SceneLayerWrappingChangedEventArgs> wrappingDel;

    private void SetSceneLayerEventDelegates()
    {
        sceneLayerDisposing = (sceneLayer) => RemoveLayer(sceneLayer);
        firstCRDel = (eventArgs) => _SceneLayerFirstColRowChanged(eventArgs);
        visChgDel = (eventArgs) => _SceneLayerVisibleChanged(eventArgs);
        sceneLayerTileSizeDel = (eventArgs) => _GridPointSizeChanged(eventArgs);
        refQueueDel = (eventArgs) => _RefreshQueueNewArea(eventArgs);
        wrappingDel = (eventArgs) => _SceneLayerWrappingChanged(eventArgs);
    }

    private void _SceneLayerFirstColRowChanged(SourceGridPointChangedEventArgs e)
    {
        // shifting at least one Layer, so redraw entire Backbuffer
        refreshNeeded = SceneRefreshType.All;
    }

    private void _SceneLayerVisibleChanged(SceneLayerVisibleChangedEventArgs e)
    {
        // redraw entire Backbuffer
        refreshNeeded = SceneRefreshType.All;
        _SetVisibleLayersArray();
    }

    private void _SetVisibleLayersArray()
    {
        if (_visibleLayers == null)
            _visibleLayers = new List<SceneLayer>();

        _visibleLayers.Clear();
        foreach (SceneLayer grid in this)
        {
            if (grid.Visible == true)
                _visibleLayers.Add(grid);
        }
    }

    private void _GridPointSizeChanged(SceneLayerTileSizeChangedEventArgs e)
    {
        refreshNeeded = SceneRefreshType.All;
    }

    private void _RefreshQueueNewArea(RefreshQueueAreaAddedEventArgs e)
    {
        // set refresh to Queue if no refresh required
        if (refreshNeeded == SceneRefreshType.None)
            refreshNeeded = SceneRefreshType.Queue;

        // if matrix that added Tile to queue is visible...
        if (e.layer.Visible)
        {
            // refresh all other visible SceneLayers
            for (int i = _visibleLayers.Count - 1; i >= 0; i--)
            {
                SceneLayer otherSceneLayer = _visibleLayers[i];

                // refresh other SceneLayers; no need to do the calling one again
                if (e.layer != otherSceneLayer)
                    // only refresh e.tileAdded.DrawLocationRefresh rectangle; do not raise cascading events
                    otherSceneLayer.RefreshQueue.AddPixelRangeToRefreshQueue(e.area, false);
            }
        }
    }

    private void _SceneLayerWrappingChanged(SceneLayerWrappingChangedEventArgs e)
    {
        refreshNeeded = SceneRefreshType.All;
    }

    #endregion handle SceneLayer events

    #region indexers

    public SceneLayer this[int i] => _sceneLayers[i];

    public SceneLayer this[string id] => GetSceneLayerByID(id);

    #endregion indexers

    #region enumerable code

    public IEnumerator GetEnumerator()
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

        // unsubscribe from events
        foreach (SceneLayer grid in _sceneLayers)
        {
            grid.FirstColRowChanged -= firstCRDel;
            grid.VisibleChanged -= visChgDel;
            grid.GridPointSizeChanged -= sceneLayerTileSizeDel;
            grid.RefreshQueueAreaAdded -= refQueueDel;
            grid.WrappingChanged -= wrappingDel;
        }

        _allScenes.Remove(this);

        // cancel all subscriptions to this object
        SceneLayerAdded = null;
        SceneLayerRemoved = null;
        SceneDisposing = null;
    }

    #endregion IDisposable Members

    #region static

    internal static List<Scene> _allScenes = new List<Scene>();

    public static Scene? GetSceneByID(string id) => _allScenes.Find(s => s.ID == id);

    public static List<string> GetAllSceneIDs() => _allScenes.FindAll(s => s != null).ConvertAll(s => s.ID);

    public static ReadOnlyCollection<Scene> GetAllScenes() => _allScenes.AsReadOnly();

    public static void ClearAllScenes()
    {
        var tmp = new List<Scene>(_allScenes);
        foreach (var scene in tmp)
            scene.Dispose();
    }

    #endregion static
}