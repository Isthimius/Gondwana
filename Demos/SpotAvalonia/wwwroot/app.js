// Avalonia Browser bootstrap for .NET 8
import { dotnet } from './_framework/dotnet.js';

const { setModuleImports, getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

console.log('Starting Avalonia Browser app...');
await dotnet.run();
console.log('Avalonia app started');
