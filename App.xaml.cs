using System.Windows;
using ShinraiLauncher.Models;
using ShinraiLauncher.Services;

namespace ShinraiLauncher;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any(a => a.Equals("--sync-assets", StringComparison.OrdinalIgnoreCase)))
        {
            var cfg = LauncherConfig.Load();
            string mcDir = cfg.GetMinecraftDir();
            Console.WriteLine($"[Sync] Syncing local assets in {mcDir}...");
            await JavaLaunchEngine.EnsureLocalAssetsAndSanitizeIndexAsync(mcDir, "29", Console.WriteLine);
            await JavaLaunchEngine.EnsureLocalAssetsAndSanitizeIndexAsync(mcDir, "1.12", Console.WriteLine);
            OfflineSkinService.EnsureDefaultSkinsAndCapes();
            OfflineSkinService.ApplyOfflineSkinToJava(cfg, Console.WriteLine);
            LunarOptimizerService.ApplyAllLunarOptimizations(mcDir, LunarOptimizerService.GetDefaultLunarMods(), Console.WriteLine);
            Console.WriteLine("[Sync] Completed successfully.");
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
    }
}
