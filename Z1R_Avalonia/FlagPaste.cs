// Port of Z1R_WPF/FlagPaste.fs. Straight translation: Tri is an enum; the records are C# records; Result<T, string> is a (value, error) pair,
// with error null on success.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Z1R_Avalonia;

// Reads the flag strings from the ZORA site (the Z1R string and the ZORA string) and reports the tracker's startup settings they imply.
// The formats are from the ZORA repo (zora/flags/codec.py, fields.py and zora_flags.py): both are plain base-63 numbers in mixed radix.
public static class FlagPaste
{
    private const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!";

    // a three-state flag: off, on, or "random" (decided per seed, so it can't be known from the string)
    public enum Tri { Off, On, Random }
    private static Tri tri(int n) => n switch { 0 => Tri.Off, 1 => Tri.On, _ => Tri.Random };

    // the Z1R string: the 27 option fields (C01..C27) by radix ("slots"), then 91 three-state fields (B01..B91); C01 is the least significant
    private static readonly int[] z1rOptionSlots = { 8, 10, 10, 7, 8, 8, 8, 15, 8, 8, 20, 20, 20, 7, 7, 20, 26, 13, 7, 6, 6, 8, 8, 12, 12, 20, 20 };

    // the ZORA string: "<version>.<payload>" (empty = every flag at its default); each field is (name, radix, version); first field least significant
    private static readonly List<(string name, int slots, int version)> zoraFields = makeZoraFields();
    private static List<(string, int, int)> makeZoraFields()
    {
        var l = new List<(string, int, int)>();
        l.Add(("randomize_magical_sword", 2, 1));
        l.Add(("randomize_letter", 2, 1));
        l.Add(("magical_sword_hearts_highest", 6, 1));
        l.Add(("progressive_items", 2, 2));
        l.Add(("shop_items_in_pool", 2, 2));
        foreach (var n in new[] { "shuffle_blue_potion", "add_l4_sword", "extra_raft_blocks", "extra_power_bracelet_blocks", "speed_up_dungeon_transitions",
                                  "speed_up_heart_fill", "recorder_kills_pols_voice", "four_potion_inventory", "auto_show_letter", "like_like_eats_rupees",
                                  "magical_boomerang_damage", "randomize_lost_hills", "randomize_dead_woods" })
            l.Add((n, 3, 3));
        l.Add(("l4_sword_in_level_2", 2, 4));
        l.Add(("level_9_entrance_sword", 2, 4));
        l.Add(("oops_all_enemies", 4, 5));
        l.Add(("oops_all_bosses", 2, 5));
        l.Add(("allow_triforces_in_stairways", 3, 6));
        l.Add(("shuffle_wood_sword", 3, 6));
        l.Add(("pegasus_boots", 3, 7));
        l.Add(("expanded_dungeon_locations", 3, 8));
        l.Add(("highlight_no_drop_rooms", 3, 9));
        l.Add(("do_not_segment_dungeons", 3, 10));
        l.Add(("disable_custom_music", 2, 11));
        l.Add(("gatekeeper_bosses", 4, 12));
        l.Add(("full_health", 3, 13));
        l.Add(("harder_bosses", 4, 14));
        l.Add(("harder_ganon", 4, 14));
        l.Add(("offense_pacing", 4, 15));
        l.Add(("defense_pacing", 4, 15));
        l.Add(("recorder_warping", 2, 15));
        l.Add(("swap_swords_and_rings", 3, 16));
        l.Add(("swordlish", 3, 16));
        l.Add(("swordlish_lowest", 8, 16));
        l.Add(("swordlish_highest", 8, 16));
        l.Add(("item_shuffle_no_vanilla", 2, 17));
        l.Add(("compass_chime", 3, 18));
        l.Add(("universal_drops_v2", 3, 19));
        l.Add(("detailed_spoiler_log", 2, 20));
        l.Add(("wizzrobes_shoot_wallmasters", 3, 21));
        l.Add(("item_counter", 3, 22));
        return l;
    }
    private const int zoraCurrentVersion = 22;   // (zora-flags.json 'current_version'; the tracker only needs these fields' radixes to read past them)

    private static BigInteger? parseBase63(string s)   // (null for None)
    {
        var n = BigInteger.Zero;
        var ok = s.Length > 0;
        foreach (var c0 in s)
        {
            var c = c0 == '_' ? '!' : c0;   // ('_' is an alias for '!': flags copied from a seed file name paste as they are)
            var i = alphabet.IndexOf(c);
            if (i < 0) ok = false;
            else n = n * new BigInteger(63) + new BigInteger(i);
        }
        return ok ? n : null;
    }

    // what the Z1R string says about the settings the tracker has (null = not known from the string)
    public sealed record Z1RPart(int? Quest,           // 0 = 1Q, 1 = 2Q, 2 = Mixed 1Q, 3 = Mixed 2Q
                                 int? ItemShuffle,     // 0 = Off, 1 = Intra, 2 = Full
                                 Tri Mirror,
                                 Tri HideNumbers,
                                 List<string> Notes);

    // (F#: Result<Z1RPart, string>; error is null when it worked)
    public static (Z1RPart ok, string error) decodeZ1R(string s)
    {
        var number = parseBase63(s.Trim());
        if (!number.HasValue)
            return (null, "That doesn't look like a Z1R flag string (it should use only letters, digits and !).");
        var n = number.Value;
        var options = new int[z1rOptionSlots.Length];
        for (int i = 0; i < z1rOptionSlots.Length; i++)
        {
            var slots = new BigInteger(z1rOptionSlots[i]);
            options[i] = (int)BigInteger.Remainder(n, slots);
            n = BigInteger.Divide(n, slots);
        }
        var toggles = new int[91];
        for (int i = 0; i <= 90; i++)
        {
            toggles[i] = (int)BigInteger.Remainder(n, new BigInteger(3));
            n = BigInteger.Divide(n, new BigInteger(3));
        }
        var notes = new List<string>();
        // (whatever is left of n is ignored, as the spec says: the tracker reads B01..B91 and the spec has B01..B94, so a current string leaves digits here)
        int? quest;
        if (options[0] <= 3) quest = options[0];
        else
        {
            notes.Add("Overworld quest is Random in these flags: pick the overworld that matches your seed.");
            quest = null;
        }
        int? shuffle;
        switch (options[5])   // C06 Item shuffle: None, Item Only, Intra-Dungeon, Full, Random
        {
            case 0: case 1: shuffle = 0; break;
            case 2: shuffle = 1; break;
            case 3: shuffle = 2; break;
            default:
                notes.Add("Item Shuffle is Random in these flags: set Off / Intra / Full to match your seed.");
                shuffle = null;
                break;
        }
        return (new Z1RPart(quest, shuffle, tri(toggles[5]), tri(toggles[23]), notes), null);
    }

    // what the ZORA string says about the ZORA extras (a three-state flag's Random can't be known from the string)
    public sealed record ZoraPart(bool Progressive,
                                  Tri FourthSword,
                                  bool FourthInLevel2,
                                  Tri BootsReplaceBoomerangs,
                                  Tri ExtraRaftBlocks,
                                  Tri ExtraPowerBlocks,
                                  Tri RandomizeDeadWoods,
                                  Tri RandomizeLostHills,
                                  string Warning = null);   // (set when the string is newer than this tracker: it was read anyway)

    // a seed file name from the ZORA page (the spec's file_name section): zora-<version>-<Z1R>-<ZORA>-<seed>[-music][.nes | .spoiler.txt | .tracker.json]
    public sealed record FileNameParts(string Version, string Z1R, string Zora, string Seed, bool Music, string Extension);

    // the ZORA release this tracker's flag tables are from (a seed from a newer one still reads, with a warning)
    public const string specZoraVersion = "2.0.1";

    private static readonly System.Text.RegularExpressions.Regex fileNameRegex = new System.Text.RegularExpressions.Regex(
        @"^zora-([.0-9A-Z_a-z-]+?)-([0-9A-Z_a-z]+)-((?:[1-9][0-9]*\.[0-9A-Z_a-z]+)?)-([0-9]+)(-music)?(\.nes|\.spoiler\.txt|\.tracker\.json)?$");

    // Reads a file name, with or without its extension, a full path, or quotes around it ("Copy as path"). Null when it is not one of the page's names.
    public static FileNameParts parseFileName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().Trim('"', '\'');
        var slash = s.LastIndexOfAny(new[] { '/', '\\' });
        if (slash >= 0) s = s.Substring(slash + 1);
        // a browser that saved the name twice added " (1)" or "(1)" before the extension
        s = System.Text.RegularExpressions.Regex.Replace(s, @" ?\([0-9]+\)(?=\.[A-Za-z.]+$|$)", "");
        // the extension may be in any case (Windows)
        foreach (var ext in new[] { ".spoiler.txt", ".tracker.json", ".nes" })
            if (s.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(0, s.Length - ext.Length) + ext; break; }
        var m = fileNameRegex.Match(s);
        if (!m.Success) return null;
        return new FileNameParts(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Success, m.Groups[6].Value);
    }

    // null when the ZORA release in a file name is not newer than the tracker's tables; otherwise a warning to show
    public static string newerReleaseWarning(string nameVersion)
    {
        var m = System.Text.RegularExpressions.Regex.Match(nameVersion ?? "", @"^[0-9]+(\.[0-9]+){1,3}");
        if (!m.Success || !Version.TryParse(m.Value, out var seen) || !Version.TryParse(specZoraVersion, out var known)) return null;
        return seen > known
            ? $"This seed was made by ZORA {m.Value}, newer than this tracker knows ({specZoraVersion}). The flags were read anyway; if something looks wrong, update the tracker."
            : null;
    }

    // (F#: Result<ZoraPart, string>; error is null when it worked)
    public static (ZoraPart ok, string error) decodeZora(string s)
    {
        s = s.Trim();
        var values = new Dictionary<string, int>();
        string warning = null;
        (ZoraPart, string) finish()
        {
            int v(string name) => values.TryGetValue(name, out var x) ? x : 0;
            return (new ZoraPart(Progressive: v("progressive_items") == 1,
                                 FourthSword: tri(v("add_l4_sword")),
                                 FourthInLevel2: v("l4_sword_in_level_2") == 1,
                                 BootsReplaceBoomerangs: tri(v("pegasus_boots")),
                                 ExtraRaftBlocks: tri(v("extra_raft_blocks")),
                                 ExtraPowerBlocks: tri(v("extra_power_bracelet_blocks")),
                                 RandomizeDeadWoods: tri(v("randomize_dead_woods")),
                                 RandomizeLostHills: tri(v("randomize_lost_hills")),
                                 Warning: warning), null);
        }
        if (s == "") return finish();
        var dot = s.IndexOf('.');
        var version = 0;
        // (digits only, no sign, spaces or leading zero: the spec's ^([1-9][0-9]*)\.)
        var versionOk = dot > 0 && s[0] != '0' && int.TryParse(s.Substring(0, dot), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out version);
        if (!versionOk || version < 1)
            return (null, "That doesn't look like a ZORA flag string (it should look like 22.abc: a version, a dot, then letters and digits).");
        // a newer string is read anyway: fields only ever get added above the old ones, so the ones this tracker knows still sit where they did
        if (version > zoraCurrentVersion)
            warning = $"These ZORA flags are version {version}, newer than this tracker understands (up to {zoraCurrentVersion}). They were read anyway; if something looks wrong, update the tracker.";
        var number = parseBase63(s.Substring(dot + 1));
        if (!number.HasValue)
            return (null, "That doesn't look like a ZORA flag string (the part after the dot should use only letters, digits and !).");
        var n = number.Value;
        foreach (var (name, slots, v) in zoraFields)
        {
            if (v <= version)
            {
                values[name] = (int)BigInteger.Remainder(n, new BigInteger(slots));
                n = BigInteger.Divide(n, new BigInteger(slots));
            }
        }
        if (!n.IsZero && version <= zoraCurrentVersion) return (null, "These ZORA flags have extra digits beyond their version, so they may have been mistyped.");
        else return finish();
    }
}
