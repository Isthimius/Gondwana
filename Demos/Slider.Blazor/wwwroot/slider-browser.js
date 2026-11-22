const scoreKey = "gondwana.slider.high-scores.v1";
let resizeObserver = null;
let removeListeners = null;

export function readScores() {
    try { return localStorage.getItem(scoreKey); }
    catch { return null; }
}

export function saveScores(json) {
    localStorage.setItem(scoreKey, json);
}

export function downloadSave(json) {
    const blob = new Blob([json], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "Slider.slider.json";
    document.body.appendChild(link);
    try { link.click(); }
    finally {
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
}

export function getSize(element) {
    const rect = element.getBoundingClientRect();
    return { width: Math.max(1, Math.round(rect.width)), height: Math.max(1, Math.round(rect.height)) };
}

export function watchSurface(element, dotNetRef) {
    stopWatching();

    resizeObserver = new ResizeObserver(entries => {
        const rect = entries[0]?.contentRect;
        if (rect) {
            dotNetRef.invokeMethodAsync("UpdateSize",
                Math.max(1, Math.round(rect.width)),
                Math.max(1, Math.round(rect.height))).catch(() => {});
        }
    });
    resizeObserver.observe(element);

    const onKeyDown = event => {
        if (event.key === "Shift")
            dotNetRef.invokeMethodAsync("SetShiftFromBrowser", true).catch(() => {});
    };
    const onKeyUp = event => {
        if (event.key === "Shift")
            dotNetRef.invokeMethodAsync("SetShiftFromBrowser", false).catch(() => {});
    };
    const onBlur = () => {
        dotNetRef.invokeMethodAsync("SetShiftFromBrowser", false).catch(() => {});
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    window.addEventListener("blur", onBlur);
    document.addEventListener("visibilitychange", onBlur);

    removeListeners = () => {
        window.removeEventListener("keydown", onKeyDown);
        window.removeEventListener("keyup", onKeyUp);
        window.removeEventListener("blur", onBlur);
        document.removeEventListener("visibilitychange", onBlur);
    };
}

export function stopWatching() {
    resizeObserver?.disconnect();
    resizeObserver = null;
    removeListeners?.();
    removeListeners = null;
}
