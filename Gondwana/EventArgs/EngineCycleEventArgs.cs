namespace Gondwana.EventArgs;

public delegate void EngineCycleEventHandler(EngineCycleEventArgs e);

public class EngineCycleEventArgs : System.EventArgs
{
    public long GrossCycles;
    public long GrossCyclesTotal;
    public long NetCycles;
    public long NetCyclesTotal;
    public double GrossCPS;
    public double NetFPS;

    private EngineCycleEventArgs() { }

    public EngineCycleEventArgs(long grossCycles, long grossCyclesTotal, long netCycles, long netCyclesTotal, double grossCPS, double netFPS)
    {
        GrossCycles = grossCycles;
        GrossCyclesTotal = grossCyclesTotal;
        NetCycles = netCycles;
        NetCyclesTotal = netCyclesTotal;
        GrossCPS = grossCPS;
        NetFPS = netFPS;
    }
}
