﻿using System.Diagnostics;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace DiscordAudioStream.ScreenCapture;

public class WindowList
{
    private const char HASH_SEPARATOR = '|';

    private record ProcessHandleItem(HWND handle, string title, string filename);
    private readonly List<ProcessHandleItem> processes;

    private WindowList(List<ProcessHandleItem> processes)
    {
        this.processes = processes;
    }

    public static WindowList Empty()
    {
        return new(new());
    }

    public static WindowList Refresh()
    {
        HWND shellWindow = PInvoke.GetShellWindow().AssertNotNull("No shell process found");
        HWND discordAudioStreamWindow = (HWND)Process.GetCurrentProcess().MainWindowHandle;
        List<ProcessHandleItem> processes = new();

        PInvoke.EnumWindows(
            // Called for each top-level window
            (hWnd, lParam) =>
            {
                if (hWnd == shellWindow || hWnd == discordAudioStreamWindow)
                {
                    return true;
                }

                // Ignore windows without WS_VISIBLE
                if (!PInvoke.IsWindowVisible(hWnd))
                {
                    return true;
                }

                // Ignore windows with "" as title
                int windowTextLength = PInvoke.GetWindowTextLength(hWnd);
                if (windowTextLength == 0)
                {
                    return true;
                }

                // Ignore suspended Windows Store apps
                if (PInvoke.DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, out BOOL cloaked).Failed)
                {
                    Logger.Log($"Cannot get property DWMWA_CLOAKED. This is normal on Windows 7.");
                }
                else if (cloaked)
                {
                    return true;
                }

                string name = PInvoke.GetWindowText(hWnd, windowTextLength + 1);
                if (name == CustomAreaForm.WINDOW_TITLE)
                {
                    return true;
                }

                PInvoke.GetWindowThreadProcessId(hWnd, out uint processId).AssertNotZero("GetWindowThreadProcessId failed");
                string filename = Process.GetProcessById((int)processId).MainModule.FileName;

                processes.Add(new(hWnd, name, filename));
                return true;
            },
            IntPtr.Zero
        ).AssertSuccess("EnumWindows failed");

        return new WindowList(processes);
    }

    public IEnumerable<string> Names => processes.Select(p => p.title);

    public HWND getHandle(int index)
    {
        return processes[index].handle;
    }

    public string getWindowHash(int index)
    {
        return processes[index].filename + HASH_SEPARATOR + processes[index].title;
    }

    public int IndexOfHandle(HWND handle)
    {
        return processes.FindIndex(p => p.handle == handle);
    }

    public int IndexOfWindowHash(string hash)
    {
        string[] hashParts = hash.Split(HASH_SEPARATOR);
        if (hashParts.Length != 2)
        {
            throw new ArgumentException("Invalid hash");
        }
        string filename = hashParts[0];
        string title = hashParts[1];

        int exactMatch = processes.FindIndex(p => p.filename == filename && p.title == title);
        if (exactMatch != -1)
        {
            return exactMatch;
        }

        int filenameMatch = processes.FindIndex(p => p.filename == filename);
        if (filenameMatch != -1)
        {
            return filenameMatch;
        }

        int titleMatch = processes.FindIndex(p => p.title == title);
        if (titleMatch != -1)
        {
            return titleMatch;
        }

        throw new InvalidOperationException("No window matches hash");
    }
}
