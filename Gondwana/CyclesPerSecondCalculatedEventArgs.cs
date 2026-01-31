using System.Text;

namespace Gondwana;

/// <summary>
/// Provides data for events that report calculated cycles per second (CPS) performance metrics.
/// This event args class contains both gross and net cycle counts, their corresponding rates per second,
/// and the sampling time period. Net CPS typically represents the effective frame rate (FPS) of the engine,
/// while gross CPS includes all cycles regardless of whether they resulted in rendered frames.
/// </summary>
public class CyclesPerSecondCalculatedEventArgs : EventArgs
{
    /// <summary>
    /// The total number of gross cycles counted during the sampling period.
    /// Gross cycles represent all engine update cycles, including those that may not have
    /// resulted in a rendered frame. This value provides insight into the engine's overall
    /// processing activity.
    /// </summary>
    public long TotalGrossCycles;

    /// <summary>
    /// The total number of net cycles counted during the sampling period.
    /// Net cycles typically represent engine update cycles that resulted in rendered frames,
    /// providing a more accurate measure of visible performance and frame delivery.
    /// </summary>
    public long TotalNetCycles;

    /// <summary>
    /// The calculated gross cycles per second rate, representing how many engine cycles
    /// occurred per second during the sampling period. This includes all cycles regardless
    /// of whether they produced rendered output.
    /// </summary>
    public double GrossCPS;

    /// <summary>
    /// The calculated net cycles per second rate, representing the effective frame rate (FPS).
    /// This value indicates how many rendered frames or meaningful engine cycles occurred per second
    /// during the sampling period, providing the most relevant performance metric for visual smoothness.
    /// </summary>
    public double NetCPS;

    /// <summary>
    /// The duration of the sampling period in seconds over which the cycle counts and rates
    /// were measured. This time window determines the granularity and accuracy of the CPS calculations.
    /// </summary>
    public double SampleTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CyclesPerSecondCalculatedEventArgs"/> class
    /// with the specified cycle counts, rates, and sampling time.
    /// </summary>
    /// <param name="totalGross">
    /// The total number of gross cycles counted during the sampling period.
    /// </param>
    /// <param name="totalNet">
    /// The total number of net cycles counted during the sampling period.
    /// </param>
    /// <param name="grossCPS">
    /// The calculated gross cycles per second rate.
    /// </param>
    /// <param name="netCPS">
    /// The calculated net cycles per second rate (effective FPS).
    /// </param>
    /// <param name="sampleTime">
    /// The duration of the sampling period in seconds.
    /// </param>
    public CyclesPerSecondCalculatedEventArgs(long totalGross, long totalNet, double grossCPS, double netCPS, double sampleTime)
    {
        TotalGrossCycles = totalGross;
        TotalNetCycles = totalNet;
        GrossCPS = grossCPS;
        NetCPS = netCPS;
        SampleTime = sampleTime;
    }

    /// <summary>
    /// Returns a formatted string representation of the performance metrics, including
    /// total cycle counts, sampling time, and calculated rates. This provides a human-readable
    /// summary of all performance data suitable for logging, debugging, or display purposes.
    /// </summary>
    /// <returns>
    /// A multi-line string containing formatted performance metrics with the following information:
    /// total gross cycles, total net cycles, sampling time in seconds, gross CPS, and net CPS (FPS).
    /// </returns>
    public override string ToString()
    {
        var cpsValue = new StringBuilder()
            .AppendLine($"Total gross cycles: {TotalGrossCycles:N0}")
            .AppendLine($"Total net cycles: {TotalNetCycles:N0}")
            .AppendLine($"Sampling time: {SampleTime:N2}s")
            .AppendLine($"Gross CPS: {GrossCPS:N2}")
            .AppendLine($"Net CPS (FPS): {NetCPS:N2}");

        return cpsValue.ToString();
    }
}