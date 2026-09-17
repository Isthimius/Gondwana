using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Physics.Collisions;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
/// Represents a game scene containing multiple layers of tiles, sprites, and game objects.
/// A scene organizes content into layers with independent coordinate systems, parallax scrolling,
/// and z-ordering, providing a complete environment for rendering and gameplay.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Scene"/> class is the primary container for organizing game content into
/// manageable layers. Each scene can contain multiple <see cref="SceneLayer"/> instances,
/// each with its own coordinate system, tile grid, and rendering properties.
/// </para>
/// <para>
/// Scenes maintain a collision world for physics interactions, support serialization for
/// save/load functionality, and provide extensible metadata storage through a <see cref="ValueBag"/>.
/// </para>
/// <para>
/// All scenes are automatically tracked in a global collection accessible via static methods
/// such as <see cref="GetAllScenes"/> and <see cref="GetSceneByID"/>.
/// </para>
/// </remarks>
[JsonObject(IsReference = true)]
public class Scene : IEnumerable<SceneLayer>, IDisposable
{
    [JsonProperty]
    private readonly List<SceneLayer> _sceneLayers = [];

    [JsonIgnore]
    private readonly object _renderSurfaceHostSync = new();

    [JsonIgnore]
    private RenderSurfaceHostBase? _boundRenderSurfaceHost;
    
    #region Scene events

    /// <summary>
    /// Occurs when a <see cref="SceneLayer"/> is added to this scene.
    /// </summary>
    /// <remarks>
    /// This event is raised after a layer has been added to the scene and all internal setup
    /// has completed, including event subscription and scene reference assignment. Subscribers
    /// can use this event to respond to scene composition changes.
    /// </remarks>
    public event Action<SceneLayer>? SceneLayerAdded;

    /// <summary>
    /// Occurs when a <see cref="SceneLayer"/> is removed from this scene.
    /// </summary>
    /// <remarks>
    /// This event is raised after a layer has been removed from the scene and all internal cleanup
    /// has completed, including event unsubscription and scene reference clearing. Subscribers
    /// can use this event to respond to scene composition changes or perform cleanup.
    /// </remarks>
    public event Action<SceneLayer>? SceneLayerRemoved;

    /// <summary>
    /// Occurs when this scene is being disposed.
    /// </summary>
    /// <remarks>
    /// This event is raised at the beginning of the <see cref="Dispose"/> method, before any
    /// layers are removed or resources are released. Subscribers can use this event to perform
    /// cleanup operations or save state before the scene is destroyed.
    /// </remarks>
    public event Action<Scene>? SceneDisposing;

    #endregion Scene events

    #region constructors / finalizer

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class with an empty layer collection.
    /// </summary>
    /// <remarks>
    /// The constructor creates a new scene with a unique ID, initializes the collision world,
    /// and registers the scene in the global scene collection. The scene is ready to have
    /// layers added via <see cref="AddLayer"/> or other methods.
    /// </remarks>
    public Scene()
    {
        _sceneLayers = new List<SceneLayer>();
        Init();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Scene"/> class from serialized state.
    /// </summary>
    /// <param name="sceneLayers">The layers to attach to the scene, or <see langword="null"/> to create an empty collection.</param>
    /// <param name="id">The unique identifier to assign to the scene, or <see langword="null"/> to generate a new identifier.</param>
    /// <param name="collisionGroups">The collision group registry to use for the scene, or <see langword="null"/> to create a new registry.</param>
    /// <param name="collisionProfiles">The collision profile registry to use for the scene, or <see langword="null"/> to create the standard profiles.</param>
    [JsonConstructor]
    protected Scene(List<SceneLayer>? sceneLayers,
                    string? id,
                    CollisionGroupRegistry? collisionGroups,
                    CollisionProfileRegistry? collisionProfiles)
    {
        _sceneLayers = sceneLayers ?? new List<SceneLayer>();
        ID = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString() : id;
        CollisionGroups = collisionGroups ?? new CollisionGroupRegistry();
        CollisionProfiles = collisionProfiles ?? new CollisionProfileRegistry();
        ValueBag = new TypedValueBag();

        Init();
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="Scene"/> class, releasing resources if the scene
    /// was not explicitly disposed.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that scene resources are cleaned up even if <see cref="Dispose"/>
    /// is not called explicitly. However, it is recommended to always call <see cref="Dispose"/>
    /// or use the scene in a <c>using</c> statement to ensure deterministic cleanup.
    /// </remarks>
    ~Scene()
    {
        Dispose();
    }

    private void Init()
    {
        SetSceneLayerEventDelegates();

        foreach (var sceneLayer in _sceneLayers)
            OnSceneLayerAdded(sceneLayer);

        if (!ReferenceEquals(this, Empty))
            _allScenes.Add(this);
    }

    #endregion constructors / finalizer

    #region public properties

    /// <summary>
    /// Gets the extensible value bag for storing arbitrary scene-specific metadata.
    /// </summary>
    /// <value>A <see cref="TypedValueBag"/> instance for storing custom key-value data.</value>
    /// <remarks>
    /// The value bag allows games or engine extensions to attach arbitrary structured data
    /// to scenes (such as level metadata, objectives, ambient settings, or custom properties)
    /// without modifying the core <see cref="Scene"/> class. Values are accessed using
    /// strongly-typed <see cref="ValueKey{T}"/> instances and are included in scene serialization.
    /// </remarks>
    [JsonIgnore]
    public TypedValueBag ValueBag { get; private set; } = new();

    /// <summary>
    /// Gets or sets the unique identifier for this scene.
    /// </summary>
    /// <value>A string representing the scene's unique ID, typically a GUID.</value>
    /// <remarks>
    /// The ID is automatically generated when a scene is created and is used to identify
    /// the scene in the global scene collection. It is also used during serialization and
    /// deserialization to maintain scene references across save/load operations.
    /// </remarks>
    [JsonProperty]
    public string ID { get; protected internal set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the total number of layers in this scene, including hidden layers.
    /// </summary>
    /// <value>The count of all <see cref="SceneLayer"/> instances in the scene.</value>
    /// <remarks>
    /// This property returns the total number of layers regardless of their visibility state.
    /// To get only visible layers, use <see cref="VisibleSceneLayers"/> or <see cref="CountOfVisibleLayers"/>.
    /// </remarks>
    [JsonIgnore]
    public int Count => _sceneLayers?.Count ?? 0;

    /// <summary>
    /// Gets the render surface host currently bound to this scene.
    /// </summary>
    /// <remarks>
    /// A scene may be actively bound to at most one render surface host. Multiple camera
    /// perspectives into the same scene should be represented by multiple views on that host.
    /// </remarks>
    [JsonIgnore]
    internal RenderSurfaceHostBase? BoundRenderSurfaceHost
    {
        get
        {
            lock (_renderSurfaceHostSync)
                return _boundRenderSurfaceHost;
        }
    }

    /// <summary>
    /// Gets whether the scene's current host consumes dirty-region refresh queues.
    /// Unbound scenes retain invalidations for a future bitmap host.
    /// </summary>
    [JsonIgnore]
    internal bool UsesDirtyRegionRendering => BoundRenderSurfaceHost?.Backbuffer.IsGlThreadRendered != true;

    /// <summary>
    /// Gets or sets a value indicating whether the entire scene needs to be refreshed on the next render.
    /// </summary>
    /// <value>
    /// <c>true</c> if all layers and content should be re-rendered from scratch; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// This flag is set to <c>true</c> when structural changes occur that affect the entire scene,
    /// such as adding/removing layers, changing layer properties (z-order, parallax, wrapping, etc.),
    /// or other global scene modifications.
    /// </para>
    /// <para>
    /// When <c>true</c>, the rendering system will perform a full redraw rather than incremental
    /// updates, ensuring all visual changes are properly reflected. The flag should be reset after
    /// the full refresh is completed.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool FullRefreshNeeded { get; set; }

    /// <summary>
    /// Gets a read-only collection of all layers in this scene, in their current order.
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlyCollection{T}"/> containing all <see cref="SceneLayer"/> instances,
    /// including hidden layers.
    /// </value>
    /// <remarks>
    /// This collection includes all layers regardless of visibility and preserves the order
    /// in which they were added. For rendering purposes, use <see cref="VisibleSceneLayers"/>
    /// which returns only visible layers sorted by z-order.
    /// </remarks>
    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> SceneLayers => _sceneLayers.AsReadOnly();

    private ReadOnlyCollection<SceneLayer>? _visibleSortedCache;
    private bool _visibleSortedDirty = true;

    /// <summary>
    /// Gets a read-only collection of visible layers sorted by z-order for rendering.
    /// </summary>
    /// <value>
    /// A <see cref="ReadOnlyCollection{T}"/> containing only visible <see cref="SceneLayer"/> instances,
    /// sorted by their <see cref="SceneLayer.ZOrder"/> property in ascending order.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property returns a cached, sorted collection that is automatically updated when
    /// layer visibility or z-order changes. The cache improves performance by avoiding repeated
    /// sorting operations during rendering.
    /// </para>
    /// <para>
    /// Layers are sorted in ascending z-order, meaning layers with lower z-order values are
    /// rendered first (appear behind) and layers with higher z-order values are rendered last
    /// (appear in front).
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// Gets the number of currently visible layers in this scene.
    /// </summary>
    /// <value>The count of layers with <see cref="SceneLayer.Visible"/> set to <c>true</c>.</value>
    /// <remarks>
    /// This property provides a quick way to determine how many layers will be rendered without
    /// iterating through the full layer collection. It reflects the count of layers in
    /// <see cref="VisibleSceneLayers"/>.
    /// </remarks>
    [JsonIgnore]
    public int CountOfVisibleLayers => VisibleSceneLayers?.Count ?? 0;

    /// <summary>
    /// Gets the registry of collision groups used to organize and manage collision detection within the scene.
    /// </summary>
    /// <remarks>Use this property to access the collection of collision groups for efficient grouping and
    /// handling of collision logic. The registry is initialized automatically and provides methods for adding,
    /// removing, and querying collision groups as needed.</remarks>
    [JsonProperty]
    public CollisionGroupRegistry CollisionGroups { get; private set; } = new();

    /// <summary>
    /// Gets the named collision-filtering profiles used by layers and sprites in
    /// this scene. Profiles resolve their group names through <see cref="CollisionGroups"/>.
    /// </summary>
    [JsonProperty]
    public CollisionProfileRegistry CollisionProfiles { get; private set; } = new();

    #endregion public properties

    #region internal properties and methods

    /// <summary>
    /// Claims this scene for the specified render surface host.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the scene is already bound to a different host.
    /// </exception>
    internal void BindRenderSurfaceHost(RenderSurfaceHostBase host)
    {
        ArgumentNullException.ThrowIfNull(host);

        // Scene.Empty is a shared null-object placeholder and is never treated as an
        // actively rendered scene for binding ownership purposes.
        if (ReferenceEquals(this, Empty))
            return;

        lock (_renderSurfaceHostSync)
        {
            if (_boundRenderSurfaceHost is not null &&
                !ReferenceEquals(_boundRenderSurfaceHost, host))
            {
                throw new InvalidOperationException(
                    $"Scene '{ID}' is already bound to another RenderSurfaceHost.");
            }

            _boundRenderSurfaceHost = host;
        }

        // A GPU host redraws the full frame and never consumes RefreshQueue. Remove
        // anything accumulated before binding; future additions are rejected by the
        // queue's scene-policy callback.
        if (!UsesDirtyRegionRendering)
        {
            foreach (var layer in _sceneLayers)
                layer.RefreshQueue.ClearRefreshQueue();
        }
    }

    /// <summary>
    /// Releases this scene when it is currently owned by the specified host.
    /// Calls from any other host are ignored.
    /// </summary>
    internal void UnbindRenderSurfaceHost(RenderSurfaceHostBase host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (ReferenceEquals(this, Empty))
            return;

        lock (_renderSurfaceHostSync)
        {
            if (ReferenceEquals(_boundRenderSurfaceHost, host))
                _boundRenderSurfaceHost = null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether any visible layer in this scene has pending updates
    /// that require rendering.
    /// </summary>
    /// <value>
    /// <c>true</c> if any visible layer's refresh queue contains dirty regions; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// This property is used internally by the rendering system to determine if the scene needs
    /// to be processed during the current frame. It provides an efficient way to skip rendering
    /// scenes that have no visual changes.
    /// </remarks>
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

    /// <summary>
    /// Creates and adds a new <see cref="SceneLayer"/> to this scene with the specified properties.
    /// </summary>
    /// <param name="columnCount">
    /// The number of columns (tiles wide) in the layer's grid.
    /// </param>
    /// <param name="rowCount">
    /// The number of rows (tiles high) in the layer's grid.
    /// </param>
    /// <param name="width">
    /// The width of each tile in pixels. Default is 32.
    /// </param>
    /// <param name="height">
    /// The height of each tile in pixels. Default is 32.
    /// </param>
    /// <param name="zOrder">
    /// The rendering order for this layer relative to other layers. Lower values render first (behind).
    /// Default is 0.
    /// </param>
    /// <param name="parallax">
    /// The parallax scrolling factor for this layer. Values less than 1.0 create a background effect,
    /// values greater than 1.0 create a foreground effect. Default is 1.0 (no parallax).
    /// </param>
    /// <param name="coordinateSystem">
    /// The coordinate system type to use for this layer. Default is <see cref="CoordinateSystemTypes.Orthogonal"/>.
    /// </param>
    /// <returns>The newly created and added <see cref="SceneLayer"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a new layer with the specified grid dimensions and visual properties,
    /// adds it to the scene's layer collection, and triggers the <see cref="SceneLayerAdded"/> event.
    /// The scene is marked for full refresh to ensure the new layer is rendered.
    /// </para>
    /// <para>
    /// The returned layer reference can be used to further configure the layer, such as setting
    /// tiles, adding sprites, or adjusting visual properties.
    /// </para>
    /// </remarks>
    public SceneLayer AddLayer(int columnCount,
                               int rowCount,
                               int width = 32,
                               int height = 32,
                               int zOrder = 0,
                               float parallax = 1f,
                               CoordinateSystemTypes coordinateSystem = CoordinateSystemTypes.Orthogonal)
    {
        var sceneLayer = new SceneLayer(columnCount, rowCount, width, height, parallax, coordinateSystem);
        sceneLayer.ZOrder = zOrder;

        _sceneLayers.Add(sceneLayer);
        OnSceneLayerAdded(sceneLayer);

        FullRefreshNeeded = true;

        return sceneLayer;
    }

    /// <summary>
    /// Adds an existing <see cref="SceneLayer"/> to this scene.
    /// </summary>
    /// <param name="sceneLayer">The <see cref="SceneLayer"/> to add to the scene.</param>
    /// <returns>The added <see cref="SceneLayer"/>.</returns>
    /// <remarks>
    /// This method adds a pre-existing layer to the scene's layer collection and triggers
    /// the <see cref="SceneLayerAdded"/> event. The scene is marked for full refresh to ensure
    /// the new layer is rendered. Use this method when you need to add a layer that was
    /// created separately or transferred from another scene.
    /// </remarks>
    public SceneLayer AddLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Add(sceneLayer);
        OnSceneLayerAdded(sceneLayer);
        FullRefreshNeeded = true;
        return sceneLayer;
    }

    /// <summary>
    /// Removes all layers from this scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method removes every <see cref="SceneLayer"/> from the scene, raising the
    /// <see cref="SceneLayerRemoved"/> event for each layer. The scene is marked for full refresh
    /// and the layer collection is cleared.
    /// </para>
    /// <para>
    /// Use this method when you need to completely reset the scene's content, such as when
    /// loading a new level or returning to a main menu.
    /// </para>
    /// </remarks>
    public void RemoveAllLayers()
    {
        // raise "remove" event for each SceneLayer
        foreach (SceneLayer sceneLayer in this)
            OnSceneLayerRemoved(sceneLayer);

        _sceneLayers.Clear();

        FullRefreshNeeded = true;
    }

    /// <summary>
    /// Removes the specified layer from this scene.
    /// </summary>
    /// <param name="sceneLayer">The <see cref="SceneLayer"/> to remove from the scene.</param>
    /// <remarks>
    /// <para>
    /// This method removes the specified layer from the scene's layer collection and raises
    /// the <see cref="SceneLayerRemoved"/> event. The scene is marked for full refresh to ensure
    /// the visual representation is updated.
    /// </para>
    /// <para>
    /// If the specified layer is not in this scene, the method has no effect.
    /// </para>
    /// </remarks>
    public void RemoveLayer(SceneLayer sceneLayer)
    {
        _sceneLayers.Remove(sceneLayer);
        OnSceneLayerRemoved(sceneLayer);

        FullRefreshNeeded = true;
    }

    /// <summary>
    /// Retrieves a layer from this scene by its unique identifier.
    /// </summary>
    /// <param name="id">The unique ID of the layer to retrieve.</param>
    /// <returns>
    /// The <see cref="SceneLayer"/> with the matching ID, or <c>null</c> if no layer with that ID exists in this scene.
    /// </returns>
    /// <remarks>
    /// This method performs a linear search through the scene's layers to find a matching ID.
    /// For frequent lookups, consider caching layer references or using the indexer syntax
    /// <c>scene["layerID"]</c> which calls this method internally.
    /// </remarks>
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
    /// Computes a world-space pixel bounding rectangle that encloses all layers in the scene.
    /// </summary>
    /// <returns>
    /// A <see cref="RectangleF"/> representing the union of all layer bounds in pixel coordinates,
    /// or <see cref="RectangleF.Empty"/> if the scene has no layers or all layers have empty bounds.
    /// </returns>
    /// <remarks>
    /// Each layer reports its own bounds via <c>GetLayerBoundsPx()</c>, and this method unions
    /// them together to produce a single bounding rectangle that encompasses the entire scene.
    /// This is useful for determining camera limits, culling regions, or overall scene dimensions.
    /// </remarks>
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

    /// <summary>
    /// Raises the <see cref="SceneLayerAdded"/> event and performs internal setup when a layer
    /// is added to the scene.
    /// </summary>
    /// <param name="sceneLayer">The <see cref="SceneLayer"/> that was added.</param>
    /// <remarks>
    /// <para>
    /// This method is called internally when a layer is added to the scene. It performs the following:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Assigns this scene as the layer's parent scene</description></item>
    /// <item><description>Subscribes to layer events for change tracking</description></item>
    /// <item><description>Invalidates the visible layer cache</description></item>
    /// <item><description>Raises the <see cref="SceneLayerAdded"/> event</description></item>
    /// </list>
    /// <para>
    /// Derived classes can override this method to add custom behavior when layers are added,
    /// but should call the base implementation to ensure proper setup.
    /// </para>
    /// </remarks>
    protected virtual void OnSceneLayerAdded(SceneLayer sceneLayer)
    {
        sceneLayer.Scene = this;
        sceneLayer.ApplyDefaultTileCollisionProfile();
        SpriteManager.Instance.RefreshCollisionProfiles(sceneLayer);

        sceneLayer.Disposing += sceneLayerDisposing;
        sceneLayer.VisibleChanged += visChgDel;
        sceneLayer.SceneLayerTileSizeChanged += sceneLayerTileSizeDel;
        sceneLayer.WrappingChanged += wrappingDel;
        sceneLayer.ShowGridLinesChanged += gridLinesShowChanged;
        sceneLayer.ShowCollisionBoxesChanged += showCollisionBoxesChanged;
        sceneLayer.ZOrderChanged += zOrderChangedDel;
        sceneLayer.ParallaxChanged += parallaxChangedDel;
        sceneLayer.OriginPxChanged += originPxChangedDel;

        _visibleSortedDirty = true;
        SceneLayerAdded?.Invoke(sceneLayer);
    }

    /// <summary>
    /// Raises the <see cref="SceneLayerRemoved"/> event and performs internal cleanup when a layer
    /// is removed from the scene.
    /// </summary>
    /// <param name="sceneLayer">The <see cref="SceneLayer"/> that was removed.</param>
    /// <remarks>
    /// <para>
    /// This method is called internally when a layer is removed from the scene. It performs the following:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Clears the layer's parent scene reference</description></item>
    /// <item><description>Unsubscribes from all layer events</description></item>
    /// <item><description>Invalidates the visible layer cache</description></item>
    /// <item><description>Raises the <see cref="SceneLayerRemoved"/> event</description></item>
    /// </list>
    /// <para>
    /// Derived classes can override this method to add custom cleanup behavior when layers are removed,
    /// but should call the base implementation to ensure proper cleanup.
    /// </para>
    /// </remarks>
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
        sceneLayer.OriginPxChanged -= originPxChangedDel;

        _visibleSortedDirty = true;
        SceneLayerRemoved?.Invoke(sceneLayer);
    }

    /// <summary>
    /// Raises the <see cref="SceneDisposing"/> event when the scene is being disposed.
    /// </summary>
    /// <remarks>
    /// This method is called at the beginning of the <see cref="Dispose"/> method to notify
    /// subscribers that the scene is being destroyed. Derived classes can override this method
    /// to add custom disposal logic, but should call the base implementation to ensure the event is raised.
    /// </remarks>
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
    private Action<SceneLayer> originPxChangedDel;

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
        originPxChangedDel = (sceneLayer) => _SceneLayerZeroPixelChanged();
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

    /// <summary>
    /// Gets the <see cref="SceneLayer"/> at the specified index in the layer collection.
    /// </summary>
    /// <param name="i">The zero-based index of the layer to retrieve.</param>
    /// <returns>
    /// The <see cref="SceneLayer"/> at the specified index, or <c>null</c> if the index is out of range.
    /// </returns>
    /// <remarks>
    /// This indexer provides direct access to layers by their position in the collection.
    /// The order reflects the order in which layers were added, not their z-order for rendering.
    /// For rendering order, use <see cref="VisibleSceneLayers"/> which is sorted by z-order.
    /// </remarks>
    public SceneLayer? this[int i] => (i >= 0 && i < _sceneLayers.Count) ? _sceneLayers[i] : null;

    /// <summary>
    /// Gets the <see cref="SceneLayer"/> with the specified unique identifier.
    /// </summary>
    /// <param name="id">The unique ID of the layer to retrieve.</param>
    /// <returns>
    /// The <see cref="SceneLayer"/> with the matching ID, or <c>null</c> if no layer with that ID exists in this scene.
    /// </returns>
    /// <remarks>
    /// This indexer provides convenient access to layers by their ID and internally calls
    /// <see cref="GetSceneLayerByID"/>. For example: <c>var layer = scene["myLayerId"];</c>
    /// </remarks>
    public SceneLayer? this[string id] => GetSceneLayerByID(id);

    #endregion indexers

    #region enumerable code

    /// <summary>
    /// Returns an enumerator that iterates through the scene's layers.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> for iterating through <see cref="SceneLayer"/> instances.</returns>
    /// <remarks>
    /// This method enables enumeration of scene layers using non-generic enumerator interfaces,
    /// supporting scenarios where type information is not available at compile time. For type-safe
    /// enumeration, the generic <see cref="IEnumerable{T}.GetEnumerator"/> is preferred.
    /// </remarks>
    public IEnumerator GetEnumerator() => ((IEnumerable<SceneLayer>)this).GetEnumerator();

    /// <summary>
    /// Returns a strongly-typed enumerator that iterates through the scene's layers.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator{T}"/> for iterating through <see cref="SceneLayer"/> instances.
    /// </returns>
    /// <remarks>
    /// This method enables enumeration of scene layers using <c>foreach</c> loops and LINQ queries.
    /// The enumerator iterates through layers in the order they were added to the scene, not their
    /// z-order for rendering.
    /// </remarks>
    IEnumerator<SceneLayer> IEnumerable<SceneLayer>.GetEnumerator()
    {
        for (int i = 0; i < _sceneLayers.Count; i++)
        {
            yield return _sceneLayers[i];
        }
    }

    #endregion enumerable code

    #region IDisposable Members

    /// <summary>
    /// Releases all resources used by the <see cref="Scene"/> and removes it from the global scene collection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs orderly cleanup of the scene, including:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Raising the <see cref="SceneDisposing"/> event</description></item>
    /// <item><description>Removing all layers via <see cref="RemoveAllLayers"/></description></item>
    /// <item><description>Removing the scene from the global scene collection</description></item>
    /// <item><description>Clearing all event subscriptions</description></item>
    /// </list>
    /// <para>
    /// After disposal, the scene should not be used. This method can be overridden in derived classes
    /// to add custom cleanup logic, but the base implementation should be called to ensure proper
    /// resource release.
    /// </para>
    /// <para>
    /// This method suppresses finalization to prevent the finalizer from running, as cleanup has
    /// already been performed.
    /// </para>
    /// </remarks>
    public virtual void Dispose()
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

    /// <summary>
    /// Retrieves a scene from the global scene collection by its unique identifier.
    /// </summary>
    /// <param name="id">The unique ID of the scene to retrieve.</param>
    /// <returns>
    /// The <see cref="Scene"/> with the matching ID, or <c>null</c> if no scene with that ID exists.
    /// </returns>
    /// <remarks>
    /// This method searches the global collection of all active scenes. Scenes are automatically
    /// added to this collection when created and removed when disposed.
    /// </remarks>
    public static Scene? GetSceneByID(string id) => _allScenes.Find(s => s.ID == id);

    /// <summary>
    /// Gets a list of unique identifiers for all active scenes in the global scene collection.
    /// </summary>
    /// <returns>A <see cref="List{T}"/> of scene ID strings.</returns>
    /// <remarks>
    /// This method returns the IDs of all scenes that have been created and not yet disposed.
    /// It excludes null entries and provides a snapshot of active scene identifiers at the time of the call.
    /// </remarks>
    public static List<string> GetAllSceneIDs() => _allScenes.FindAll(s => s != null).ConvertAll(s => s.ID);

    /// <summary>
    /// Gets a read-only collection of all active scenes in the global scene collection.
    /// </summary>
    /// <returns>
    /// A <see cref="ReadOnlyCollection{T}"/> containing all active <see cref="Scene"/> instances.
    /// </returns>
    /// <remarks>
    /// This method provides access to all scenes that have been created and not yet disposed.
    /// The collection includes all scenes except the singleton <see cref="Empty"/> scene.
    /// Changes to the underlying collection (such as creating or disposing scenes) will be
    /// reflected in subsequent calls to this method.
    /// </remarks>
    public static ReadOnlyCollection<Scene> GetAllScenes() => _allScenes.AsReadOnly();

    /// <summary>
    /// Disposes all active scenes in the global scene collection, releasing all resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method iterates through all active scenes and calls <see cref="Dispose"/> on each one,
    /// effectively clearing the entire scene collection. This is typically used during engine
    /// shutdown or when resetting the application state.
    /// </para>
    /// <para>
    /// After calling this method, all scene references become invalid and should not be used.
    /// New scenes can be created after clearing.
    /// </para>
    /// </remarks>
    public static void ClearAllScenes()
    {
        var tmp = new List<Scene>(_allScenes);
        foreach (var scene in tmp)
            scene.Dispose();
    }

    #endregion static helpers

    #region empty Scene

    /// <summary>
    /// Gets the singleton empty scene instance.
    /// </summary>
    /// <value>A <see cref="Scene"/> instance that contains only the empty layer and cannot be modified.</value>
    /// <remarks>
    /// <para>
    /// The empty scene is a special singleton instance that serves as a null object pattern
    /// implementation. It contains only <see cref="SceneLayer.Empty"/> and does not allow
    /// adding or removing layers.
    /// </para>
    /// <para>
    /// This scene is useful as a default value or placeholder when a valid scene is not available,
    /// avoiding null reference checks throughout the codebase. Attempting to add layers to this
    /// scene will throw an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// The empty scene is not included in the global scene collection and cannot be disposed.
    /// </para>
    /// </remarks>
    public static Scene Empty { get; } = new EmptyScene();

    private sealed class EmptyScene : Scene
    {
        internal EmptyScene()
        {
            _sceneLayers.Clear();

            // Attach the singleton empty layer
            _sceneLayers.Add(SceneLayer.Empty);
            SceneLayer.Empty.Scene = this;

            FullRefreshNeeded = false;
        }

        /// <summary>
        /// Prevents layers from being added to the singleton empty scene.
        /// </summary>
        /// <param name="sceneLayer">The layer that was requested to be added.</param>
        protected override void OnSceneLayerAdded(SceneLayer sceneLayer)
            => throw new InvalidOperationException("Cannot add layers to Scene.Empty");

        /// <summary>
        /// Performs no action when a layer is removed from the singleton empty scene.
        /// </summary>
        /// <param name="sceneLayer">The layer being removed.</param>
        protected override void OnSceneLayerRemoved(SceneLayer sceneLayer)
        {
            // no-op
        }

        /// <summary>
        /// Prevents disposal of the singleton empty scene instance.
        /// </summary>
        public override void Dispose()
        {
            // Intentionally empty - singleton
        }
    }

    #endregion empty Scene
}
