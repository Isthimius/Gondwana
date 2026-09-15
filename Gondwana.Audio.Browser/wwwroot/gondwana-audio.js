/**
 * Browser/WASM audio backend for Gondwana.
 * Import before using Gondwana.Audio.Browser:
 *
 *   await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
 */

/** @type {Map<string, {audio: HTMLAudioElement, context: AudioContext|null, source: MediaElementAudioSourceNode|null, panner: StereoPannerNode|null, state: number}>} */
const _players = new Map();

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function disposeEntry(entry) {
    entry.disposed = true;
    entry.audio.removeEventListener("ended", entry.onEnded);
    entry.audio.pause();
    entry.state = 0;
    try { entry.source?.disconnect(); } catch { }
    try { entry.panner?.disconnect(); } catch { }
    try { entry.context?.close().catch(() => { }); } catch { }
    entry.audio.removeAttribute("src");
    entry.audio.load();
}

/**
 * Loads a URI-addressable browser audio track without starting playback.
 */
export function load(key, src, loop, volume, pan, playbackSpeed, onEnded) {
    const existing = _players.get(key);
    if (existing) disposeEntry(existing);

    let audio = new Audio();

    let context = null;
    let source = null;
    let panner = null;

    const AudioContextType = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (AudioContextType) {
        try {
            context = new AudioContextType();
            source = context.createMediaElementSource(audio);
            if (typeof context.createStereoPanner === "function") {
                panner = context.createStereoPanner();
                panner.pan.value = clamp(pan, -1, 1);
                source.connect(panner);
                panner.connect(context.destination);
            } else {
                source.connect(context.destination);
            }
        } catch {
            try { source?.disconnect(); } catch { }
            try { panner?.disconnect(); } catch { }
            try { context?.close().catch(() => { }); } catch { }
            // A media element remains bound to a failed Web Audio source. Use a
            // fresh element so the ordinary media fallback can still be heard.
            audio = new Audio();
            context = null;
            source = null;
            panner = null;
        }
    }

    // Set CORS before src so cross-origin requests use the correct mode.
    audio.crossOrigin = "anonymous";
    audio.src = src;
    audio.loop = loop;
    audio.volume = clamp(volume, 0, 1);
    audio.playbackRate = clamp(playbackSpeed, 0.25, 4);

    const entry = { audio, context, source, panner, state: 0, disposed: false };
    entry.onEnded = () => {
        if (entry.disposed || audio.loop) return;
        entry.state = 0;
        onEnded?.();
    };
    audio.addEventListener("ended", entry.onEnded);
    _players.set(key, entry);
}

export function play(key, fromStart) {
    const entry = _players.get(key);
    if (!entry) return;

    if (fromStart) entry.audio.currentTime = 0;
    if (entry.context?.state === "suspended") entry.context.resume().catch(() => { });

    entry.state = 1;
    entry.audio.play().catch(() => {
        if (!entry.disposed && entry.state === 1 && entry.audio.paused)
            entry.state = entry.audio.currentTime > 0 ? 2 : 0;
    });
}

export function pause(key) {
    const entry = _players.get(key);
    if (!entry) return;
    entry.audio.pause();
    if (entry.state === 1) entry.state = 2;
}

export function stop(key) {
    const entry = _players.get(key);
    if (!entry) return;
    entry.audio.pause();
    entry.audio.currentTime = 0;
    entry.state = 0;
}

export function setVolume(key, volume) {
    const entry = _players.get(key);
    if (entry) entry.audio.volume = clamp(volume, 0, 1);
}

export function setLoop(key, loop) {
    const entry = _players.get(key);
    if (entry) entry.audio.loop = loop;
}

export function setPan(key, pan) {
    const entry = _players.get(key);
    if (entry?.panner) entry.panner.pan.value = clamp(pan, -1, 1);
}

export function setPlaybackSpeed(key, playbackSpeed) {
    const entry = _players.get(key);
    if (entry) entry.audio.playbackRate = clamp(playbackSpeed, 0.25, 4);
}

export function setCurrentTime(key, seconds) {
    const entry = _players.get(key);
    if (!entry) return;

    const duration = Number.isFinite(entry.audio.duration) ? entry.audio.duration : Number.POSITIVE_INFINITY;
    entry.audio.currentTime = clamp(seconds, 0, duration);
}

export function getCurrentTime(key) {
    const entry = _players.get(key);
    return entry ? entry.audio.currentTime || 0 : 0;
}

export function getDuration(key) {
    const entry = _players.get(key);
    return entry && Number.isFinite(entry.audio.duration) ? entry.audio.duration : 0;
}

// 0 = stopped, 1 = playing, 2 = paused
export function getState(key) {
    const entry = _players.get(key);
    return entry ? entry.state : 0;
}

export function unload(key) {
    const entry = _players.get(key);
    if (!entry) return;
    disposeEntry(entry);
    _players.delete(key);
}
