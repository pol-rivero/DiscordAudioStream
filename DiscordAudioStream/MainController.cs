﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using DiscordAudioStream.AudioCapture;
using DiscordAudioStream.ScreenCapture;

using DLLs;

namespace DiscordAudioStream
{
    public class MainController
    {
        private readonly MainForm form;
        private bool forceRefresh = false;
        private int numberOfScreens = -1;
        private Size lastCapturedFrameSize = new Size(0, 0);

        private ScreenCaptureManager screenCapture;
        private ProcessHandleList processHandleList;
        private readonly CaptureState captureState = new CaptureState();
        private readonly CaptureResizer captureResizer = new CaptureResizer();

        private AudioPlayback audioPlayback = null;
        private AudioMeterForm currentMeterForm = null;

        internal Action OnAudioMeterClosed { get; set; }

        public MainController(MainForm owner)
        {
            form = owner;
            processHandleList = ProcessHandleList.Refresh();
        }

        public bool IsStreaming { get; private set; } = false;

        internal void Init()
        {
            RefreshAreaInfo();

            captureState.HideTaskbar = Properties.Settings.Default.HideTaskbar;
            captureState.CapturingCursor = Properties.Settings.Default.CaptureCursor;

            screenCapture = new ScreenCaptureManager(captureState);
            screenCapture.CaptureAborted += AbortCapture;

            Thread drawThread = CreateDrawThread();
            drawThread.Start();
        }

        // Called when the X button is pressed
        internal bool Stop()
        {
            bool cancel = false;
            if (IsStreaming)
            {
                cancel = true; // Do not close form, return to settings instead
                EndStream();
            }
            else
            {
                Logger.EmptyLine();
                Logger.Log("Close button pressed, stopping program.");
                screenCapture?.Stop();
                User32.UnregisterHotKey(form.Handle, 0);
            }
            return cancel;
        }

        // PRIVATE METHODS

        private void SetPreviewSize(Size size)
        {
            lastCapturedFrameSize = size;
            if (IsStreaming)
            {
                size = captureResizer.GetScaledSize(size);
                form.SetPreviewUISize(size);
            }
        }

        private void RefreshAreaInfo()
        {
            if (!form.Created)
            {
                return;
            }

            int windowIndex = form.VideoIndex - numberOfScreens - 1;

            if (windowIndex == -1)
            {
                // Index right before first Window: Custom area
                captureState.Target = CaptureState.CaptureTarget.CustomArea;
            }
            else if (windowIndex >= 0)
            {
                // Window
                captureState.WindowHandle = processHandleList[windowIndex];
            }
            else
            {
                // Screen
                if (Screen.AllScreens.Length > 1 && form.VideoIndex == numberOfScreens - 1)
                {
                    // All screens
                    captureState.Target = CaptureState.CaptureTarget.AllScreens;
                }
                else
                {
                    // Single screen
                    captureState.Screen = Screen.AllScreens[form.VideoIndex];
                }
            }

            form.HideTaskbarEnabled = captureState.HideTaskbarSupported;
            if (captureState.HideTaskbarSupported)
            {
                // The selected method allows hiding taskbar, see if checkbox is checked
                captureState.HideTaskbar = form.HideTaskbar;
            }
        }

        private Thread CreateDrawThread()
        {
            // Get the handle now, since we cannot get it from inside the thread
            IntPtr formHandle = form.Handle;

            return new Thread(() =>
            {
                int fps = Properties.Settings.Default.CaptureFramerate;
                Logger.EmptyLine();
                Logger.Log($"Creating Draw thread. Target framerate: {fps} FPS ({screenCapture.CaptureIntervalMs} ms)");

                Stopwatch stopwatch = new Stopwatch();
                Size oldSize = new Size(0, 0);

                while (true)
                {
                    stopwatch.Restart();
                    try
                    {
                        Bitmap next = ScreenCaptureManager.GetNextFrame();

                        // No new data, keep displaying last frame
                        if (next == null)
                        {
                            continue;
                        }

                        // Detect size changes
                        if (next.Size != oldSize)
                        {
                            oldSize = next.Size;
                            SetPreviewSize(next.Size);
                        }

                        // Display captured frame
                        // Refresh if the stream has started and "Force screen redraw" is enabled
                        form.UpdatePreview(next, IsStreaming && forceRefresh, formHandle);
                    }
                    catch (InvalidOperationException)
                    {
                        // Form is closing
                        Logger.Log("Form is closing, stop Draw thread.");
                        return;
                    }
                    stopwatch.Stop();

                    int wait = screenCapture.CaptureIntervalMs - (int)stopwatch.ElapsedMilliseconds;
                    if (wait > 0)
                    {
                        Thread.Sleep(wait);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "Draw Thread"
            };
        }

        // INTERNAL METHODS (called from MainForm)

        internal void UpdateAreaComboBox(int oldIndex)
        {
            IntPtr oldHandle = IntPtr.Zero;
            if (oldIndex > numberOfScreens)
            {
                // We were capturing a window, store its handle
                int windowIndex = oldIndex - numberOfScreens - 1;
                oldHandle = processHandleList[windowIndex];
            }

            // Refresh list of windows
            RefreshScreens();

            if (oldIndex > numberOfScreens)
            {
                // We were capturing a window, see if it still exists
                int windowIndex = processHandleList.IndexOf(oldHandle);
                if (windowIndex == -1)
                {
                    // Window has been closed, return to last saved screen
                    form.VideoIndex = Properties.Settings.Default.AreaIndex;
                }
                else
                {
                    // Window still exists
                    form.VideoIndex = windowIndex + numberOfScreens + 1;
                }
            }
            else
            {
                // We were capturing a screen
                form.VideoIndex = oldIndex;
            }
        }

        internal void RefreshScreens()
        {
            List<string> screens = Screen.AllScreens
                .Select((screen, i) =>
                {
                    Rectangle bounds = screen.Bounds;
                    string screenName = screen.Primary ? "Primary screen" : $"Screen {i + 1}";
                    return $"{screenName} ({bounds.Width} x {bounds.Height})";
                })
                .ToList();

            if (Screen.AllScreens.Length > 1)
            {
                screens.Add("Everything");
            }
            numberOfScreens = screens.Count;

            processHandleList = ProcessHandleList.Refresh();
            IEnumerable<(string, bool)> elements = screens
                .Select(screenName => (screenName, false))
                .Append(("Custom area", true))
                .Concat(processHandleList.Names.Select(windowName => (windowName, false)));

            form.SetVideoItems(elements);
        }

        internal void RefreshAudioDevices()
        {
            IEnumerable<string> elements = new string[] { "(None)" }
                .Concat(AudioPlayback.RefreshDevices());

            int defaultIndex = AudioPlayback.GetLastDeviceIndex() + 1; // Add 1 for "None" element
            form.SetAudioElements(elements, defaultIndex);
        }

        internal void StartStream(bool skipAudioWarning)
        {
            try
            {
                if (form.HasSomeAudioSource)
                {
                    StartStreamAudioRecording(form.AudioSourceIndex, skipAudioWarning);
                }
                else
                {
                    StartStreamWithoutAudio(skipAudioWarning);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            form.EnableStreamingUI(true);
            // Reading Properties.Settings can be slow, set flag once at the start of the stream
            forceRefresh = Properties.Settings.Default.OffscreenDraw;
            Logger.Log("Force screen redraw: " + forceRefresh);
            IsStreaming = true;

            SetPreviewSize(lastCapturedFrameSize);
        }

        private void StartStreamWithoutAudio(bool skipAudioWarning)
        {
            if (!skipAudioWarning)
            {
                DialogResult r = MessageBox.Show(
                    "No audio source selected, continue anyways?",
                    "Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    // The second button ("No") is the default option
                    MessageBoxDefaultButton.Button2
                );

                if (r == DialogResult.No)
                {
                    throw new OperationCanceledException();
                }
            }

            Logger.EmptyLine();
            Logger.Log("START STREAM (Without audio)");
            // Clear the stored last used audio device
            Properties.Settings.Default.AudioDeviceID = "";
            Properties.Settings.Default.Save();
        }

        private void StartStreamAudioRecording(int deviceIndex, bool skipAudioWarning)
        {
            if (deviceIndex == AudioPlayback.GetDefaultDeviceIndex())
            {
                if (!skipAudioWarning)
                {
                    DialogResult r = MessageBox.Show(
                        "The captured audio device is the same as the output device of DiscordAudioStream.\n"
                            + "This will cause an audio loop, which may result in echo or very loud sounds. Continue anyways?",
                        "Warning",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Warning,
                        // The second button ("Cancel") is the default option
                        MessageBoxDefaultButton.Button2
                    );

                    if (r == DialogResult.Cancel)
                    {
                        throw new OperationCanceledException();
                    }
                }

                Logger.EmptyLine();
                Logger.Log("DEFAULT DEVICE CAPTURED (Audio loop)");
            }

            Logger.EmptyLine();
            Logger.Log("START STREAM (With audio)");

            audioPlayback = new AudioPlayback(deviceIndex);
            audioPlayback.AudioLevelChanged += (left, right) => currentMeterForm?.SetLevels(left, right);
            try
            {
                audioPlayback.Start();
            }
            catch (InvalidOperationException e)
            {
                MessageBox.Show(
                    e.Message,
                    "Unable to capture the audio device",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1
                );
                throw new OperationCanceledException();
            }
        }

        private void EndStream()
        {
            Logger.EmptyLine();
            Logger.Log("END STREAM");
            form.EnableStreamingUI(false);
            IsStreaming = false;
            audioPlayback?.Stop();
        }

        private void AbortCapture()
        {
            form.Invoke(new Action(() =>
            {
                RefreshScreens();
                form.VideoIndex = Properties.Settings.Default.AreaIndex;
                if (!IsStreaming)
                {
                    return;
                }

                EndStream();
                if (Properties.Settings.Default.AutoExit)
                {
                    Logger.EmptyLine();
                    Logger.Log("AutoExit was enabled, closing form.");
                    form.Close();
                }
            }));
        }

        internal void ShowSettingsForm(bool darkMode)
        {
            SettingsForm settingsBox = new SettingsForm(darkMode, captureState) { Owner = form, TopMost = form.TopMost };
            settingsBox.CaptureMethodChanged += RefreshAreaInfo;
            settingsBox.FramerateChanged += screenCapture.RefreshFramerate;
            settingsBox.ShowAudioInputsChanged += RefreshAudioDevices;
            settingsBox.ShowDialog();
        }

        internal void ShowAboutForm(bool darkMode)
        {
            AboutForm aboutBox = new AboutForm(darkMode) { Owner = form, TopMost = form.TopMost };
            aboutBox.ShowDialog();
        }

        internal void ShowAudioMeterForm(bool darkMode)
        {
            // Disabled by the user
            if (!Properties.Settings.Default.ShowAudioMeter)
            {
                return;
            }
            // No audio to display
            if (!form.HasSomeAudioSource)
            {
                return;
            }

            if (currentMeterForm == null)
            {
                currentMeterForm = new AudioMeterForm(darkMode) { Owner = form };
                currentMeterForm.FormClosed += (sender, e) =>
                {
                    currentMeterForm = null;
                    OnAudioMeterClosed?.Invoke();
                };
            }
            currentMeterForm.TopMost = form.TopMost;
            currentMeterForm.Show();
            form.Focus();
        }

        internal void HideAudioMeterForm()
        {
            currentMeterForm?.Hide();
        }

        internal void SetVideoIndex(int index)
        {
            if (numberOfScreens == -1)
            {
                return;
            }

            RefreshAreaInfo();

            // Do not save settings for Windows (only screen or Custom area)
            if (index <= numberOfScreens)
            {
                Properties.Settings.Default.AreaIndex = index;
                Properties.Settings.Default.Save();
            }
        }

        internal void SetScaleIndex(int index)
        {
            captureResizer.SetScaleMode((ScaleMode)index);

            Properties.Settings.Default.ScaleIndex = index;
            Properties.Settings.Default.Save();
        }

        internal void SetHideTaskbar(bool hideTaskbar)
        {
            captureState.HideTaskbar = hideTaskbar;
            Properties.Settings.Default.HideTaskbar = hideTaskbar;
            Properties.Settings.Default.Save();
        }

        internal void SetCapturingCursor(bool capturing)
        {
            captureState.CapturingCursor = capturing;
            Properties.Settings.Default.CaptureCursor = capturing;
            Properties.Settings.Default.Save();
        }

        internal void MoveWindow(Point newPosition)
        {
            form.Location = newPosition;
        }
    }
}
