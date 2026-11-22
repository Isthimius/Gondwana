const scoreKey = "gondwana.slider.high-scores.v1";
let resizeObserver = null;
let removeListeners = null;
let removeDialogTrap = null;

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

export function focusDialog(element, dotNetRef) {
    stopDialogTrap();

    const dialog = element.querySelector(".browser-dialog");
    if (!dialog)
        return;

    const focusables = () => Array.from(dialog.querySelectorAll(
        'input:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])'
    )).filter(node => node.getClientRects().length > 0);

    const onDialogKeyDown = event => {
        if (event.key === "Escape") {
            event.preventDefault();
            event.stopPropagation();
            dotNetRef.invokeMethodAsync("DismissPicker").catch(() => {});
            return;
        }

        if (event.key !== "Tab")
            return;

        const controls = focusables();
        if (!controls.length) {
            event.preventDefault();
            return;
        }

        const first = controls[0];
        const last = controls[controls.length - 1];

        if (event.shiftKey && (document.activeElement === first || !dialog.contains(document.activeElement))) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !dialog.contains(document.activeElement))) {
            event.preventDefault();
            first.focus();
        }
    };

    const keepFocusInDialog = event => {
        if (!dialog.contains(event.target))
            focusables()[0]?.focus();
    };

    document.addEventListener("keydown", onDialogKeyDown, true);
    document.addEventListener("focusin", keepFocusInDialog, true);
    removeDialogTrap = () => {
        document.removeEventListener("keydown", onDialogKeyDown, true);
        document.removeEventListener("focusin", keepFocusInDialog, true);
    };

    focusables()[0]?.focus();
}

function stopDialogTrap() {
    removeDialogTrap?.();
    removeDialogTrap = null;
}

export function restoreCanvasFocus(element) {
    stopDialogTrap();
    element.querySelector("canvas[tabindex]")?.focus();
}

export function stopWatching() {
    stopDialogTrap();
    resizeObserver?.disconnect();
    resizeObserver = null;
    removeListeners?.();
    removeListeners = null;
}
