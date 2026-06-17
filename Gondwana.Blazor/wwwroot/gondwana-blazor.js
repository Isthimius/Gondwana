// Gondwana Blazor render helper – presents pixel data onto an HTML canvas element.
// Imported as a JS module by BlazorBitmapRenderSurfaceComponent.

let animationFrameId = null;
let tickCallback = null;

/**
 * Gets the client (CSS) dimensions of the canvas element.
 * @param {HTMLCanvasElement} canvas - The target canvas element.
 * @returns {{width: number, height: number}} The client width and height.
 */
export function getCanvasSize(canvas) {
    if (!canvas) return { width: 1, height: 1 };
    return {
        width: canvas.clientWidth || 1,
        height: canvas.clientHeight || 1
    };
}

/**
 * Starts the render loop using requestAnimationFrame.
 * @param {object} dotnetHelper - .NET object reference with invokable tick method.
 */
export function startRenderLoop(dotnetHelper) {
    tickCallback = dotnetHelper;

    async function loop() {
        if (!tickCallback) return;

        try {
            // In WebAssembly, DotNetObjectReference exposes sync invokeMethod; other hosts may only provide async invokeMethodAsync.
            if (typeof tickCallback.invokeMethod === 'function') {
                tickCallback.invokeMethod('OnAnimationFrame');
            } else {
                await tickCallback.invokeMethodAsync('OnAnimationFrame');
            }
        } finally {
            if (tickCallback) {
                animationFrameId = requestAnimationFrame(loop);
            }
        }
    }

    animationFrameId = requestAnimationFrame(loop);
}

/**
 * Stops the render loop.
 */
export function stopRenderLoop() {
    if (animationFrameId !== null) {
        cancelAnimationFrame(animationFrameId);
        animationFrameId = null;
    }
    tickCallback = null;
}

/**
 * Renders an RGBA pixel buffer onto a canvas element via the Canvas 2D API.
 * @param {HTMLCanvasElement} canvas - The target canvas element.
 * @param {number} canvasWidth - Canvas width in pixels.
 * @param {number} canvasHeight - Canvas height in pixels.
 * @param {number} width - Frame region width in pixels.
 * @param {number} height - Frame region height in pixels.
 * @param {number} x - Destination X position in canvas pixel coordinates.
 * @param {number} y - Destination Y position in canvas pixel coordinates.
 * @param {Uint8Array} data - RGBA byte array (width * height * 4 bytes, unpremultiplied).
 */
export function putImageData(canvas, canvasWidth, canvasHeight, width, height, x, y, data) {
    if (!canvas) return;

    const state = canvas.__gondwana ??= {};

    if (state.w !== canvasWidth || state.h !== canvasHeight) {
        canvas.width = canvasWidth;
        canvas.height = canvasHeight;

        state.ctx = canvas.getContext('2d', { alpha: false });
        state.w = canvasWidth;
        state.h = canvasHeight;
    }

    const ctx = state.ctx;
    if (!ctx) return;

    const rgba = new Uint8ClampedArray(data.buffer, data.byteOffset, data.byteLength);
    const imageData = new ImageData(rgba, width, height);
    ctx.putImageData(imageData, x, y);
}
