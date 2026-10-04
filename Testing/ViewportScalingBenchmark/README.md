# ViewportScalingBenchmark

Opt-in CPU microbenchmark for Gondwana's final viewport-presentation scaling path.

It measures the cost of presenting a prebuilt 1920 x 1080 image at 1920 x 1080 and
3840 x 2160 using both `Linear` and `NearestNeighbor` scaling. The benchmark performs
20 warmup frames followed by 200 measured frames and reports average milliseconds per
frame plus managed allocations per frame.

This is intentionally a narrow presentation benchmark. It does **not** include scene
rendering, GPU/WebGL work, texture upload, screenshots, window/UI dispatch, or the
desktop compositor. A monitor at the tested destination sizes is not required.

## Run

```console
dotnet run --project Testing/ViewportScalingBenchmark/ViewportScalingBenchmark.csproj -c Release
```

Representative output has the form:

```text
1920x1080 -> 1920x1080, Linear: ... ms/frame, ... managed bytes/frame
1920x1080 -> 1920x1080, NearestNeighbor: ... ms/frame, ... managed bytes/frame
1920x1080 -> 3840x2160, Linear: ... ms/frame, ... managed bytes/frame
1920x1080 -> 3840x2160, NearestNeighbor: ... ms/frame, ... managed bytes/frame
```

The benchmark is most useful for comparing presentation-filter costs and detecting
regressions in the CPU compatibility path. Results are machine-specific and should not
be treated as GPU throughput or frame-rate guarantees.

See [viewport scaling](../../docs/viewport-scaling.md) for the rendering contract,
implementation notes, validation coverage, and current representative measurements.
