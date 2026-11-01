// Ported from Z1R_WPF/SpeechRecognition.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// The recognizer (WPF: a System.Speech.Recognition.SpeechRecognitionEngine) is a Platform.SpeechRecognitionEngine: System.Speech on Windows,
// a stub elsewhere (see Stubs in PROGRESS.md).
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Z1R_Avalonia;

public static class SpeechRecognition
{
    public static readonly Platform.SpeechRecognitionEngine speechRecognizer = new Platform.SpeechRecognitionEngine();

    public class SpeechRecognitionInstance
    {
        private readonly TrackerModel.DungeonTrackerInstanceKind kind;
        private readonly string wakePhrase = "tracker set";
        private readonly Dictionary<string, int> mapStatePhrases;
        private static Dictionary<string, int> make_mapStatePhrases(TrackerModel.DungeonTrackerInstanceKind kind)
        {
            var coda = new (string, int)[] {
                ("level nine"        , 8),
                ("any road"          , 12),  // 9 10 11 12
                ("sword three"       , 13),
                ("sword two"         , 14),
                ("sword one"         , 15),
                ("arrow shop"        , 16),
                ("bomb shop"         , 17),
                ("book shop"         , 18),
                ("candle shop"       , 19),
                ("blue ring shop"    , 20),
                ("meat shop"         , 21),
                ("key shop"          , 22),
                ("shield shop"       , 23),
                ("unknown secret"    , 24),
                ("large secret"      , 25),
                ("medium secret"     , 26),
                ("small secret"      , 27),
                ("door repair"       , 28),
                ("money making game" , 29),
                ("the letter"        , 30),
                ("arm owes" /*armos*/, 31),
                ("hint shop"         , 32),
                ("take any"          , 33),
                ("potion shop"       , 34),
                ("don't care"        , 35),
                ("nothing"           , 35),
                };
            var d = new Dictionary<string, int>();
            switch (kind.Tag)
            {
                case TrackerModel.DungeonTrackerInstanceKind.Tags.HIDE_DUNGEON_NUMBERS:
                    d.Add("level"             , 0);   // 0 1 2 3 4 5 6 7
                    break;
                case TrackerModel.DungeonTrackerInstanceKind.Tags.DEFAULT:
                    d.Add("level one"         , 0);
                    d.Add("level two"         , 1);
                    d.Add("level three"       , 2);
                    d.Add("level four"        , 3);
                    d.Add("level five"        , 4);
                    d.Add("level six"         , 5);
                    d.Add("level seven"       , 6);
                    d.Add("level eight"       , 7);
                    break;
                default: throw new InvalidOperationException();
            }
            foreach (var (phrase, state) in coda)
                d.Add(phrase, state);
            return d;
        }
        public SpeechRecognitionInstance(TrackerModel.DungeonTrackerInstanceKind kind)
        {
            this.kind = kind;
            mapStatePhrases = make_mapStatePhrases(kind);
            speechRecognizer.LoadGrammar(wakePhrase, mapStatePhrases.Keys.ToArray());
        }
        /// <summary>WPF returns `int option`; null here for None.</summary>
        public int? ConvertSpokenPhraseToMapCell(string phrase)
        {
            phrase = phrase.Substring(wakePhrase.Length + 1);
            var newState = mapStatePhrases[phrase];
            if (newState == 12) // any road
            {
                if      (TrackerModel.mapSquareChoiceDomain.CanAddUse( 9)) return 9;
                else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(10)) return 10;
                else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(11)) return 11;
                else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(12)) return 12;
                else return null;
            }
            else
            {
                if (kind.Equals(TrackerModel.DungeonTrackerInstanceKind.HIDE_DUNGEON_NUMBERS) && newState == 0)
                {
                    if      (TrackerModel.mapSquareChoiceDomain.CanAddUse(0)) return 0;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(1)) return 1;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(2)) return 2;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(3)) return 3;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(4)) return 4;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(5)) return 5;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(6)) return 6;
                    else if (TrackerModel.mapSquareChoiceDomain.CanAddUse(7)) return 7;
                    else return null;
                }
                else
                {
                    if (TrackerModel.mapSquareChoiceDomain.CanAddUse(newState))
                        return newState;
                    else
                        return null;
                }
            }
        }
        public void AttachSpeechRecognizedToApp(Canvas appMainCanvas, Action<string> whenRecognizedFunc)
        {
            speechRecognizer.SpeechRecognized += r =>
            {
                if (TrackerModelOptions.ListenForSpeech.Value)
                {
                    if (!(TrackerModelOptions.RequirePTTForSpeech.Value) ||                                              // if PTT not required, or
                            Gamepad.IsLeftShoulderButtonDown() ||                                                          // if PTT is currently held, or
                            (DateTime.Now - Gamepad.LeftShoulderButtonMostRecentRelease) < TimeSpan.FromSeconds(1.0))      // if PTT was just recently released
                    {
                        //printfn "conf: %3.3f  %s" r.Result.Confidence r.Result.Text                                         // then we want to use speech
                        var threshold =
                            TrackerModelOptions.RequirePTTForSpeech.Value
                                ? 0.90f   // empirical tests suggest this confidence threshold is good enough to avoid false positives with PTT
                                : 0.94f;  // empirical tests suggest this confidence threshold is good to avoid false positives
                        if (r.Confidence > threshold)
                            Dispatcher.UIThread.Invoke(() => whenRecognizedFunc(r.Text));
                    }
                }
            };
        }
    }
}
