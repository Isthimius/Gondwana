/**
 * gondwana-audio.js
 *
 * Lightweight HTML5 Audio playback module for Gondwana WASM games.
 * Import this module before starting Avalonia:
 *
 *   await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");
 *
 * All functions are keyed by a string identifier that matches the key
 * passed to BrowserAudioManager.Load() on the C# side.
 */

/** @type {Map<string, HTMLAudioElement>} */
const _players = new Map();

/**
 * Loads a new audio track and optionally starts playing it immediately.
 * If a track with the same key already exists it is stopped and replaced.
 * @param {string} key    - Unique identifier for this track.
 * @param {string} src    - Relative or absolute URL of the audio file.
 * @param {boolean} loop  - Whether the track should loop.
 * @param {number} volume - Initial volume in the range [0, 1].
 */
export function load(key, src, loop, volume) {
    const existing = _players.get(key);
    if (existing) {
        existing.pause();
        existing.src = "";
    }

    const audio = new Audio(src);
    audio.loop = loop;
    audio.volume = Math.max(0, Math.min(1, volume));
    _players.set(key, audio);
}

/**
 * Starts (or resumes) playback of a loaded track.
 * @param {string} key       - The track identifier.
 * @param {boolean} fromStart - If true, seek to the beginning before playing.
 */
export function play(key, fromStart) {
    const audio = _players.get(key);
    if (!audio) return;
    if (fromStart) audio.currentTime = 0;
    audio.play().catch(() => { /* autoplay policy — user must interact first */ });
}

/**
 * Pauses playback of a loaded track without resetting its position.
 * @param {string} key - The track identifier.
 */
export function pause(key) {
    const audio = _players.get(key);
    if (audio) audio.pause();
}

/**
 * Stops playback and resets the track to the beginning.
 * @param {string} key - The track identifier.
 */
export function stop(key) {
    const audio = _players.get(key);
    if (!audio) return;
    audio.pause();
    audio.currentTime = 0;
}

/**
 * Sets the volume of a loaded track.
 * @param {string} key    - The track identifier.
 * @param {number} volume - Volume in the range [0, 1].
 */
export function setVolume(key, volume) {
    const audio = _players.get(key);
    if (audio) audio.volume = Math.max(0, Math.min(1, volume));
}

/**
 * Enables or disables looping for a loaded track.
 * @param {string} key   - The track identifier.
 * @param {boolean} loop - Whether the track should loop.
 */
export function setLoop(key, loop) {
    const audio = _players.get(key);
    if (audio) audio.loop = loop;
}

/**
 * Unloads a track and releases its resources.
 * @param {string} key - The track identifier.
 */
export function unload(key) {
    const audio = _players.get(key);
    if (!audio) return;
    audio.pause();
    audio.src = "";
    _players.delete(key);
}
