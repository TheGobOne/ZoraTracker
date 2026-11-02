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
document.addEventListener("visibilitychange", () => { if (document.visibilityState === "hidden") flush(); });

await running;
