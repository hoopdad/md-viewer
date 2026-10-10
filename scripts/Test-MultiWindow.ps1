[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Executable,

    [switch] $UseFileAssociation
)

$ErrorActionPreference = "Stop"
$Executable = (Resolve-Path -LiteralPath $Executable).Path
if ($PSVersionTable.PSEdition -eq "Core") {
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath, "-Executable", $Executable)
    if ($UseFileAssociation) { $arguments += "-UseFileAssociation" }
    & powershell.exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "Multi-window regression test failed." }
    return
}

Add-Type @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

public static class ViewerTestWindows
{
    public sealed class Window
    {
        public IntPtr Handle { get; set; }
        public string Name { get; set; }
    }

    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int maxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static Window[] ForProcess(int processId)
    {
        var windows = new List<Window>();
        EnumWindows((window, parameter) =>
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner == processId && IsWindowVisible(window))
            {
                var name = new StringBuilder(256);
                GetClassName(window, name, name.Capacity);
                if (name.ToString().StartsWith("HwndWrapper[", StringComparison.Ordinal))
                {
                    var title = new StringBuilder(1024);
                    GetWindowText(window, title, title.Capacity);
                    windows.Add(new Window { Handle = window, Name = title.ToString() });
                }
            }
            return true;
        }, IntPtr.Zero);
        return windows.ToArray();
    }

    public static void Close(IntPtr window)
    {
        if (!PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}
'@

function Get-ViewerProcess {
    @(Get-Process | Where-Object { $_.ProcessName -eq "md-viewer" })
}

function Open-TestFile([string] $Path) {
    if ($UseFileAssociation) {
        $launcher = Start-Process -FilePath $env:ComSpec `
            -ArgumentList "/d /c start `"`" `"$Path`"" -PassThru
        if (!$launcher.WaitForExit(10000)) { throw "Shell launch timed out for $Path." }
        if ($launcher.ExitCode -ne 0) { throw "Shell launch failed for $Path." }
    } else {
        Start-Process -FilePath $Executable -ArgumentList "`"$Path`"" | Out-Null
    }
}

function Get-ViewerWindows {
    @([ViewerTestWindows]::ForProcess($script:viewer.Id))
}

function Wait-Documents([string[]] $Titles) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $windows = @(Get-ViewerWindows)
        $actual = @($windows | ForEach-Object { $_.Name } | Sort-Object)
        $expected = @($Titles | ForEach-Object { "$_ - md-viewer" } | Sort-Object)
        if (($actual -join "`n") -eq ($expected -join "`n")) {
            return
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    Write-Host "Tracked viewer PID: $($script:viewer.Id); exited: $($script:viewer.HasExited)"
    Get-ViewerProcess | Select-Object Id, Path, MainWindowTitle | Format-List | Out-Host
    throw "Expected rendered windows: $($expected -join ', '); actual: $($actual -join ', ')."
}

function Close-TestWindow($Window) {
    [ViewerTestWindows]::Close($Window.Handle)
}

if (@(Get-ViewerProcess).Count -ne 0) {
    throw "Close existing md-viewer windows before running this test."
}

$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ("md-viewer-test-" + [Guid]::NewGuid())
$null = New-Item -ItemType Directory -Path $testDirectory
$x = Join-Path $testDirectory "x.md"
$y = Join-Path $testDirectory "y.md"
$z = Join-Path $testDirectory "z.markdown"
[IO.File]::WriteAllText($x, "# Multi-window X`n`nFirst document.")
[IO.File]::WriteAllText($y, "# Multi-window Y`n`nSecond document.")
[IO.File]::WriteAllText($z, "# Multi-window Z`n`nThird document.")
$script:viewer = $null

try {
    Open-TestFile $x
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    do {
        $processes = @(Get-ViewerProcess)
        if ($processes.Count -eq 1) {
            $script:viewer = $processes[0]
            break
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($null -eq $script:viewer -or $script:viewer.Path -ne $Executable) {
        throw "The launch did not start the requested executable: $Executable."
    }
    $null = $script:viewer.Handle

    # Launch while the first window may still be initializing WebView2.
    Open-TestFile $y
    Wait-Documents @("Multi-window X", "Multi-window Y")
    $first = @(Get-ViewerWindows | Where-Object { $_.Name -eq "Multi-window X - md-viewer" })[0]
    Open-TestFile $x
    Wait-Documents @("Multi-window X", "Multi-window X", "Multi-window Y")

    Close-TestWindow $first
    Wait-Documents @("Multi-window X", "Multi-window Y")
    Open-TestFile $z
    Wait-Documents @("Multi-window X", "Multi-window Y", "Multi-window Z")
    if (@(Get-ViewerProcess).Count -ne 1) {
        throw "Shell launches did not reuse one process."
    }

    foreach ($window in @(Get-ViewerWindows)) { Close-TestWindow $window }
    if (!$script:viewer.WaitForExit(10000)) {
        throw "The viewer did not exit after its last window closed."
    }
    if ($script:viewer.ExitCode -ne 0) {
        throw "The viewer exited with code $($script:viewer.ExitCode)."
    }
    Write-Host "PASS: two-file opens, duplicate opens, document titles, independent closing, .markdown opens, shared process, and last-window exit."
} finally {
    if ($null -ne $script:viewer -and !$script:viewer.HasExited) {
        Stop-Process -Id $script:viewer.Id
    }
    Remove-Item -LiteralPath $x, $y, $z -Force
    Remove-Item -LiteralPath $testDirectory -Force
}
