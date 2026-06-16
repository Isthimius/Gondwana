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
    if (canvas.width !== width) canvas.width = width;
    if (canvas.height !== height) canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    const imageData = new ImageData(new Uint8ClampedArray(data), width, height);
    ctx.putImageData(imageData, 0, 0);
}
