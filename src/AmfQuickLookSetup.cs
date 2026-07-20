using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Forms;

namespace AmfQuickLook.Setup
{
    internal static class Program
    {
        private const string AppName = "AMF QuickLook";
        private const string Version = "0.1.3";
        private static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "AMF QuickLook");

        [STAThread]
        private static int Main(string[] args)
        {
            bool silent = HasArg(args, "/silent");
            try
            {
                if (HasArg(args, "/uninstall"))
                {
                    Uninstall();
                    if (!silent) MessageBox.Show("AMF QuickLook has been uninstalled.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }

                Install();
                if (!silent) MessageBox.Show("AMF QuickLook has been installed. You can open .amf files by double-clicking them.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                if (silent) Console.Error.WriteLine(ex.Message);
                else MessageBox.Show(ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static bool HasArg(string[] args, string value)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static void Install()
        {
            Directory.CreateDirectory(InstallDir);
            Directory.CreateDirectory(Path.Combine(InstallDir, "bin"));

            Extract("payload.AmfQuickLook.Core.dll", Path.Combine(InstallDir, "bin", "AmfQuickLook.Core.dll"));
            Extract("payload.AmfQuickLook.exe", Path.Combine(InstallDir, "bin", "AmfQuickLook.exe"));
            Extract("payload.AmfQuickLook.Shell.dll", Path.Combine(InstallDir, "bin", "AmfQuickLook.Shell.dll"));
            Extract("payload.install.ps1", Path.Combine(InstallDir, "install.ps1"));
            Extract("payload.uninstall.ps1", Path.Combine(InstallDir, "uninstall.ps1"));
            Extract("payload.README.md", Path.Combine(InstallDir, "README.md"));

            File.Copy(Application.ExecutablePath, Path.Combine(InstallDir, "AMFQuickLookSetup.exe"), true);
            RunPowerShell(Path.Combine(InstallDir, "install.ps1"));
            CreateShortcuts();
            RegisterUninstall();
        }

        private static void Uninstall()
        {
            var uninstallScript = Path.Combine(InstallDir, "uninstall.ps1");
            if (File.Exists(uninstallScript))
            {
                RunPowerShell(uninstallScript);
            }

            DeleteShortcut(Environment.SpecialFolder.DesktopDirectory, "AMF QuickLook.lnk");
            DeleteShortcut(Environment.SpecialFolder.StartMenu, Path.Combine("Programs", "AMF QuickLook", "AMF QuickLook.lnk"));
            DeleteShortcut(Environment.SpecialFolder.StartMenu, Path.Combine("Programs", "AMF QuickLook", "Uninstall AMF QuickLook.lnk"));

            var startFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "AMF QuickLook");
            if (Directory.Exists(startFolder)) Directory.Delete(startFolder, true);

            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
            {
                if (key != null) key.DeleteSubKeyTree("AMFQuickLook", false);
            }

            if (Directory.Exists(InstallDir))
            {
                try { Directory.Delete(InstallDir, true); }
                catch { ScheduleDeleteOnReboot(InstallDir); }
            }
        }

        private static void Extract(string resourceName, string outputPath)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Missing installer resource: " + resourceName);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                using (var output = File.Create(outputPath))
                {
                    input.CopyTo(output);
                }
            }
        }

        private static void RunPowerShell(string script)
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException("PowerShell script failed: " + script);
            }
        }

        private static void CreateShortcuts()
        {
            var appExe = Path.Combine(InstallDir, "bin", "AmfQuickLook.exe");
            var setupExe = Path.Combine(InstallDir, "AMFQuickLookSetup.exe");
            var startFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "AMF QuickLook");
            Directory.CreateDirectory(startFolder);

            CreateShortcut(Path.Combine(startFolder, "AMF QuickLook.lnk"), appExe, "", InstallDir, "Open AMF QuickLook");
            CreateShortcut(Path.Combine(startFolder, "Uninstall AMF QuickLook.lnk"), setupExe, "/uninstall", InstallDir, "Uninstall AMF QuickLook");
            CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "AMF QuickLook.lnk"), appExe, "", InstallDir, "Open AMF QuickLook");
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string description)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void DeleteShortcut(Environment.SpecialFolder folder, string relativePath)
        {
            var path = Path.Combine(Environment.GetFolderPath(folder), relativePath);
            if (File.Exists(path)) File.Delete(path);
        }

        private static void RegisterUninstall()
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AMFQuickLook"))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", "neomakers");
                key.SetValue("InstallLocation", InstallDir);
                key.SetValue("DisplayIcon", Path.Combine(InstallDir, "bin", "AmfQuickLook.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(InstallDir, "AMFQuickLookSetup.exe") + "\" /uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void ScheduleDeleteOnReboot(string path)
        {
            MoveFileEx(path, null, 4);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
    }
}
