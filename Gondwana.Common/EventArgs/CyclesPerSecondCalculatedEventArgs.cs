namespace Gondwana.Common.EventArgs;

public delegate void CyclesPerSecondCalculatedHandler(CyclesPerSecondCalculatedEventArgs e);

public class CyclesPerSecondCalculatedEventArgs : System.EventArgs
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
}
