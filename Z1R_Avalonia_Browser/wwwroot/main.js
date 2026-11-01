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

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

dotnetRuntime.setModuleImports('main.js', { playSound, beep, openUrl });

const config = dotnetRuntime.getConfig();
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
