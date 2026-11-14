import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// helpers the tracker calls (Z1R_Avalonia/Browser.cs, BrowserJs)
const sounds = {};
let audioContext = null;
function playSound(file, volume) {
    // one player per file, restarted from the beginning each time (as the desktop tracker)
    const a = sounds[file] ??= new Audio(file);
    a.volume = Math.max(0, Math.min(1, volume));
    a.currentTime = 0;
    a.play().catch(() => {});   // browsers refuse sound until the page has been clicked once
}
function beep() {
    // a short tone: browsers have no system alert sound
    audioContext ??= new AudioContext();
    const osc = audioContext.createOscillator(), gain = audioContext.createGain();
    osc.frequency.value = 880;
    gain.gain.setValueAtTime(0.2, audioContext.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, audioContext.currentTime + 0.12);
    osc.connect(gain).connect(audioContext.destination);
    osc.start();
    osc.stop(audioContext.currentTime + 0.12);
}
function openUrl(url) {
    window.open(url, "_blank", "noopener");
}
function downloadText(fileName, text) {
    const a = document.createElement("a");
    a.href = URL.createObjectURL(new Blob([text], { type: "application/json" }));
    a.download = fileName;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 10000);
}
// the tracker's kept files (Z1R_Avalonia/Browser.cs, BrowserStorage)
function storageKeys(prefix) {
    const keys = [];
    for (let i = 0; i < localStorage.length; i++) {
        const k = localStorage.key(i);
        if (k.startsWith(prefix)) keys.push(k);
    }
    return keys;
}
function storageGet(key) { return localStorage.getItem(key) ?? ""; }
function storageSet(key, value) {
    try { localStorage.setItem(key, value); return true; }
    catch { return false; }   // full
}
function storageRemove(key) { localStorage.removeItem(key); }

// Two tabs of the tracker share one autosave, so each tab's autosave overwrites the other's. A new tab says hello; an open tab answers,
// and the new tab then shows a warning. (BroadcastChannel doesn't deliver a message back to the tab that sent it.)
const tabChannel = typeof BroadcastChannel != "undefined" ? new BroadcastChannel("zoratracker-tab") : null;
if (tabChannel) {
    tabChannel.onmessage = e => {
        if (e.data === "hello") tabChannel.postMessage("here");
        else if (e.data === "here") { const w = document.getElementById("tabWarn"); if (w) w.style.display = "flex"; }
    };
    tabChannel.postMessage("hello");
}

// Avalonia cancels the browser's default action for keys it handles, which stopped F5 / Ctrl+F5 / Ctrl+R from reloading the page.
// The web tracker has no use for those keys (global hotkeys are off here), so they are kept away from Avalonia and the browser reloads as usual.
document.addEventListener("keydown", e => {
    if (e.key === "F5" || ((e.ctrlKey || e.metaKey) && (e.key === "r" || e.key === "R"))) e.stopPropagation();
}, true);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

dotnetRuntime.setModuleImports('main.js', { playSound, beep, openUrl, downloadText, storageKeys, storageGet, storageSet, storageRemove });

const config = dotnetRuntime.getConfig();
const running = dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

// save changed files as the page is left, not only once a second
const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);
const flush = () => { try { exports.Z1R_Avalonia.BrowserStorage.Flush(); } catch { } };
window.addEventListener("pagehide", flush);

// Text sent as one insert (phone keyboards, IME, autocomplete) reaches Avalonia's hidden input element, but Avalonia.Browser 11.3 only takes
// typed text from keydown, so it never gets to the TextBox. A key Avalonia handles has its keydown default prevented, which means no
// beforeinput follows it; so any insertText that does arrive here was not typed into the tracker yet, and it is passed on (no double insert).
// A composition is skipped when Avalonia's own compositionend just handled it.
const outDiv = document.getElementById("out");
let lastCompositionEnd = { data: null, time: 0 };
document.addEventListener("compositionend", e => { lastCompositionEnd = { data: e.data, time: performance.now() }; }, true);
document.addEventListener("beforeinput", e => {
    if (!outDiv || !outDiv.contains(e.target)) return;
    const t = e.inputType;
    if (t !== "insertText" && t !== "insertReplacementText" && t !== "insertFromComposition") return;
    const text = e.data ?? (e.dataTransfer ? e.dataTransfer.getData("text/plain") : null);
    if (!text) return;
    if (t === "insertFromComposition" && lastCompositionEnd.data === text && performance.now() - lastCompositionEnd.time < 200) return;
    let typed = false;
    try { typed = exports.Z1R_Avalonia.BrowserTextInput.InsertText(text); } catch { }
    if (typed) e.preventDefault();   // (keep the hidden input empty, as Avalonia does)
}, true);
document.addEventListener("visibilitychange", () => { if (document.visibilityState === "hidden") flush(); });

await running;
