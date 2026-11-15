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
// (site data can be blocked, which makes every localStorage call throw: reads then find nothing, and writes and removes do nothing)
function storageKeys(prefix) {
    const keys = [];
    try {
        for (let i = 0; i < localStorage.length; i++) {
            const k = localStorage.key(i);
            if (k && k.startsWith(prefix)) keys.push(k);
        }
    } catch { return []; }
    return keys;
}
function storageGet(key) {
    try { return localStorage.getItem(key) ?? ""; }
    catch { return ""; }
}
function storageSet(key, value) {
    try {
        const file = key.startsWith("zt:");
        if (file && localStorage.getItem(key) === value) return true;   // (the same content: nothing to write, and no new progress)
        localStorage.setItem(key, value);
        if (file) noteWrite();
        return true;
    }
    catch { return false; }   // full
}
// "zt-writes" says which tab last wrote a kept file with new content, and a count: "<tab id>:<n>". A paused tab compares it on bye (see below).
const writesKey = "zt-writes";
function readWrites() { try { return localStorage.getItem(writesKey) ?? ""; } catch { return ""; } }
function noteWrite() {
    const n = parseInt(readWrites().split(":").pop()) || 0;
    const v = `${thisTab.id}:${n + 1}`;
    try { localStorage.setItem(writesKey, v); }
    catch { try { localStorage.removeItem(writesKey); localStorage.setItem(writesKey, v); } catch { } }   // (full: freeing the old value makes room)
}
function storageRemove(key) {
    try { localStorage.removeItem(key); } catch { }
}
// Two tabs of the tracker share one set of kept files (the autosave, the settings), so each tab's saves would overwrite the other's: only one
// tab saves at a time, and the others pause (BrowserStorage.Flush asks storagePaused). A tab makes a claim, its time and a random id, when it
// opens and again when the player picks it with "Use this tab"; every tab hears every claim, and the tab with the latest one saves. So all the
// tabs agree which tab saves, even when two open at the same moment. A tab says bye as it closes; when the saving tab says bye, the tab
// left with the latest claim takes over and saves at once, but only if no other tab wrote new content while it was paused (zt-writes is what it was when this
// tab paused, or this tab wrote it last). Otherwise the other tab saved newer progress, which resuming would overwrite: this tab stays
// paused and asks for a reload. (BroadcastChannel doesn't deliver a message back to the tab that sent it.)
const tabChannel = typeof BroadcastChannel != "undefined" ? new BroadcastChannel("zoratracker-tab") : null;
const thisTab = { id: Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2), born: Date.now() };
let myClaim = thisTab.born;
let holder = { t: myClaim, id: thisTab.id };   // (the latest claim heard, this tab's own included: that tab saves)
const otherTabs = new Map();   // the other open tabs of the tracker: id -> the latest claim each made
const later = (a, b) => a.t > b.t || (a.t === b.t && a.id > b.id);   // (the id breaks a tie, so two claims are never equal)
function storagePaused() { return holder.id !== thisTab.id; }
let flushNow = () => {}, flushAllNow = () => {};   // (set once .NET is running)
const pageTitle = document.title;
let wasPaused = false;
let writesWhenPaused = "";   // (zt-writes when this tab paused)
let staleTab = false;   // (another tab saved newer progress while this one was paused)
const bannerText = el("tabBanner")?.textContent ?? "";

function tabPost(kind, legacy = kind === "hello" || kind === "takeover") {
    // (writes: zt-writes as this message is sent, before the sender saves anything, so a tab this message pauses knows where it stood)
    // (a tab from before 1.0 knows only the text "hello", which pauses it: hello and takeover send it first, and legacy: true then tells the
    // tabs of this build that the "hello" just before came from this tab, not from an old one)
    try {
        if (!tabChannel) return;
        if (legacy) tabChannel.postMessage("hello");
        tabChannel.postMessage({ kind, id: thisTab.id, born: thisTab.born, claim: myClaim, writes: readWrites(), legacy });
    } catch { }
}
// A "hello" text is matched with the legacy: true message that follows it from the same tab (a channel keeps one sender's messages in
// order). One still unmatched a moment later came from a tab from before 1.0: that tab saves (it never pauses on its own), so this tab pauses.
let legacyHellos = 0;
function legacyHelloHeard() {
    legacyHellos++;
    setTimeout(() => {
        if (legacyHellos <= 0) return;
        legacyHellos--;
        if (!storagePaused()) flushNow();   // (keep the last second of changes before stopping)
        holder = { t: Math.max(Date.now(), holder.t + 1), id: "old-build" };   // (an old tab never says bye: only Use this tab or a reload saves here again)
        try { tabChannel.postMessage("here"); } catch { }   // (the old tab then shows its own notice)
        tabStateChanged(false);
    }, 300);
}
function el(id) { return document.getElementById(id); }
function showTabWarn(mode) {   // "paused", "stale" (paused, and the other tab saved newer progress) or "info" (this tab saves)
    const w = el("tabWarn");
    if (!w) return;
    el("tabWarnTitle").textContent = mode === "info" ? "ZORATracker is open in another tab" : "This tab has stopped saving";
    el("tabWarnText").textContent =
        mode === "paused" ? "ZORATracker is open in another tab, and only one tab can save (each would overwrite the other's progress). Changes made in this tab are not kept. Click Use this tab to save here instead (the other tab then stops saving), or close this tab."
      : mode === "stale" ? "Another tab saved newer progress while this tab wasn't saving. Reload this tab to pick that progress up. Using this tab instead overwrites it with this tab's older progress."
      : "Only one tab can save, so the other tab has stopped saving and this tab's progress is kept. Close the other tab.";
    el("tabWarnUse").style.display = mode === "info" ? "none" : "";
    el("tabWarnOk").textContent = mode === "info" ? "OK" : "Not now";
    w.style.display = "flex";
}
function hideTabWarn() { const w = el("tabWarn"); if (w) w.style.display = "none"; }
// shows whether this tab saves (the notice, the banner and the page title), and saves at once when this tab has just started saving again;
// writesThen is zt-writes as the message that paused this tab was sent
function tabStateChanged(saveAll, writesThen) {
    const paused = storagePaused();
    if (paused !== wasPaused) {
        wasPaused = paused;
        if (paused) {
            writesWhenPaused = typeof writesThen === "string" ? writesThen : readWrites();
            showTabWarn("paused");
        }
        else {
            staleTab = false;
            hideTabWarn();
            (saveAll ? flushAllNow : flushNow)();
        }
    }
    const banner = el("tabBanner");
    if (banner) {
        banner.style.display = paused ? "block" : "none";   // (stays up after the notice is closed, while this tab isn't saving)
        banner.textContent = staleTab ? "Not saving: another tab saved newer progress. Reload this tab to pick it up." : bannerText;
    }
    document.title = paused ? "This tab has stopped saving" : otherTabs.size > 0 ? "ZORATracker is open in another tab" : pageTitle;
}
// true when another tab wrote new content to a kept file since this tab paused (a paused tab never writes, so this tab writing last means no)
function otherTabWrote() {
    const now = readWrites();
    return now !== writesWhenPaused && !now.startsWith(thisTab.id + ":");
}
// makes this tab the one that saves; saveAll writes every file now (the player picked this tab, so its files should win)
function takeOver(saveAll) {
    myClaim = Math.max(Date.now(), holder.t + 1);   // (later than every claim heard, even if the clock went back)
    holder = { t: myClaim, id: thisTab.id };
    tabPost("takeover");
    tabStateChanged(saveAll);
}
// "Use this tab" and the banner: after another tab saved newer progress, only once the player confirms overwriting it
function useThisTab() {
    if (!storagePaused()) return;
    if (staleTab && !confirm("Another tab saved newer progress after this tab stopped saving.\nUsing this tab overwrites it with this tab's older progress (reload this tab instead to keep it).\n\nUse this tab anyway?")) return;
    takeOver(true);
}
if (tabChannel) {
    tabChannel.onmessage = e => {
        const m = e.data;
        if (m === "hello") { legacyHelloHeard(); return; }   // (an older build's tab, or the text this build sends first: see tabPost)
        if (!m || typeof m !== "object" || typeof m.id !== "string") return;   // (an older build's "here" text is ignored)
        if (m.legacy === true && legacyHellos > 0) legacyHellos--;
        if (m.kind === "bye") {
            otherTabs.delete(m.id);
            if (m.id === holder.id && storagePaused()) {
                // the saving tab closed. If it saved new progress while this tab was paused, this tab stays paused (resuming would overwrite
                // that progress with older files). Otherwise the tab left with the latest claim takes over: it saved most recently, so it
                // paused last (every tab left picks the same one, as they all know the same claims; when that one is stale, so are the others).
                if (otherTabWrote()) {
                    if (!staleTab) { staleTab = true; showTabWarn("stale"); }
                }
                else {
                    let next = { t: myClaim, id: thisTab.id };
                    for (const [id, claim] of otherTabs) if (later({ t: claim, id }, next)) next = { t: claim, id };
                    if (next.id === thisTab.id) { takeOver(false); return; }
                }
            }
            tabStateChanged(false);
            return;
        }
        // hello (a tab just opened), here (an open tab answering a hello) or takeover (a tab the player picked)
        let writesThen = m.writes;
        if (typeof m.claim === "number") otherTabs.set(m.id, m.claim);
        if (typeof m.claim === "number" && later({ t: m.claim, id: m.id }, holder)) {
            // (a new tab pauses this one: save the last second of changes first, so the new tab reads them as it starts. Not on takeover:
            // the player picked the other tab, so its files win)
            if (m.kind === "hello" && !storagePaused()) { flushNow(); writesThen = readWrites(); }
            holder = { t: m.claim, id: m.id };
        }
        if (m.kind === "hello") tabPost("here");
        tabStateChanged(false, writesThen);
        if (m.kind === "here" && !storagePaused()) showTabWarn("info");   // (this tab just opened and saves; another tab was already open)
    };
    tabPost("hello");
}
el("tabWarnUse")?.addEventListener("click", useThisTab);
el("tabBanner")?.addEventListener("click", useThisTab);
el("tabWarnOk")?.addEventListener("click", hideTabWarn);
// save changed files as the page is left, not only once a second, then tell the other tabs this one is gone
window.addEventListener("pagehide", () => {
    flushNow();
    if (!storagePaused()) writesWhenPaused = readWrites();   // (this tab stops saving here: the page cache can bring it back, see pageshow)
    tabPost("bye");
});
// (back from the browser's page cache: the other tabs heard this tab's bye, so it opens again as a new tab, which saves. But if another tab
// saved while this page was away, this page's files are older: it stays paused, as a tab does when the saving tab closes after saving)
window.addEventListener("pageshow", e => {
    if (!e.persisted || !tabChannel) return;
    otherTabs.clear();
    if (staleTab || otherTabWrote()) {
        myClaim = 0;   // (older than any tab's claim: this tab's hello pauses no one)
        holder = { t: 0, id: "" };   // (no tab: this tab stays paused even with no other tab open, until Use this tab or a reload)
        wasPaused = true;   // (so writesWhenPaused keeps what it was as this page was left)
        staleTab = true;
        tabPost("hello", false);   // (no "hello" text: a tab from before 1.0 may hold the newer progress, so it keeps saving)
        showTabWarn("stale");
        tabStateChanged(false);
        return;
    }
    myClaim = thisTab.born = Math.max(Date.now(), holder.t + 1);
    holder = { t: myClaim, id: thisTab.id };
    tabPost("hello");
    tabStateChanged(false);
});

// Avalonia cancels the browser's default action for keys it handles, which stopped F5 / Ctrl+F5 / Ctrl+R from reloading the page.
// The web tracker has no use for those keys (global hotkeys are off here), so they are kept away from Avalonia and the browser reloads as usual.
document.addEventListener("keydown", e => {
    if (e.key === "F5" || ((e.ctrlKey || e.metaKey) && (e.key === "r" || e.key === "R"))) e.stopPropagation();
}, true);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

dotnetRuntime.setModuleImports('main.js', { playSound, beep, openUrl, downloadText, storageKeys, storageGet, storageSet, storageRemove, storagePaused });

const config = dotnetRuntime.getConfig();
const running = dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);
const flush = () => { try { exports.Z1R_Avalonia.BrowserStorage.Flush(); } catch { } };
flushNow = flush;
flushAllNow = () => { try { exports.Z1R_Avalonia.BrowserStorage.FlushAll(); } catch { flush(); } };

// Text sent as one insert (phone keyboards, IME, autocomplete) reaches Avalonia's hidden input element, but Avalonia.Browser 11.3 only takes
// typed text from keydown, so it never gets to the TextBox. A key Avalonia handles has its keydown default prevented, which means no
// beforeinput follows it; so any insertText that does arrive here was not typed into the tracker yet, and it is passed on (no double insert).
// A composition is skipped when Avalonia's own compositionend handles it, whichever comes first: Chrome sends compositionend before the
// insertFromComposition beforeinput, Safari sends it after, so a composition with no compositionend just before it waits briefly for one.
const outDiv = document.getElementById("out");
const insertText = text => { try { return exports.Z1R_Avalonia.BrowserTextInput.InsertText(text); } catch { return false; } };
let lastCompositionEnd = { data: null, time: 0 };
let pendingComposition = null;   // (insertFromComposition text waiting to see whether a compositionend for it follows)
document.addEventListener("compositionend", e => {
    lastCompositionEnd = { data: e.data, time: performance.now() };
    if (pendingComposition && pendingComposition.text === e.data) { clearTimeout(pendingComposition.timer); pendingComposition = null; }
}, true);
document.addEventListener("beforeinput", e => {
    if (!outDiv || !outDiv.contains(e.target)) return;
    const t = e.inputType;
    if (t !== "insertText" && t !== "insertReplacementText" && t !== "insertFromComposition") return;
    const text = e.data ?? (e.dataTransfer ? e.dataTransfer.getData("text/plain") : null);
    if (!text) return;
    if (t === "insertFromComposition") {
        if (lastCompositionEnd.data === text && performance.now() - lastCompositionEnd.time < 200) return;
        if (pendingComposition) { clearTimeout(pendingComposition.timer); insertText(pendingComposition.text); }
        const p = { text, timer: 0 };
        p.timer = setTimeout(() => { if (pendingComposition === p) { pendingComposition = null; insertText(text); } }, 100);
        pendingComposition = p;
        return;
    }
    if (insertText(text)) e.preventDefault();   // (keep the hidden input empty, as Avalonia does)
}, true);
document.addEventListener("visibilitychange", () => { if (document.visibilityState === "hidden") flush(); });

await running;
