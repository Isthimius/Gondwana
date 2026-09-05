// Gondwana Blazor render helper – presents pixel data onto an HTML canvas element.
// Imported as a JS module by BlazorBitmapRenderSurfaceComponent.

let animationFrameId = null;
let tickCallback = null;

/**
 * Focuses an element without scrolling it into view.
 * @param {string} elementId - The target element ID.
 */
export function focusElementById(elementId) {
    document.getElementById(elementId)?.focus({ preventScroll: true });
}

/**
 * Prevents browser scrolling and navigation gestures on a Gondwana input canvas.
 * Component event modifiers cannot be forwarded through SKGLView's unmatched attributes,
 * so the WebGL path installs the equivalent native listeners once the canvas exists.
 * @param {string} elementId - The target canvas element ID.
 */
export function suppressBrowserInputDefaultsById(elementId) {
    const canvas = document.getElementById(elementId);
    if (!canvas || canvas.__gondwanaInputDefaultsSuppressed) return;

    for (const eventName of ['keydown', 'wheel', 'touchstart']) {
        canvas.addEventListener(eventName, event => event.preventDefault(), { passive: false });
    }

    canvas.__gondwanaInputDefaultsSuppressed = true;
}

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
 * Observes the canvas client size and notifies .NET whenever it changes.
 * @param {HTMLCanvasElement} canvas - The target canvas element.
 * @param {object} dotnetHelper - .NET object reference with invokable size-changed method.
 */
export function observeCanvasSize(canvas, dotnetHelper) {
    if (!canvas || !dotnetHelper) return;

    const state = canvas.__gondwana ??= {};

    if (state.resizeObserver) {
        state.resizeObserver.disconnect();
    }

    const notifySize = () => {
        const size = getCanvasSize(canvas);
        dotnetHelper.invokeMethodAsync('OnCanvasSizeChanged', size.width, size.height);
    };

    if (typeof ResizeObserver === 'function') {
        state.resizeObserver = new ResizeObserver(() => notifySize());
        state.resizeObserver.observe(canvas);
    } else {
        state.resizeListener = () => notifySize();
        window.addEventListener('resize', state.resizeListener);
    }

    notifySize();
}

/**
 * Stops observing canvas client size changes.
 * @param {HTMLCanvasElement} canvas - The target canvas element.
 */
export function unobserveCanvasSize(canvas) {
    if (!canvas?.__gondwana) return;

    const state = canvas.__gondwana;
    state.resizeObserver?.disconnect();
    state.resizeObserver = null;

    if (state.resizeListener) {
        window.removeEventListener('resize', state.resizeListener);
        state.resizeListener = null;
    }
}

/**
 * Starts the render loop using requestAnimationFrame.
 * @param {object} dotnetHelper - .NET object reference with invokable tick method.
 */
export function startRenderLoop(dotnetHelper) {
    tickCallback = dotnetHelper;
    let syncInvokeFailed = false;

    async function invokeTick() {
        if (typeof tickCallback.invokeMethod === 'function') {
            try {
                tickCallback.invokeMethod('OnAnimationFrame');
                return;
            } catch (error) {
                if (!syncInvokeFailed) {
                    syncInvokeFailed = true;
                    console.warn('Gondwana.Blazor falling back to invokeMethodAsync for render-loop ticks.', error);
                }
                // Some runtimes expose invokeMethod but do not support this call path reliably.
            }
        }

        await tickCallback.invokeMethodAsync('OnAnimationFrame');
    }

    async function loop() {
        if (!tickCallback) return;

        try {
            await invokeTick();
            if (tickCallback) {
                animationFrameId = requestAnimationFrame(loop);
            }
        } catch (error) {
            console.error('Gondwana.Blazor render loop stopped after OnAnimationFrame failed.', error);
            stopRenderLoop();
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
