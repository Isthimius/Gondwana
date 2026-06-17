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

    function loop() {
        if (tickCallback) {
            tickCallback.invokeMethodAsync('OnAnimationFrame');
            animationFrameId = requestAnimationFrame(loop);
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
 * @param {number} width - Frame width in pixels.
 * @param {number} height - Frame height in pixels.
 * @param {Uint8Array} data - RGBA byte array (width * height * 4 bytes, unpremultiplied).
 */
export function putImageData(canvas, width, height, data) {
    if (!canvas) return;

    const state = canvas.__gondwana ??= {};

    if (state.w !== width || state.h !== height) {
        canvas.width = width;
        canvas.height = height;

        state.ctx = canvas.getContext('2d', { alpha: false });
        state.imageData = state.ctx ? state.ctx.createImageData(width, height) : null;
        state.w = width;
        state.h = height;
    }

    const ctx = state.ctx;
    const imageData = state.imageData;
    if (!ctx || !imageData) return;

    imageData.data.set(data);
    ctx.putImageData(imageData, 0, 0);
}
