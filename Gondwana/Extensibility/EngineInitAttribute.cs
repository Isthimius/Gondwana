namespace Gondwana.Extensibility;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class EngineInitAttribute : Attribute
{
    /// <summary>
    /// The timing at which the component should be initialized.
    /// </summary>
    public InitTiming InitTiming { get; }

    /// <summary>
    /// The priority of the initialization relative to other components.
    /// Higher values indicate higher priority and will run first. The default value is 1.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Specifies that this method should be run on Engine.Initialize().
    /// </summary>
    /// <remarks>This attribute is used to configure the initialization timing and priority of an engine
    /// component. By default, the initialization timing is set to <see cref="InitTiming.PostInit"/>
    ///  with a priority of 1.</remarks>
    public EngineInitAttribute() : this(InitTiming.PostInit, 1) { }

    /// <summary>
    /// Specifies that this method should be run on Engine.Initialize().
    /// </summary>
    /// <param name="initTiming">The timing at which the component should be initialized.</param>
    /// <param name="priority">The priority of the initialization relative to other components.
    /// Higher values indicate higher priority and will run first. The default value is 1.</param>
    public EngineInitAttribute(InitTiming initTiming, int priority = 1)
    {
        InitTiming = initTiming;
        Priority = priority;
    }
}
