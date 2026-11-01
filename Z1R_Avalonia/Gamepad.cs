// The gamepad's left shoulder button is push-to-talk for speech recognition. WPF reads the gamepad with SharpDX.DirectInput (Windows only);
// this reads it with SDL on every OS. As in WPF: the first gamepad found (else the first joystick) at startup is used, and a gamepad plugged
// in later isn't noticed. A recognised gamepad's left shoulder is SDL's LEFT_SHOULDER; for a plain joystick it's button 4 (zero-based), as
// WPF's `JoystickOffset.Buttons4`.
using System;
using System.Threading;

namespace Z1R_Avalonia;

public static class Gamepad
{
    private static int LeftShoulderButtonIsDown = 0;

    public static DateTime LeftShoulderButtonMostRecentRelease = DateTime.Now;   // lack of threadsafety might make it inaccurate/stale, but that's ok

    public static bool IsLeftShoulderButtonDown() =>
        Volatile.Read(ref LeftShoulderButtonIsDown) == 1;

    public static event Action<Exception> ControllerFailureEvent;

    public static unsafe bool Initialize()
    {
        SDL.SDL_Gamepad* gamepad = null;
        SDL.SDL_Joystick* joystick = null;
        if (!Platform.isBrowser && Platform.Sdl.InitSubSystem(SDL.SDL_InitFlags.SDL_INIT_GAMEPAD))
        {
            using (var gamepads = SDL.SDL3.SDL_GetGamepads())
                if (gamepads != null && gamepads.Count > 0)
                    gamepad = SDL.SDL3.SDL_OpenGamepad(gamepads[0]);
            if (gamepad == null)
                using (var joysticks = SDL.SDL3.SDL_GetJoysticks())
                    if (joysticks != null && joysticks.Count > 0)
                        joystick = SDL.SDL3.SDL_OpenJoystick(joysticks[0]);
        }
        if (gamepad == null && joystick == null)
        {
            Console.WriteLine("gamepad not found");
            return false;
        }
        else
        {
            Console.WriteLine("found gamepad");

            var gp = (IntPtr)gamepad;
            var js = (IntPtr)joystick;
            var thread = new Thread(() =>
            {
                // Poll the button (WPF polls DirectInput's buffered data in a loop)
                bool wasDown = false;
                try
                {
                    while (true)
                    {
                        bool down;
                        if (gp != IntPtr.Zero)
                        {
                            SDL.SDL3.SDL_UpdateGamepads();
                            if (!SDL.SDL3.SDL_GamepadConnected((SDL.SDL_Gamepad*)gp)) throw new Exception($"the gamepad was disconnected: {SDL.SDL3.SDL_GetError()}");
                            down = SDL.SDL3.SDL_GetGamepadButton((SDL.SDL_Gamepad*)gp, SDL.SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER);
                        }
                        else
                        {
                            SDL.SDL3.SDL_UpdateJoysticks();
                            if (!SDL.SDL3.SDL_JoystickConnected((SDL.SDL_Joystick*)js)) throw new Exception($"the joystick was disconnected: {SDL.SDL3.SDL_GetError()}");
                            down = SDL.SDL3.SDL_GetJoystickButton((SDL.SDL_Joystick*)js, 4);
                        }
                        if (down && !wasDown)
                        {
                            //printfn "left shoulder button pressed"
                            Volatile.Write(ref LeftShoulderButtonIsDown, 1);
                        }
                        if (!down && wasDown)
                        {
                            //printfn "left shoulder button released"
                            Volatile.Write(ref LeftShoulderButtonIsDown, 0);
                            LeftShoulderButtonMostRecentRelease = DateTime.Now;
                        }
                        wasDown = down;
                        Thread.Sleep(10);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to read from gamepad.  Reading the gamepad will no longer function, but the rest of the app will continue to work.");
                    Console.WriteLine("The gamepad error will be logged.");
                    ControllerFailureEvent?.Invoke(e);
                }
            });
            thread.IsBackground = true;
            thread.Name = "gamepad";
            thread.Start();
            return true;
        }
    }
}
