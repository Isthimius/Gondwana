// Gondwana Blazor render helper – presents pixel data onto an HTML canvas element.
// Imported as a JS module by BlazorBitmapRenderSurfaceComponent.

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

        state.ctx = canvas.getContext('2d');
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
