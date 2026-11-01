// STUB: the WPF tracker reads the gamepad with SharpDX.DirectInput (Windows only). The left shoulder button is push-to-talk
// for speech recognition. Until a cross-platform gamepad reader is added (planned: SDL via Silk.NET), no gamepad is ever found.
using System;
using System.Threading;

namespace Z1R_Avalonia;

public static class Gamepad
{
    private static int LeftShoulderButtonIsDown = 0;

    public static DateTime LeftShoulderButtonMostRecentRelease = DateTime.Now;   // lack of threadsafety might make it inaccurate/stale, but that's ok

    public static bool IsLeftShoulderButtonDown() =>
        Volatile.Read(ref LeftShoulderButtonIsDown) == 1;

#pragma warning disable CS0067   // (raised by the real gamepad reader, once there is one)
    public static event Action<Exception> ControllerFailureEvent;
#pragma warning restore CS0067

    public static bool Initialize()
    {
        Platform.stub("gamepad");
        Console.WriteLine("gamepad not found");
        return false;
    }
}
