using System.IO;
using System.Text.Json;

namespace ShinraiLauncher.Models;

public enum JvmOptimizationPreset
{
    SchoolLowSpec,     // Okul PC / Düşük Sistem (Intel HD / Düşük RAM dostu)
    AikarsPerformance, // Aikar's Flags (Yüksek FPS & Stabilite - G1GC)
    ZgcUltraLowLatency,// Java 21+ Generational ZGC
    Custom             // Özel Argümanlar
}

public class LauncherConfig
{
    public string Username { get; set; } = "wuplish";
    public string SelectedEdition { get; set; } = "Java"; // "Java" or "Bedrock"
    public string SelectedJavaVersion { get; set; } = "1.21.11-Sodium";
    public int AllocatedRamMb { get; set; } = 4096;
    public int MinRamMb { get; set; } = 1024;
    public JvmOptimizationPreset JvmPreset { get; set; } = JvmOptimizationPreset.AikarsPerformance;
    public string CustomJvmArgs { get; set; } = "-XX:+UseG1GC -XX:MaxGCPauseMillis=120 -XX:+DisableExplicitGC";
    public bool FullOfflineIsolation { get; set; } = true;
    public bool AutoApplyFpsOptions { get; set; } = true;

    // Paths (relative to launcher folder by default so it stays 100% portable)
    public string CustomMinecraftDir { get; set; } = "";
    public string CustomJavaExePath { get; set; } = "";
    public string CustomBedrockExePath { get; set; } = "";

    // Skin & Cape
    public string SelectedSkinPath { get; set; } = "";
    public string SelectedCapePath { get; set; } = "";
    public bool IsSlimSkin { get; set; } = false;
    public bool ApplySkinLocally { get; set; } = true;

    // GitHub Releases Configuration
    public string GitHubRepo { get; set; } = "wuplish/Shinrai-Launcher";
    public string GitHubReleaseTag { get; set; } = "latest";
    public string DirectPackUrl { get; set; } = "";

    // LAN Preferences
    public string LastDirectConnectIp { get; set; } = "192.168.1.10:25565";

    private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shinrai_config.json");

    public static string GetPortableRootDir() => AppDomain.CurrentDomain.BaseDirectory;

    public string GetMinecraftDir()
    {
        if (!string.IsNullOrWhiteSpace(CustomMinecraftDir) && Directory.Exists(CustomMinecraftDir))
            return CustomMinecraftDir;

        string dir = Path.Combine(GetPortableRootDir(), "game_data", ".minecraft");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetBedrockDir()
    {
        if (!string.IsNullOrWhiteSpace(CustomBedrockExePath) && File.Exists(CustomBedrockExePath))
            return Path.GetDirectoryName(CustomBedrockExePath)!;

        string dir = Path.Combine(GetPortableRootDir(), "game_data", "bedrock");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GetRuntimeDir()
    {
        string dir = Path.Combine(GetPortableRootDir(), "game_data", "runtime");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var cfg = JsonSerializer.Deserialize<LauncherConfig>(json);
                if (cfg != null)
                    return cfg;
            }
        }
        catch
        {
            // Ignore corrupt config and use defaults
        }

        var defaultCfg = new LauncherConfig();
        defaultCfg.EnsureDirectories();
        defaultCfg.Save();
        return defaultCfg;
    }

    public void EnsureDirectories()
    {
        try
        {
            string mcDir = GetMinecraftDir();
            Directory.CreateDirectory(Path.Combine(mcDir, "versions"));
            Directory.CreateDirectory(Path.Combine(mcDir, "libraries"));
            Directory.CreateDirectory(Path.Combine(mcDir, "assets", "indexes"));
            Directory.CreateDirectory(Path.Combine(mcDir, "assets", "objects"));
            Directory.CreateDirectory(Path.Combine(mcDir, "mods"));
            Directory.CreateDirectory(Path.Combine(mcDir, "resourcepacks"));
            Directory.CreateDirectory(Path.Combine(mcDir, "saves"));
            Directory.CreateDirectory(GetBedrockDir());
            Directory.CreateDirectory(GetRuntimeDir());
            Directory.CreateDirectory(Path.Combine(GetPortableRootDir(), "game_data", "skins"));
            Directory.CreateDirectory(Path.Combine(GetPortableRootDir(), "game_data", "capes"));
        }
        catch
        {
            // Ignore directory creation errors on read-only drives
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(this, options));
        }
        catch
        {
            // Ignore write errors
        }
    }
}
