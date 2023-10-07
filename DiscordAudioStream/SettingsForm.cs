﻿using System;
using System.Drawing;
using System.Windows.Forms;

using CustomComponents;

using DiscordAudioStream.ScreenCapture;

namespace DiscordAudioStream
{
    internal partial class SettingsForm : Form
    {
        public event Action CaptureMethodChanged;
        public event Action FramerateChanged;
        public event Action ShowAudioInputsChanged;

        private enum FrameRates
        {
            FPS_15 = 15,
            FPS_30 = 30,
            FPS_60 = 60
        }
        private readonly CaptureState captureState;

        public SettingsForm(bool darkMode, CaptureState captureState)
        {
            Logger.EmptyLine();
            Logger.Log("Initializing SettingsForm. darkMode=" + darkMode);

            // Store capture state in order to change ScreenMethod or WindowMethod
            this.captureState = captureState;

            // Enable dark titlebar
            if (darkMode) HandleCreated += new EventHandler(DarkThemeManager.FormHandleCreated);

            InitializeComponent();

            ApplyDarkTheme(darkMode);

            // Set default values

            themeComboBox.SelectedIndex = Properties.Settings.Default.Theme;
            autoExitCheckbox.Checked = Properties.Settings.Default.AutoExit;
            outputLogCheckbox.Checked = Properties.Settings.Default.OutputLogFile;
            offscreenDrawCheckbox.Checked = Properties.Settings.Default.OffscreenDraw;
            showAudioInputsCheckbox.Checked = Properties.Settings.Default.ShowAudioInputs;
            streamTitleBox.Text = Properties.Settings.Default.StreamTitle;
            audioMeterCheckBox.Checked = Properties.Settings.Default.ShowAudioMeter;

            windowMethodComboBox.SelectedIndex = (int)captureState.WindowMethod;
            fullscreenMethodComboBox.SelectedIndex = (int)captureState.ScreenMethod;

            FrameRates selectedFramerate = (FrameRates)Properties.Settings.Default.CaptureFramerate;
            Array allFramerates = Enum.GetValues(typeof(FrameRates));
            captureFramerateComboBox.SelectedIndex = Array.IndexOf(allFramerates, selectedFramerate);

            // Set tooltips
            toolTip.SetToolTip(autoExitCheckbox, Properties.Resources.Tooltip_AutoExit);
            toolTip.SetToolTip(captureFramerateLabel, Properties.Resources.Tooltip_CaptureFramerate);
            toolTip.SetToolTip(captureFramerateComboBox, Properties.Resources.Tooltip_CaptureFramerate);
            toolTip.SetToolTip(fullscreenMethodLabel, Properties.Resources.Tooltip_FullscreenMethod);
            toolTip.SetToolTip(fullscreenMethodComboBox, Properties.Resources.Tooltip_FullscreenMethod);
            toolTip.SetToolTip(windowMethodLabel, Properties.Resources.Tooltip_WindowMethod);
            toolTip.SetToolTip(windowMethodComboBox, Properties.Resources.Tooltip_WindowMethod);
            toolTip.SetToolTip(outputLogCheckbox, Properties.Resources.Tooltip_OutputLog);
            toolTip.SetToolTip(offscreenDrawCheckbox, Properties.Resources.Tooltip_OffscreenDraw);
            toolTip.SetToolTip(showAudioInputsCheckbox, Properties.Resources.Tooltip_ShowAudioInputs);
            toolTip.SetToolTip(themeLabel, Properties.Resources.Tooltip_WindowTheme);
            toolTip.SetToolTip(themeComboBox, Properties.Resources.Tooltip_WindowTheme);
            toolTip.SetToolTip(streamTitleLabel, Properties.Resources.Tooltip_StreamTitle);
            toolTip.SetToolTip(streamTitleBox, Properties.Resources.Tooltip_StreamTitle);
            toolTip.SetToolTip(audioMeterCheckBox, Properties.Resources.Tooltip_ShowAudioMeter);
        }

        private void ApplyDarkTheme(bool darkMode)
        {
            if (darkMode)
            {
                BackColor = DarkThemeManager.DarkBackColor;
                ForeColor = Color.White;

                settingsTabs.BackTabColor = DarkThemeManager.DarkBackColor;
                settingsTabs.BorderColor = DarkThemeManager.DarkSecondColor;
                settingsTabs.HeaderColor = DarkThemeManager.DarkSecondColor;
                settingsTabs.TextColor = Color.White;
            }

            settingsTabs.ActiveColor = DarkThemeManager.AccentColor;

            captureMethodGroup.SetDarkMode(darkMode);
            windowMethodComboBox.SetDarkMode(darkMode);
            fullscreenMethodComboBox.SetDarkMode(darkMode);
            captureFramerateComboBox.SetDarkMode(darkMode);
            themeComboBox.SetDarkMode(darkMode);
            streamTitleBox.SetDarkMode(darkMode);
            autoExitCheckbox.SetDarkMode(darkMode);
            outputLogCheckbox.SetDarkMode(darkMode);
            offscreenDrawCheckbox.SetDarkMode(darkMode);
            showAudioInputsCheckbox.SetDarkMode(darkMode);
            audioMeterCheckBox.SetDarkMode(darkMode);

            classicVolumeMixerLink.LinkColor = DarkThemeManager.AccentColor;
            audioDevicesLink.LinkColor = DarkThemeManager.AccentColor;
        }


        // Events


        private void SettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        private void themeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            int theme = themeComboBox.SelectedIndex;
            // Nothing changed
            if (Properties.Settings.Default.Theme == theme) return;

            Properties.Settings.Default.Theme = theme;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log($"Change settings: Theme={Properties.Settings.Default.Theme}. Restarting...");

            Application.Restart();
            Environment.Exit(0);
        }


        private void classicVolumeMixerLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string cplPath = System.IO.Path.Combine(Environment.SystemDirectory, "sndvol.exe");
            System.Diagnostics.Process.Start(cplPath);
        }

        private void audioDevicesLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string cplPath = System.IO.Path.Combine(Environment.SystemDirectory, "control.exe");
            System.Diagnostics.Process.Start(cplPath, "/name Microsoft.Sound");
        }

        private void autoExitCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.AutoExit == autoExitCheckbox.Checked) return;

            Properties.Settings.Default.AutoExit = autoExitCheckbox.Checked;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: AutoExit=" + Properties.Settings.Default.AutoExit);
        }

        private void outputLogCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.OutputLogFile == outputLogCheckbox.Checked) return;

            Properties.Settings.Default.OutputLogFile = outputLogCheckbox.Checked;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: OutputLogFile=" + Properties.Settings.Default.OutputLogFile);
        }

        private void fullscreenMethodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            captureState.ScreenMethod = (CaptureState.ScreenCaptureMethod)fullscreenMethodComboBox.SelectedIndex;
            CaptureMethodChanged?.Invoke();
        }

        private void windowMethodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            captureState.WindowMethod = (CaptureState.WindowCaptureMethod)windowMethodComboBox.SelectedIndex;
            CaptureMethodChanged?.Invoke();
        }

        private void captureFramerateComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Array allFramerates = Enum.GetValues(typeof(FrameRates));
            FrameRates selectedFramerate = (FrameRates)allFramerates.GetValue(captureFramerateComboBox.SelectedIndex);

            // Nothing changed
            if (Properties.Settings.Default.CaptureFramerate == (int)selectedFramerate) return;

            Properties.Settings.Default.CaptureFramerate = (int)selectedFramerate;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: CaptureFramerate=" + selectedFramerate);

            FramerateChanged?.Invoke();
        }

        private void offscreenDrawCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.OffscreenDraw == offscreenDrawCheckbox.Checked) return;

            Properties.Settings.Default.OffscreenDraw = offscreenDrawCheckbox.Checked;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: OffscreenDraw=" + Properties.Settings.Default.OffscreenDraw);
        }

        private void SettingsForm_HelpButtonClicked(object sender, System.ComponentModel.CancelEventArgs e)
        {
            System.Diagnostics.Process.Start(Properties.Resources.URL_CaptureMethodsInfoLink);
        }

        private void showAudioInputsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.ShowAudioInputs == showAudioInputsCheckbox.Checked) return;

            Properties.Settings.Default.ShowAudioInputs = showAudioInputsCheckbox.Checked;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: ShowAudioInputs=" + Properties.Settings.Default.ShowAudioInputs);

            ShowAudioInputsChanged?.Invoke();
        }

        private void streamTitleBox_TextChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.StreamTitle == streamTitleBox.Text) return;
            try
            {
                Properties.Settings.Default.StreamTitle = streamTitleBox.Text;
                Properties.Settings.Default.Save();
                // Text could contain sensitive information, don't log it
                Logger.Log("Stream title saved successfully");
            }
            catch (ArgumentException ex)
            {
                Logger.Log("Failed to save stream title: " + ex.Message);
                // Saving could fail when auto-filling a character with many code points, like some emojis
                // Once the character has been completely filled, saving should succeed
            }
        }

        private void audioMeterCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // Nothing changed
            if (Properties.Settings.Default.ShowAudioMeter == audioMeterCheckBox.Checked) return;

            Properties.Settings.Default.ShowAudioMeter = audioMeterCheckBox.Checked;
            Properties.Settings.Default.Save();
            Logger.EmptyLine();
            Logger.Log("Change settings: ShowAudioMeter=" + Properties.Settings.Default.ShowAudioMeter);
        }
    }
}
