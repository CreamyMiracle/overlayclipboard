﻿using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OverlayClipboard
{
    internal class Program
    {
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        [STAThread]
        static void Main(string[] args)
        {
            var handle = Win32API.GetConsoleWindow();
            Win32API.ShowWindow(handle, SW_HIDE);

            GameOverlay.TimerService.EnableHighPrecisionTimers();
            using (var overlay = new Overlay())
            {
                overlay.Run();
            }
        }
    }
}