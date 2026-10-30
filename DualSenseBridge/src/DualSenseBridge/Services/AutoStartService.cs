using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace DualSenseBridge.Services;

/// <summary>
/// Starts the app at logon through a Task Scheduler task with highest privileges.
/// A Run registry key cannot start an app that requires elevation without a UAC prompt,
/// a scheduled task can – and it also lets us run at normal priority with no time limit.
/// </summary>
public static class AutoStartService
{
    public const string TaskName = "DualSenseBridge";
    private const string LegacyRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        var (exitCode, _) = RunSchtasks($"/Query /TN \"{TaskName}\"");
        return exitCode == 0;
    }

    public static bool Enable()
    {
        string exe = AppPaths.ExecutablePath;
        string user = WindowsIdentity.GetCurrent().Name;
        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Author>DualSense Bridge</Author>
                <Description>Pokreće DualSense Bridge (DualSense → Xbox 360) pri prijavi u Windows.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <Delay>PT3S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(user)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exe)}</Command>
                  <Arguments>--tray</Arguments>
                  <WorkingDirectory>{SecurityElement.Escape(Path.GetDirectoryName(exe))}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;

        string file = Path.Combine(Path.GetTempPath(), $"DualSenseBridge-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            var (exitCode, output) = RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F");
            if (exitCode != 0)
            {
                Log.Error($"schtasks /Create failed ({exitCode}): {output}");
                return false;
            }

            RemoveLegacyRunEntry();
            Log.Info("Autostart task registered");
            return true;
        }
        finally
        {
            try { File.Delete(file); } catch { /* temp file */ }
        }
    }

    public static bool Disable()
    {
        RemoveLegacyRunEntry();
        if (!IsEnabled())
        {
            return true;
        }

        var (exitCode, output) = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
        if (exitCode != 0)
        {
            Log.Error($"schtasks /Delete failed ({exitCode}): {output}");
            return false;
        }

        Log.Info("Autostart task removed");
        return true;
    }

    private static void RemoveLegacyRunEntry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
            key?.DeleteValue(AppPaths.AppName, throwOnMissingValue: false);
        }
        catch
        {
            // Not critical.
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi)!;
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                return (-1, "timeout");
            }

            return (process.ExitCode, output.Trim());
        }
        catch (Exception ex)
        {
            Log.Error("Could not run schtasks.exe", ex);
            return (-1, ex.Message);
        }
    }
}
