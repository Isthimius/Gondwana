import { dotnet } from '../dotnet.js'

const response = await fetch('./wasm-assets.json');
if (!response.ok) {
    throw new Error(`Failed to load wasm-assets.json (${response.status} ${response.statusText})`);
}

let config;
try {
    config = await response.json();
} catch (error) {
    throw new Error(`Failed to parse wasm-assets.json: ${error instanceof Error ? error.message : error}`);
}

const dotnetRuntime = await dotnet
    .withConfig(config)
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

await dotnetRuntime.runMain(config.mainAssemblyName, [window.location.search]);
