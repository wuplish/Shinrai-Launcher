using System.Diagnostics;
using System.IO;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public class BedrockStatusInfo
{
    public bool IsPortableReady { get; set; }
    public string? PortableExePath { get; set; }
    public bool IsSystemInstalled { get; set; }
    public string? SystemInstallPath { get; set; }
    public string StatusSummary { get; set; } = "";
}

public static class BedrockLaunchEngine
{
    public static BedrockStatusInfo CheckBedrockStatus(LauncherConfig config)
    {
        var info = new BedrockStatusInfo();

        // 1. Check custom path or game_data/bedrock
        if (!string.IsNullOrWhiteSpace(config.CustomBedrockExePath) && File.Exists(config.CustomBedrockExePath))
        {
            info.IsPortableReady = true;
            info.PortableExePath = config.CustomBedrockExePath;
        }
        else
        {
            string bedrockDir = config.GetBedrockDir();
            if (Directory.Exists(bedrockDir))
            {
                var exeFiles = Directory.GetFiles(bedrockDir, "Minecraft.Windows.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    info.IsPortableReady = true;
                    info.PortableExePath = exeFiles[0];
                }
            }
        }

        // 2. Check if system has GDK or UWP Bedrock installed (useful at home to export or launch)
        string[] systemCandidates =
        [
            @"C:\XboxGames\Minecraft for Windows\Content\Minecraft.Windows.exe",
            @"D:\XboxGames\Minecraft for Windows\Content\Minecraft.Windows.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps")
        ];

        foreach (string path in systemCandidates.Take(2))
        {
            if (File.Exists(path))
            {
                info.IsSystemInstalled = true;
                info.SystemInstallPath = path;
                break;
            }
        }

        string uwpDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "Microsoft.MinecraftUWP_8wekyb3d8bbwe");

        if (Directory.Exists(uwpDataDir))
        {
            info.IsSystemInstalled = true;
        }

        if (info.IsPortableReady)
            info.StatusSummary = $"Portable Bedrock Hazır (Adminsiz Çalışır): {Path.GetFileName(Path.GetDirectoryName(info.PortableExePath))}";
        else if (info.IsSystemInstalled)
            info.StatusSummary = "Sistemde Bedrock Algılandı (Portable klasöre aktarılabilir veya doğrudan açılabilir)";
        else
            info.StatusSummary = "Portable Bedrock klasörü ('game_data/bedrock') boş — GitHub'dan indirin veya klasöre kopyalayın";

        return info;
    }

    /// <summary>
    /// Bedrock Edition için Cracked/Offline oyuncu adını (mp_username), FPS ayarlarını ve seçili Skin'i
    /// hem Portable klasöre hem de GDK/UWP com.mojang klasörlerine yazar.
    /// </summary>
    public static void ApplyOfflineProfileToBedrock(LauncherConfig config, Action<string>? log = null)
    {
        string cleanUsername = OfflineAuthService.SanitizeUsername(config.Username);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] mojangDirs =
        [
            // 1. Portable local Bedrock data folder
            Path.Combine(config.GetBedrockDir(), "games", "com.mojang", "minecraftpe"),
            // 2. Modern GDK Bedrock AppData path (1.21.120+ Win32/GDK)
            Path.Combine(appData, "Minecraft Bedrock", "Users", "Shared", "games", "com.mojang", "minecraftpe"),
            // 3. UWP LocalState path
            Path.Combine(localAppData, "Packages", "Microsoft.MinecraftUWP_8wekyb3d8bbwe", "LocalState", "games", "com.mojang", "minecraftpe")
        ];

        foreach (string peDir in mojangDirs)
        {
            try
            {
                Directory.CreateDirectory(peDir);
                string optionsFile = Path.Combine(peDir, "options.txt");
                var lines = File.Exists(optionsFile) ? File.ReadAllLines(optionsFile).ToList() : new List<string>();

                SetBedrockOption(lines, "mp_username", cleanUsername);
                if (config.AutoApplyFpsOptions)
                {
                    SetBedrockOption(lines, "gfx_vsync", "0");
                    SetBedrockOption(lines, "gfx_max_framerate", "0");
                    SetBedrockOption(lines, "gfx_fancygraphics", "0");
                }

                File.WriteAllLines(optionsFile, lines);

                // Copy custom skin if enabled
                if (config.ApplySkinLocally && !string.IsNullOrWhiteSpace(config.SelectedSkinPath) && File.Exists(config.SelectedSkinPath))
                {
                    File.Copy(config.SelectedSkinPath, Path.Combine(peDir, "custom.png"), overwrite: true);
                }
            }
            catch
            {
                // Ignore inaccessible paths
            }
        }

        log?.Invoke($"[Bedrock Offline] Gamertag '{cleanUsername}' ve yerel skin Bedrock yapılandırmasına işlendi.");
    }

    private static void SetBedrockOption(List<string> lines, string key, string value)
    {
        string prefix = key + ":";
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}:{value}";
                return;
            }
        }
        lines.Add($"{key}:{value}");
    }

    public static Process? LaunchBedrockEdition(LauncherConfig config, Action<string> log)
    {
        ApplyOfflineProfileToBedrock(config, log);
        var status = CheckBedrockStatus(config);

        // 1. Prefer Portable Exe (Requires zero Admin rights on school computers)
        string? exeToLaunch = status.PortableExePath ?? status.SystemInstallPath;
        if (!string.IsNullOrWhiteSpace(exeToLaunch) && File.Exists(exeToLaunch))
        {
            string workDir = Path.GetDirectoryName(exeToLaunch)!;
            log($"[Bedrock Motoru] Portable Win32/GDK yürütülebilir dosyası başlatılıyor: {exeToLaunch}");

            var psi = new ProcessStartInfo
            {
                FileName = exeToLaunch,
                WorkingDirectory = workDir,
                UseShellExecute = false
            };

            // Add portable folder to PATH so local vcruntime140_1.dll / XGameRuntime hooks load first without admin install
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.EnvironmentVariables["PATH"] = $"{workDir};{currentPath}";

            return Process.Start(psi);
        }

        // 2. Fallback: UWP protocol launch if installed on system
        if (status.IsSystemInstalled)
        {
            log("[Bedrock Motoru] Sistemde kurulu Minecraft Bedrock protokol üzerinden başlatılıyor...");
            var psi = new ProcessStartInfo
            {
                FileName = "minecraft://",
                UseShellExecute = true
            };
            return Process.Start(psi);
        }

        throw new FileNotFoundException(
            "Portable Bedrock ('game_data/bedrock/Minecraft.Windows.exe') bulunamadı!\n\n" +
            "Okul bilgisayarlarında Admin yetkisi olmadan Bedrock oynamak için:\n" +
            "1. 'GitHub & Paketleyici' sekmesinden Bedrock Portable paketini indirin VEYA\n" +
            "2. Taşınabilir Bedrock (Win32/GDK) dosyalarını 'game_data/bedrock' klasörüne çıkarın.");
    }
}
