using System.Text;

namespace Gondwana;

public class CyclesPerSecondCalculatedEventArgs : EventArgs
{
    public long TotalGrossCycles;
    public long TotalNetCycles;
    public double GrossCPS;
    public double NetCPS;
    public double SamplingTime;

    public CyclesPerSecondCalculatedEventArgs(long totalGross, long totalNet, double grossCPS, double netCPS, double samplingTime)
    {
        TotalGrossCycles = totalGross;
        TotalNetCycles = totalNet;
        GrossCPS = grossCPS;
        NetCPS = netCPS;
        SamplingTime = samplingTime;
    }

    public override string ToString()
    {
        var cpsValue = new StringBuilder()
            .AppendLine($"Total gross cycles: {TotalGrossCycles:N0}")
            .AppendLine($"Total net cycles: {TotalNetCycles:N0}")
            .AppendLine($"Sampling time: {SamplingTime:0.00}s")
            .AppendLine($"Gross CPS: {GrossCPS:0.00}")
            .AppendLine($"Net CPS: {NetCPS:0.00}");

        return cpsValue.ToString();
    }
}