using System;
using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;

namespace Codeful.Services
{
    public class NotificationService
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        public void ShowNotification(string title, string message, bool playSound = true)
        {
            try
            {
                // Play notification sound if requested
                if (playSound)
                {
                    SystemSounds.Asterisk.Play();
                }

                // Use PowerShell to show a toast notification (Windows 10/11)
                var script = $@"
                    Add-Type -AssemblyName System.Windows.Forms
                    $notify = New-Object System.Windows.Forms.NotifyIcon
                    $notify.Icon = [System.Drawing.SystemIcons]::Information
                    $notify.BalloonTipTitle = '{title}'
                    $notify.BalloonTipText = '{message}'
                    $notify.Visible = $true
                    $notify.ShowBalloonTip(5000)
                    Start-Sleep -Seconds 6
                    $notify.Dispose()
                ";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                // Fallback to simple message box if notification fails
                System.Diagnostics.Debug.WriteLine($"Notification failed: {ex.Message}");
                
                // As a last resort, just play the sound
                if (playSound)
                {
                    try
                    {
                        SystemSounds.Exclamation.Play();
                    }
                    catch { }
                }
            }
        }

        public void ShowToastNotification(string title, string message)
        {
            try
            {
                // Modern Windows Toast notification using PowerShell
                var script = $@"
                    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                    [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                    [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

                    $template = @'
                    <toast>
                        <visual>
                            <binding template=""ToastGeneric"">
                                <text>{title}</text>
                                <text>{message}</text>
                            </binding>
                        </visual>
                        <audio src=""ms-winsoundevent:Notification.Default"" />
                    </toast>
                    '@

                    $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
                    $xml.LoadXml($template)
                    $toast = New-Object Windows.UI.Notifications.ToastNotification $xml
                    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Codeful').Show($toast)
                ";

                var processInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toast notification failed, falling back: {ex.Message}");
                // Fallback to balloon notification
                ShowNotification(title, message, true);
            }
        }
    }
}