using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Gondwana.Rendering;
using Gondwana.Scenes.EventArgs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class Scene : IEnumerable<SceneLayer>, IDisposable
{
    #region private / internal field declarations

    [JsonProperty]
    private List<SceneLayer> _sceneLayers;    // array of SceneLayer objects; each element is one "layer"

    internal List<SceneLayer> _visibleLayers = new List<SceneLayer>();
    //internal SceneRefreshType refreshNeeded = SceneRefreshType.All;

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
        _SetVisibleSceneLayersArray();

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
    public int Count => _sceneLayers?.Count ?? 0;

    [JsonIgnore]
    public int CountOfVisibleLayers => _visibleLayers?.Count ?? 0;

    [JsonIgnore]
    public SceneRefreshType RefreshNeeded { get; set; }

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> SceneLayer => _sceneLayers.AsReadOnly();

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> VisibleSceneLayer => _visibleLayers.AsReadOnly();

    #endregion public properties

    #region public methods

    public SceneLayer AddLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Add(sceneLayer);
        int newIdx = _sceneLayers.Count - 1;
        OnSceneLayerAdded(this[newIdx]);

        // rediscover the list of visible arrays
        _SetVisibleSceneLayersArray();

        RefreshNeeded = SceneRefreshType.All;

        return this[newIdx];
    }

    public void RemoveAllLayers()
    {
        // raise "remove" event for each SceneLayer
        foreach (SceneLayer sceneLayer in this)
            OnSceneLayerRemoved(sceneLayer);

        _sceneLayers.Clear();

        // rediscover the list of visible arrays
        _SetVisibleSceneLayersArray();

        RefreshNeeded = SceneRefreshType.All;
    }

    public void RemoveLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Remove(sceneLayer);
        OnSceneLayerRemoved(sceneLayer);

        // rediscover the list of visible scene layers
        _SetVisibleSceneLayersArray();

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
        sceneLayer.Parent = this;

        sceneLayer.SceneLayerDisposing += sceneLayerDisposing;
        sceneLayer.FirstColRowChanged += firstColRowDel;
        sceneLayer.VisibleChanged += visChgDel;
        sceneLayer.SceneLayerTileSizeChanged += sceneLayerTileSizeDel;
        sceneLayer.RefreshQueueAreaAdded += refQueueDel;
        sceneLayer.WrappingChanged += wrappingDel;

        if (SceneLayerAdded != null)
            SceneLayerAdded.Invoke(sceneLayer);
    }

    protected virtual void OnSceneLayerRemoved(SceneLayer sceneLayer)
    {
        sceneLayer.Parent = null;

        sceneLayer.SceneLayerDisposing -= sceneLayerDisposing;
        sceneLayer.FirstColRowChanged -= firstColRowDel;
        sceneLayer.VisibleChanged -= visChgDel;
        sceneLayer.SceneLayerTileSizeChanged -= sceneLayerTileSizeDel;
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
    private Action<SourceSceneLayerTileChangedEventArgs> firstColRowDel;
    private Action<SceneLayerVisibleChangedEventArgs> visChgDel;
    private Action<SceneLayerTileSizeChangedEventArgs> sceneLayerTileSizeDel;
    private Action<RefreshQueueAreaAddedEventArgs> refQueueDel;
    private Action<SceneLayerWrappingChangedEventArgs> wrappingDel;

    private void SetSceneLayerEventDelegates()
    {
        sceneLayerDisposing = (sceneLayer) => RemoveLayer(sceneLayer);
        firstColRowDel = (eventArgs) => _SceneLayerFirstColRowChanged();
        visChgDel = (eventArgs) => _SceneLayerVisibleChanged();
        sceneLayerTileSizeDel = (eventArgs) => _SceneLayerTileSizeChanged();
        refQueueDel = (eventArgs) => _RefreshQueueNewArea(eventArgs);
        wrappingDel = (eventArgs) => _SceneLayerWrappingChanged();
    }

    private void _SceneLayerFirstColRowChanged()
    {
        // shifting at least one Layer, so redraw entire Backbuffer
        RefreshNeeded = SceneRefreshType.All;
    }

    private void _SceneLayerVisibleChanged()
    {
        // redraw entire Backbuffer
        RefreshNeeded = SceneRefreshType.All;
        _SetVisibleSceneLayersArray();
    }

    private void _SetVisibleSceneLayersArray()
    {
        if (_visibleLayers == null)
            _visibleLayers = new List<SceneLayer>();

        _visibleLayers.Clear();
        foreach (SceneLayer sceneLayer in this)
        {
            if (sceneLayer.Visible)
                _visibleLayers.Add(sceneLayer);
        }
    }

    private void _SceneLayerTileSizeChanged()
    {
        RefreshNeeded = SceneRefreshType.All;
    }

    private void _RefreshQueueNewArea(RefreshQueueAreaAddedEventArgs e)
    {
        // set refresh to Queue if no refresh required
        if (RefreshNeeded == SceneRefreshType.None)
            RefreshNeeded = SceneRefreshType.Queue;

        // if SceneLayer that added Tile to queue is visible...
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

    private void _SceneLayerWrappingChanged()
    {
        RefreshNeeded = SceneRefreshType.All;
    }

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

        // unsubscribe from events
        foreach (var sceneLayer in _sceneLayers)
        {
            sceneLayer.FirstColRowChanged -= firstColRowDel;
            sceneLayer.VisibleChanged -= visChgDel;
            sceneLayer.SceneLayerTileSizeChanged -= sceneLayerTileSizeDel;
            sceneLayer.RefreshQueueAreaAdded -= refQueueDel;
            sceneLayer.WrappingChanged -= wrappingDel;
        }

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