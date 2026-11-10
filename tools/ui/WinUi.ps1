# Windows UI input/screenshot helper for isolated game verification.
# Uses SendInput (real keyboard/mouse events) and screen capture. No game memory access.

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class WinUiNative
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

    public static IntPtr FindGameWindow(uint targetPid, string titlePart)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            if (pid != targetPid || !IsWindowVisible(h)) return true;
            int len = GetWindowTextLength(h);
            if (len > 0)
            {
                var sb = new System.Text.StringBuilder(len + 2);
                GetWindowText(h, sb, sb.Capacity);
                if (sb.ToString().IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0) { found = h; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT { public int dx, dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_SCANCODE = 0x0008;
    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public static void Key(ushort vk, bool up)
    {
        var scan = (ushort)MapVirtualKey(vk, 0);
        var input = new INPUT { type = INPUT_KEYBOARD };
        input.u.ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = up ? KEYEVENTF_KEYUP : 0 };
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void KeyScan(ushort scan, bool up, bool extended)
    {
        var input = new INPUT { type = INPUT_KEYBOARD };
        input.u.ki = new KEYBDINPUT { wVk = 0, wScan = scan, dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0) };
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void Unicode(char ch, bool up)
    {
        var input = new INPUT { type = INPUT_KEYBOARD };
        input.u.ki = new KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0) };
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void MouseMove(int x, int y)
    {
        var input = new INPUT { type = INPUT_MOUSE };
        input.u.mi = new MOUSEINPUT { dx = x, dy = y, dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE };
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    public static void MouseButton(bool right, bool up)
    {
        var input = new INPUT { type = INPUT_MOUSE };
        input.u.mi = new MOUSEINPUT { dx = 0, dy = 0, dwFlags = right ? (up ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN) : (up ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN) };
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[void][WinUiNative]::SetProcessDPIAware()

function Get-CasualtiesWindow {
    param([int]$ProcessId)
    return [WinUiNative]::FindGameWindow([uint32]$ProcessId, 'Casualties')
}

function Focus-Window {
    param([IntPtr]$Handle)
    [void][WinUiNative]::ShowWindow($Handle, 9) # SW_RESTORE
    [void][WinUiNative]::BringWindowToTop($Handle)
    [void][WinUiNative]::SetForegroundWindow($Handle)
    Start-Sleep -Milliseconds 250
}

function Get-WindowClientRect {
    param([IntPtr]$Handle)
    $r = New-Object WinUiNative+RECT
    [void][WinUiNative]::GetClientRect($Handle, [ref]$r)
    $p = New-Object WinUiNative+POINT
    [void][WinUiNative]::ClientToScreen($Handle, [ref]$p)
    [pscustomobject]@{ X = $p.X; Y = $p.Y; Width = $r.Right - $r.Left; Height = $r.Bottom - $r.Top }
}

function Send-KeyDown { param([int]$Vk) [WinUiNative]::Key([uint16]$Vk, $false) }
function Send-KeyUp { param([int]$Vk) [WinUiNative]::Key([uint16]$Vk, $true) }

function Send-KeyPress {
    param([int]$Vk, [int]$HoldMs = 60)
    Send-KeyDown $Vk
    Start-Sleep -Milliseconds $HoldMs
    Send-KeyUp $Vk
}

function Send-KeyHold {
    param([int]$Vk, [int]$HoldMs)
    Send-KeyDown $Vk
    Start-Sleep -Milliseconds $HoldMs
    Send-KeyUp $Vk
}

function Send-TextRaw {
    param([string]$Text)
    foreach ($ch in $Text.ToCharArray()) {
        [WinUiNative]::Unicode($ch, $false)
        Start-Sleep -Milliseconds 15
        [WinUiNative]::Unicode($ch, $true)
        Start-Sleep -Milliseconds 15
    }
}

function Get-CharVk {
    param([char]$Ch)
    if ($Ch -ge 'a' -and $Ch -le 'z') { return [int][char]::ToUpperInvariant($Ch) }
    if ($Ch -ge 'A' -and $Ch -le 'Z') { return [int]$Ch }
    if ($Ch -ge '0' -and $Ch -le '9') { return [int]$Ch }
    switch ($Ch) {
        ' ' { return 0x20 }
        ',' { return 0xBC }
        '.' { return 0xBE }
        '/' { return 0xBF }
        '-' { return 0xBD }
        '=' { return 0xBB }
        ';' { return 0xBA }
        "'" { return 0xDE }
        _ { return 0 }
    }
    return 0
}

function Send-TypeKeys {
    param([string]$Text)
    foreach ($ch in $Text.ToCharArray()) {
        $vk = Get-CharVk $ch
        if ($vk -le 0) { continue }
        Send-KeyDown $vk
        Start-Sleep -Milliseconds 18
        Send-KeyUp $vk
        Start-Sleep -Milliseconds 32
    }
}

function Move-MouseClient {
    param([IntPtr]$Handle, [int]$X, [int]$Y)
    $r = Get-WindowClientRect -Handle $Handle
    # Absolute mouse coordinates are normalized to the virtual desktop.
    $screenW = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
    $screenH = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
    $sx = [int](($r.X + $X) * 65535 / [Math]::Max(1, $screenW - 1))
    $sy = [int](($r.Y + $Y) * 65535 / [Math]::Max(1, $screenH - 1))
    [WinUiNative]::MouseMove($sx, $sy)
}

function Click-MouseClient {
    param([IntPtr]$Handle, [int]$X, [int]$Y, [ValidateSet('Left','Right')][string]$Button = 'Left')
    Move-MouseClient -Handle $Handle -X $X -Y $Y
    Start-Sleep -Milliseconds 120
    [WinUiNative]::MouseButton(($Button -eq 'Right'), $false)
    Start-Sleep -Milliseconds 80
    [WinUiNative]::MouseButton(($Button -eq 'Right'), $true)
    Start-Sleep -Milliseconds 120
}

function Save-WindowScreenshot {
    param([IntPtr]$Handle, [string]$Path)
    $r = Get-WindowClientRect -Handle $Handle
    $bmp = New-Object System.Drawing.Bitmap $r.Width, $r.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CopyFromScreen($r.X, $r.Y, 0, 0, $bmp.Size, [System.Drawing.CopyPixelOperation]::SourceCopy)
    } finally {
        $g.Dispose()
    }
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
