import { test } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const elements = [];
const contexts = [];
class FakeAudio extends EventTarget {
    constructor(src) { super(); this.src = src; this.currentTime = 0; this.duration = 10; this.paused = true; elements.push(this); }
    play() { this.paused = false; return Promise.resolve(); }
    pause() { this.paused = true; }
    removeAttribute() { this.src = ""; }
    load() { }
}
globalThis.Audio = FakeAudio;
globalThis.AudioContext = class {
    constructor() { contexts.push(this); }
    state = "suspended";
    destination = {};
    failSource = false;
    closeCalls = 0;
    createMediaElementSource() {
        if (this.failSource) throw new Error("Unavailable");
        return { connect() {}, disconnect() {} };
    }
    createStereoPanner() { return { pan: { value: 0 }, connect() {}, disconnect() {} }; }
    resume() { this.state = "running"; return Promise.resolve(); }
    close() { this.closeCalls++; this.state = "closed"; return Promise.resolve(); }
};
const source = await readFile(new URL("../Gondwana.Audio.Browser/wwwroot/gondwana-audio.js", import.meta.url), "utf8");
const audio = await import(`data:text/javascript;base64,${Buffer.from(source).toString("base64")}`);

test("portable browser controls, natural completion and callback disposal", () => {
    let completed = 0;
    audio.load("music", "music.ogg", false, .5, -.2, 2, () => completed++);
    const media = elements.at(-1);
    assert.equal(media.playbackRate, 2);
    audio.play("music", true);
    assert.equal(audio.getState("music"), 1);
    audio.pause("music");
    assert.equal(audio.getState("music"), 2);
    audio.setCurrentTime("music", 20);
    assert.equal(audio.getCurrentTime("music"), 10);
    audio.setPlaybackSpeed("music", 0);
    assert.equal(media.playbackRate, .25);
    audio.stop("music");
    assert.equal(completed, 0);
    assert.equal(audio.getCurrentTime("music"), 0);
    audio.play("music", true);
    media.dispatchEvent(new Event("ended"));
    assert.equal(completed, 1);
    assert.equal(audio.getState("music"), 0);
    audio.setLoop("music", true);
    media.dispatchEvent(new Event("ended"));
    assert.equal(completed, 1);
    audio.unload("music");
    media.dispatchEvent(new Event("ended"));
    assert.equal(completed, 1);
    assert.equal(media.src, "");
});

test("replacement removes old callbacks and unavailable metadata is safe", () => {
    let completed = 0;
    audio.load("music", "one.ogg", false, 1, 0, 1, () => completed++);
    const old = elements.at(-1);
    audio.load("music", "two.ogg", false, 1, 0, 1, () => completed++);
    old.dispatchEvent(new Event("ended"));
    assert.equal(completed, 0);
    elements.at(-1).duration = NaN;
    assert.equal(audio.getDuration("music"), 0);
    audio.unload("music");
});

test("failed Web Audio setup falls back to a fresh media element", () => {
    contexts[0].failSource = true;
    const count = elements.length;
    audio.load("fallback", "music.ogg", false, .7, 0, 1);
    assert.equal(elements.length, count + 2);
    assert.equal(elements.at(-1).src, "music.ogg");
    audio.play("fallback", true);
    assert.equal(elements.at(-1).paused, false);
    audio.unload("fallback");
    contexts[0].failSource = false;
    assert.equal(contexts[0].closeCalls, 0);
});

test("a sound bank shares one context and unloading tracks preserves other playback", () => {
    for (let i = 0; i < 100; i++) audio.load(`bank-${i}`, "sound.ogg", false, 1, 0, 1);
    assert.equal(contexts.length, 1);
    audio.play("bank-99", true);
    for (let i = 0; i < 99; i++) audio.unload(`bank-${i}`);
    assert.equal(audio.getState("bank-99"), 1);
    assert.equal(contexts[0].closeCalls, 0);
    audio.unload("bank-99");
    audio.load("next-bank", "sound.ogg", false, 1, 0, 1);
    assert.equal(contexts.length, 1);
    audio.unload("next-bank");
});

test("the checked-in demo module matches the backend module", async () => {
    const demo = await readFile(new URL("../Demos/Spot.Blazor/wwwroot/gondwana-audio.js", import.meta.url), "utf8");
    assert.equal(demo.replaceAll("\r\n", "\n"), source.replaceAll("\r\n", "\n"));
});
