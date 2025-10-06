using System.Configuration;
using System.Data;
using System.Windows;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ReMux2;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static Microsoft.Extensions.Configuration.IConfiguration? Configuration { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var msg = $"Unhandled exception: {ex?.Message}{Environment.NewLine}{ex?.StackTrace}";
            System.Windows.MessageBox.Show(msg, "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        // Check for .NET Desktop Runtime
        if (!IsNetDesktopRuntimeInstalled())
        {
            System.Windows.MessageBox.Show(
                "This application requires the .NET Desktop Runtime (Windows Desktop). It was not detected on this system.\n\nPlease install it from:\nhttps://dotnet.microsoft.com/download/dotnet",
                "Missing .NET Desktop Runtime",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            // Optionally exit to avoid cryptic errors later
            // Shutdown();
        }

        // Load configuration from appsettings.json
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        Configuration = builder.Build();
    }

    private static bool IsNetDesktopRuntimeInstalled()
    {
        // Registry (x64)
        var hklm = Microsoft.Win32.Registry.LocalMachine;
        string[] keyPaths = new[]
        {
            @"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedfx\\Microsoft.WindowsDesktop.App",
            @"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x86\\sharedfx\\Microsoft.WindowsDesktop.App"
        };
        foreach (var kp in keyPaths)
        {
            try
            {
                using var key = hklm.OpenSubKey(kp);
                if (key != null)
                {
                    var subNames = key.GetSubKeyNames();
                    if (subNames != null && subNames.Length > 0)
                        return true;
                }
            }
            catch { }
        }

        // File system fallback
        try
        {
            var dirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.WindowsDesktop.App"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "shared", "Microsoft.WindowsDesktop.App")
            };
            foreach (var d in dirs)
            {
                if (Directory.Exists(d) && Directory.EnumerateDirectories(d).Any())
                    return true;
            }
        }
        catch { }

        return false;
    }
}

