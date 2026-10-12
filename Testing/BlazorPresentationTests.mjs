import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { putImageData, presentBitmap, getCanvasOffset } from '../Gondwana.Blazor/wwwroot/gondwana-blazor.js';

function canvas() {
    const calls = [];
    const ctx = {
        calls,
        putImageData: (...args) => calls.push(['upload', ...args]),
        fillRect: (...args) => calls.push(['clear', ...args]),
        drawImage: (...args) => calls.push(['draw', ...args]),
    };
    return { width: 0, height: 0, getContext: () => ctx, ctx };
}

globalThis.document = { createElement: () => canvas(), getElementById: () => null };
globalThis.ImageData = class { constructor(data, width, height) { Object.assign(this, { data, width, height }); } };

test('bitmap uploads stay logical while presentation resizes and clears margins', () => {
    const destination = canvas();
    putImageData(destination, 1920, 1080, 2, 3, 17, 23, new Uint8Array(24),
        3840, 2160, 0, 0, 3840, 2160, false);
    const source = destination.__gondwana.source;
    assert.equal(source.width, 1920);
    assert.equal(source.height, 1080);
    assert.equal(destination.width, 3840);
    assert.equal(destination.ctx.imageSmoothingEnabled, true);
    assert.deepEqual(source.ctx.calls[0].slice(2), [17, 23]);
    presentBitmap(destination, 1600, 1000, 0, 50, 1600, 900, true);
    assert.equal(destination.__gondwana.source, source);
    assert.equal(source.width, 1920);
    assert.equal(source.height, 1080);
    assert.deepEqual(destination.ctx.calls.at(-2), ['clear', 0, 0, 1600, 1000]);
    assert.deepEqual(destination.ctx.calls.at(-1).slice(2), [0, 0, 1920, 1080, 0, 50, 1600, 900]);
    assert.equal(destination.ctx.imageSmoothingEnabled, false);
    presentBitmap(destination, 0, 0, 0, 0, 0, 0, false);
    assert.equal(destination.width, 1600);
});

test('touch origin is read again when the canvas moves or the page scrolls', () => {
    let top = 75;
    const element = { getBoundingClientRect: () => ({ left: 125, top }) };
    assert.deepEqual(getCanvasOffset(element), { left: 125, top: 75 });
    top = -40;
    assert.deepEqual(getCanvasOffset(element), { left: 125, top: -40 });
    document.getElementById = () => element;
    assert.deepEqual(getCanvasOffset('webgl'), { left: 125, top: -40 });
});

test('generated Blazor template shares pre-WASM styles and keeps a viewport-fit surface', () => {
    const dir = new URL('../Tooling/Gondwana.Templates/templates/gondwana-blazor/', import.meta.url);
    const html = readFileSync(new URL('wwwroot/index.html', dir), 'utf8');
    const css = readFileSync(new URL('wwwroot/css/startup.css', dir), 'utf8');
    const surface = readFileSync(new URL('GameRenderSurface.razor', dir), 'utf8');

    assert.match(html, /href="css\/startup\.css"/);
    assert.match(css, /height:\s*100dvh;/);
    assert.match(css, /\.render-surface-container\s*\{/);
    assert.match(surface, /ConfigureLogicalResolution\(LogicalWidth, LogicalHeight\)/);
    assert.match(surface, /aspect-ratio:/);
    assert.match(surface, /100dvh/);
});
