import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { join, resolve, extname, sep } from 'node:path';
import { chromium } from 'playwright';

const [root, version] = process.argv.slice(2);
assert(root && version, 'Usage: node browser.mjs WORK_ROOT PACKAGE_VERSION');
// Cover browser-process/context startup too, which may hang before a page timeout exists.
const watchdog = setTimeout(() => {
    console.error('Browser consumer test exceeded its six-minute deadline');
    process.exit(1);
}, 360_000);
watchdog.unref();
const assets = JSON.parse(await readFile(join(root, 'app/obj/project.assets.json'), 'utf8'));
for (const [name, metadata] of Object.entries(assets.libraries)) {
    if (name.startsWith('Gondwana')) {
        assert.equal(name.split('/')[1], version, `Non-local dependency: ${name}`);
        assert.equal(metadata.type, 'package', `Project reference: ${name}`);
    }
}
const targets = Object.values(assets.targets);
const audio = targets.flatMap(target => Object.entries(target))
    .find(([name]) => name === `Gondwana.Audio.Browser/${version}`)?.[1];
assert(audio, 'Audio package absent from assets');
const js = Object.entries(audio.contentFiles ?? {}).find(([name]) => name.endsWith('/gondwana-audio.js'));
assert(js, 'Audio contentFiles absent');
assert.notEqual(js[1].buildAction, 'Compile');
assert.equal(js[1].copyToOutput, true);
assert(Object.keys(assets.libraries).some(name => name.startsWith('SkiaSharp.NativeAssets.WebAssembly/')),
    'Skia WebAssembly native package absent');
await mkdir(join(root, 'browser-results'), { recursive: true });

const browser = await chromium.launch({ headless: true, args: [
    '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
] });
async function verify(url, label) {
    const context = await browser.newContext();
    await context.tracing.start({ screenshots: true, snapshots: true });
    const page = await context.newPage();
    const errors = [], responses = [], messages = [];
    page.on('pageerror', error => { errors.push(error.stack); console.error(error.stack); });
    page.on('console', message => {
        messages.push(`${message.type()}: ${message.text()}`);
        if (message.type() === 'error') { errors.push(message.text()); console.error(message.text()); }
    });
    page.on('response', response => {
        responses.push({ url: response.url(), status: response.status() });
        if (response.status() >= 400) errors.push(`HTTP ${response.status()} ${response.url()}`);
    });
    page.on('requestfailed', request => errors.push(`${request.url()}: ${request.failure()?.errorText}`));
    // Observe real Skia GL submissions, without injecting a replacement renderer or scene.
    await page.addInitScript(() => {
        window.__gpu = { contexts: 0, draws: 0, textures: 0 };
        const getContext = HTMLCanvasElement.prototype.getContext;
        HTMLCanvasElement.prototype.getContext = function (type, ...args) {
            const gl = getContext.call(this, type, ...args);
            if (gl && /^webgl|^experimental-webgl/.test(type) && !gl.__observed) {
                gl.__observed = true;
                window.__gpu.contexts++;
                for (const [method, counter] of [['drawElements', 'draws'], ['drawArrays', 'draws'], ['texImage2D', 'textures']]) {
                    const original = gl[method];
                    gl[method] = function (...values) {
                        window.__gpu[counter]++;
                        return original.apply(this, values);
                    };
                }
            }
            return gl;
        };
    });
    try {
        console.log(`${label}: loading ${url}`);
        await page.goto(url, { waitUntil: 'load', timeout: 120_000 });
        await page.waitForFunction(() => {
            const canvas = document.querySelector('canvas[id^="gondwana-webgl-"]');
            return canvas?.width > 0 && canvas?.height > 0 &&
                !document.querySelector('.startup-loader') && window.__gpu.draws >= 3 && window.__gpu.textures > 0;
        }, null, { timeout: 120_000 });
        // Program awaits the JSHost audio import before rendering App. Also verify the asset URL itself.
        const audioResponse = await page.request.get(new URL('gondwana-audio.js', url).href);
        assert.equal(audioResponse.status(), 200);
        assert(responses.some(r => r.url.endsWith('/gondwana-audio.js') && r.status === 200), 'Startup audio import missing');
        assert(responses.some(r => /\.wasm(?:\?|$)/.test(r.url) && r.status === 200), 'WASM never loaded');
        const first = await page.evaluate(() => window.__gpu.draws);
        await page.waitForFunction(previous => window.__gpu.draws > previous + 3, first);
        assert.deepEqual(errors, [], 'Browser startup errors (including native loading/host failures)');
        await page.screenshot({ path: join(root, `browser-results/${label}.png`) });
        console.log(`${label}: audio import, WASM, host startup and sustained Skia/WebGL draws passed`, await page.evaluate(() => window.__gpu));
    } catch (error) {
        console.error(`${label}: ${error.message}`);
        console.error(await page.evaluate(() => ({
            gpu: window.__gpu,
            text: document.body.innerText,
            canvas: [...document.querySelectorAll('canvas')].map(c => ({ id: c.id, width: c.width, height: c.height })),
        })).catch(() => 'Page unavailable'));
        throw error;
    } finally {
        await writeFile(join(root, `browser-results/${label}.json`), JSON.stringify({ errors, responses, messages }, null, 2));
        await context.tracing.stop({ path: join(root, `browser-results/${label}.zip`) });
        await context.close();
    }
}

const contentRoot = resolve(root, 'published/wwwroot');
const mime = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.svg': 'image/svg+xml' };
const server = createServer(async (req, res) => {
    try {
        const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
        if (pathname === '/favicon.ico') { res.writeHead(204).end(); return; }
        assert(pathname.startsWith('/game/'));
        const file = resolve(contentRoot, pathname.slice('/game/'.length) || 'index.html');
        assert(file.startsWith(contentRoot + sep));
        const data = await readFile(file);
        res.writeHead(200, { 'Content-Type': mime[extname(file)] ?? 'application/octet-stream' }).end(data);
    } catch { res.writeHead(404).end(); }
});
let dev;
try {
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    await verify(`http://127.0.0.1:${server.address().port}/game/`, 'published');
    // Reserve an available port, then hand it to the DevServer.
    const reservation = createServer();
    await new Promise(resolve => reservation.listen(0, '127.0.0.1', resolve));
    const port = reservation.address().port;
    await new Promise(resolve => reservation.close(resolve));
    const url = `http://127.0.0.1:${port}/`;
    dev = spawn('dotnet', ['run', '--project', 'app/Consumer.csproj', '-c', 'Release', '--no-build', '--no-launch-profile', '--urls', url],
        { cwd: root, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    let devLog = '';
    dev.stdout.on('data', data => { devLog += data; });
    dev.stderr.on('data', data => { devLog += data; });
    let ready = false;
    for (let attempt = 0; attempt < 120; attempt++) {
        if (dev.exitCode !== null) throw new Error(`DevServer exited: ${devLog}`);
        try { ready = (await fetch(url)).ok; } catch { /* server still starting */ }
        if (ready) break;
        await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert(ready, `DevServer did not start: ${devLog}`);
    try { await verify(url, 'development'); }
    finally { await writeFile(join(root, 'browser-results/devserver.log'), devLog); }
} finally {
    if (dev) {
        if (process.platform === 'win32') {
            const kill = spawn('taskkill', ['/pid', String(dev.pid), '/T', '/F'], { windowsHide: true });
            await new Promise(resolve => kill.on('exit', resolve));
        } else dev.kill('SIGTERM');
    }
    await browser.close();
    await new Promise(resolve => server.close(resolve));
    clearTimeout(watchdog);
}
