using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Z1R_Sync
{
    class ManageContainer
    {
        private static Process _syncHostProcess;

        public static void StartLocalSyncHost()
        {
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Z1R_SignalRHost.exe");

            if (!File.Exists(exePath))
            {
                Console.WriteLine($"[Sync] ERROR: {exePath} not found.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _syncHostProcess = Process.Start(startInfo);
            Console.WriteLine("[Sync] Local SignalR Host started.");
        }
        public static void StopLocalSyncHost()
        {
            try
            {
                if (_syncHostProcess != null && !_syncHostProcess.HasExited)
                {
                    _syncHostProcess.Kill();
                    Console.WriteLine("[Sync] Local SignalR Host stopped.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] Failed to stop SignalR Host: {ex.Message}");
            }
        }


    }
}
