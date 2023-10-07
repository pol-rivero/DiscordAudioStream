﻿using System;
using System.Drawing;

using Composition.WindowsRuntimeHelpers;


namespace DiscordAudioStream.ScreenCapture.CaptureStrategy
{
    public class Win10CustomAreaCapture : CustomAreaCapture
    {
        private readonly Win10Capture capture;

        public Win10CustomAreaCapture(bool captureCursor)
        {
            capture = new Win10Capture(CaptureHelper.CreateItemForMonitor(IntPtr.Zero), captureCursor);
            capture.CustomAreaCrop += () => GetCustomArea(true);
        }

        public override Bitmap CaptureFrame()
        {
            return capture.CaptureFrame();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            capture.Dispose();
        }
    }
}
