using System.IO;
using System.Net.Http;
using System.Text.Json;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public class LunarModToggleItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string IconPath { get; set; } = "";
    public string BadgeBg { get; set; } = "#251B42";
    public string BadgeFg { get; set; } = "#A78BFA";
    public bool IsEnabled { get; set; } = true;
    public string? AssociatedJarKeyword { get; set; }
}

public static class LunarOptimizerService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("ShinraiLunarClient/2.0");
        return c;
    }

    public static List<LunarModToggleItem> GetDefaultLunarMods() =>
    [
        new() { Id = "sodium", BadgeBg = "#2E1065", BadgeFg = "#C084FC", IconPath = "M13,2 L3,14 L12,14 L11,22 L21,10 L12,10 Z", Title = "Sodium Engine", Subtitle = "OpenGL 4.6 Chunk Multithreading (+300% FPS)", IsEnabled = true, AssociatedJarKeyword = "sodium" },
        new() { Id = "lithium", BadgeBg = "#1E1B4B", BadgeFg = "#818CF8", IconPath = "M12,8 A4,4 0 1,0 12,16 A4,4 0 1,0 12,8 M12,2 L14,5 L19,4 L18,9 L22,12 L18,15 L19,20 L14,19 L12,22 L10,19 L5,20 L6,15 L2,12 L6,9 L5,4 L10,5 Z", Title = "Lithium Physics", Subtitle = "Oyun fiziği, AI ve blok tick optimizasyonu", IsEnabled = true, AssociatedJarKeyword = "lithium" },
        new() { Id = "ferritecore", BadgeBg = "#082F49", BadgeFg = "#38BDF8", IconPath = "M6,4 H18 V20 H6 Z M2,8 H6 M2,12 H6 M2,16 H6 M18,8 H22 M18,12 H22 M18,16 H22", Title = "FerriteCore RAM", Subtitle = "Bellek kullanımını %45 azaltır (Okul PC dostu)", IsEnabled = true, AssociatedJarKeyword = "ferritecore" },
        new() { Id = "entityculling", BadgeBg = "#3B0764", BadgeFg = "#E879F9", IconPath = "M1,12 C4,6 8,4 12,4 C16,4 20,6 23,12 C20,18 16,20 12,20 C8,20 4,18 1,12 Z M12,9 A3,3 0 1,0 12,15 A3,3 0 1,0 12,9", Title = "Entity Culling", Subtitle = "Duvar arkasındaki mob ve oyuncuları işlemez", IsEnabled = true, AssociatedJarKeyword = "entityculling" },
        new() { Id = "fullbright", BadgeBg = "#451A03", BadgeFg = "#FBBF24", IconPath = "M12,7 A5,5 0 1,0 12,17 A5,5 0 1,0 12,7 M12,1 V4 M12,20 V23 M4.2,4.2 L6.3,6.3 M17.7,17.7 L19.8,19.8 M1,12 H4 M20,12 H23 M4.2,19.8 L6.3,17.7 M17.7,6.3 L19.8,4.2", Title = "Fullbright (Max Gamma)", Subtitle = "Mağaralarda ve gece meşalesiz %100 aydınlık", IsEnabled = true },
        new() { Id = "nofog", BadgeBg = "#0C2D48", BadgeFg = "#7DD3FC", IconPath = "M4,10 H20 M2,14 H18 M6,18 H22 M7,6 H17", Title = "No Fog & Fast Clouds", Subtitle = "Uzak mesafe sisini ve ağır bulutları kapatır", IsEnabled = true },
        new() { Id = "pvp_particles", BadgeBg = "#450A0A", BadgeFg = "#F87171", IconPath = "M14.5,17.5 L3,6 V3 H6 L17.5,14.5 M13,19 L19,13 M16,16 L21,21 M19,21 L21,19", Title = "PvP Clean Particles", Subtitle = "Gereksiz patlama/iksir parçacıklarını azaltır", IsEnabled = true },
        new() { Id = "vsync_off", BadgeBg = "#2E1065", BadgeFg = "#A78BFA", IconPath = "M13,2 L3,14 L12,14 L11,22 L21,10 L12,10 Z", Title = "Zero Input Lag (260 FPS)", Subtitle = "VSync kapatır, fare gecikmesini sıfırlar", IsEnabled = true },
        new() { Id = "optifine_fast", BadgeBg = "#172554", BadgeFg = "#60A5FA", IconPath = "M6,3 H18 L22,9 L12,21 L2,9 Z", Title = "OptiFine Fast Math", Subtitle = "Fast Render, Smart Animations ve Dynamic FOV", IsEnabled = true }
    ];

    /// <summary>
    /// Lunar Mod Menüsündeki açık/kapalı durumlarına göre hem .jar modlarını hem de
    /// options.txt, optionsof.txt (OptiFine) ve sodium-options.json (Sodium) dosyalarını maksimum FPS için yapılandırır.
    /// </summary>
    public static void ApplyAllLunarOptimizations(string mcDir, IEnumerable<LunarModToggleItem> mods, Action<string>? log = null)
    {
        var modMap = mods.ToDictionary(m => m.Id, m => m.IsEnabled, StringComparer.OrdinalIgnoreCase);

        bool fullbright = !modMap.TryGetValue("fullbright", out bool fb) || fb;
        bool noFog = !modMap.TryGetValue("nofog", out bool nf) || nf;
        bool pvpParticles = !modMap.TryGetValue("pvp_particles", out bool pp) || pp;
        bool vsyncOff = !modMap.TryGetValue("vsync_off", out bool vo) || vo;
        bool optifineFast = !modMap.TryGetValue("optifine_fast", out bool of) || of;

        // 1. Toggle .jar / .jar.disabled in mods/
        string modsDir = Path.Combine(mcDir, "mods");
        Directory.CreateDirectory(modsDir);
        foreach (var item in mods.Where(m => !string.IsNullOrWhiteSpace(m.AssociatedJarKeyword)))
        {
            ToggleJarByKeyword(modsDir, item.AssociatedJarKeyword!, item.IsEnabled);
        }

        // 2. Write Vanilla options.txt
        string optionsPath = Path.Combine(mcDir, "options.txt");
        var lines = File.Exists(optionsPath) ? File.ReadAllLines(optionsPath).ToList() : new List<string>();

        SetKeyVal(lines, "gamma", fullbright ? "1.0" : "0.5");
        SetKeyVal(lines, "enableVsync", vsyncOff ? "false" : "true");
        SetKeyVal(lines, "maxFps", vsyncOff ? "260" : "120");
        SetKeyVal(lines, "renderClouds", noFog ? "\"false\"" : "\"fast\"");
        SetKeyVal(lines, "graphicsMode", "0"); // Fast
        SetKeyVal(lines, "particles", pvpParticles ? "1" : "0"); // Decreased particles
        SetKeyVal(lines, "entityShadows", "false");
        SetKeyVal(lines, "biomeBlendRadius", "0");
        SetKeyVal(lines, "renderDistance", "8");
        SetKeyVal(lines, "simulationDistance", "6");
        SetKeyVal(lines, "entityDistanceScaling", "0.75");
        SetKeyVal(lines, "PrioritizeChunkUpdates", "0");
        SetKeyVal(lines, "rawMouseInput", "true");
        SetKeyVal(lines, "bobView", "true");
        SetKeyVal(lines, "fovEffectScale", optifineFast ? "0.0" : "1.0");
        SetKeyVal(lines, "screenEffectScale", "0.0");
        SetKeyVal(lines, "darknessEffectScale", "0.0");
        SetKeyVal(lines, "glintSpeed", "0.5");

        File.WriteAllLines(optionsPath, lines);

        // 3. Write OptiFine optionsof.txt (for 1.12.2 / 1.21 OptiFine profiles)
        string optionsOfPath = Path.Combine(mcDir, "optionsof.txt");
        var ofLines = File.Exists(optionsOfPath) ? File.ReadAllLines(optionsOfPath).ToList() : new List<string>();

        SetKeyVal(ofLines, "ofFogType", noFog ? "3" : "1"); // 3 = OFF
        SetKeyVal(ofLines, "ofFogStart", "0.8");
        SetKeyVal(ofLines, "ofSmoothFps", "false");
        SetKeyVal(ofLines, "ofSmoothWorld", "true");
        SetKeyVal(ofLines, "ofFastRender", optifineFast ? "true" : "false");
        SetKeyVal(ofLines, "ofFastMath", optifineFast ? "true" : "false");
        SetKeyVal(ofLines, "ofSmartAnimations", optifineFast ? "true" : "false");
        SetKeyVal(ofLines, "ofLazyChunkLoading", "true");
        SetKeyVal(ofLines, "ofRenderRegions", "true");
        SetKeyVal(ofLines, "ofDynamicFov", optifineFast ? "false" : "true");
        SetKeyVal(ofLines, "ofDynamicLights", "2"); // Fast dynamic lights
        SetKeyVal(ofLines, "ofConnectedTextures", "2");
        SetKeyVal(ofLines, "ofSky", "false");
        SetKeyVal(ofLines, "ofStars", "true");
        SetKeyVal(ofLines, "ofSunMoon", "false");
        SetKeyVal(ofLines, "ofVignette", "1"); // Fast

        File.WriteAllLines(optionsOfPath, ofLines);

        // 4. Write Sodium config/sodium-options.json
        string configDir = Path.Combine(mcDir, "config");
        Directory.CreateDirectory(configDir);
        string sodiumConfig = $$"""
        {
          "quality": {
            "clouds_quality": "OFF",
            "weather_quality": "FAST",
            "leaves_quality": "FAST",
            "enable_vignette": false,
            "enable_fog": {{(noFog ? "false" : "true")}}
          },
          "performance": {
            "chunk_builder_threads": 0,
            "always_defer_chunk_updates": true,
            "animate_only_visible_textures": true,
            "use_entity_culling": true,
            "use_fog_occlusion": true,
            "use_block_face_culling": true,
            "use_no_error_gl_context": true
          },
          "advanced": {
            "enable_memory_tracing": false,
            "use_advanced_staging_buffers": true,
            "cpu_render_ahead_limit": 3
          }
        }
        """;
        File.WriteAllText(Path.Combine(configDir, "sodium-options.json"), sodiumConfig);

        log?.Invoke("[Lunar Optimizer] Sodium, OptiFine (optionsof.txt) ve Vanilla FPS ayarları uygulandı.");
    }

    private static void ToggleJarByKeyword(string modsDir, string keyword, bool enable)
    {
        foreach (string file in Directory.GetFiles(modsDir))
        {
            string name = Path.GetFileName(file);
            if (!name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isCurrentlyDisabled = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            if (enable && isCurrentlyDisabled)
            {
                string target = file[..^".disabled".Length];
                File.Move(file, target, overwrite: true);
            }
            else if (!enable && !isCurrentlyDisabled && name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                File.Move(file, file + ".disabled", overwrite: true);
            }
        }
    }

    private static void SetKeyVal(List<string> lines, string key, string val)
    {
        string prefix = key + ":";
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}:{val}";
                return;
            }
        }
        lines.Add($"{key}:{val}");
    }

    /// <summary>
    /// 1.21.11 için Fabric Loader + Sodium + Lithium + FerriteCore + EntityCulling + Fabric API
    /// ve 1.12.2 (1.12.12) sürümünü tek tıkla local 'game_data/.minecraft' içine kurar!
    /// </summary>
    public static async Task SetupTargetVersionsWithSodiumAsync(
        LauncherConfig config,
        Action<double, string> progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        string mcDir = config.GetMinecraftDir();

        // 1. Ensure 1.21.11 + all 107 libraries are downloaded locally
        progress(10, "Minecraft 1.21.11 ve tüm kütüphaneleri doğrulanıyor/indiriliyor...");
        await GitHubReleaseService.DownloadVanillaVersionForOfflineUseAsync("1.21.11", config, progress, log, ct);

        // 2. Install Fabric Loader for 1.21.11 ("1.21.11-Sodium")
        progress(35, "1.21.11 için Fabric Loader ve kütüphaneleri kuruluyor...");
        await InstallFabricProfileAsync("1.21.11", "1.21.11-Sodium", mcDir, log, ct);

        // 3. Download Sodium + Lithium + FerriteCore + EntityCulling + Fabric API for 1.21.11
        progress(55, "1.21.11 için Sodium, Lithium, FerriteCore ve EntityCulling indiriliyor...");
        await DownloadModrinthModsForVersionAsync("1.21.11", Path.Combine(mcDir, "mods"), log, ct);

        // 4. Ensure 1.12.2 (and 1.12.12 alias) + all libraries are downloaded locally
        progress(70, "Minecraft 1.12.2 (1.12.12 PvP/OptiFine) ve kütüphaneleri indiriliyor...");
        await GitHubReleaseService.DownloadVanillaVersionForOfflineUseAsync("1.12.2", config, progress, log, ct);

        // 5. Ensure Portable Java 21 is present in game_data/runtime/java-21
        if (JavaLaunchEngine.FindJavaExecutable(config, 21) == null)
        {
            progress(85, "Portable Java 21 ('game_data/runtime/java-21') indiriliyor...");
            await GitHubReleaseService.DownloadPortableJavaJreAsync(21, config, progress, log, ct);
        }

        // Apply all Lunar FPS settings
        ApplyAllLunarOptimizations(mcDir, GetDefaultLunarMods(), log);
        progress(100, "1.21.11 (Sodium+Fabric+Vanilla), 1.12.2 ve Portable Java tamamen hazır!");
    }

    public static async Task InstallFabricProfileAsync(
        string gameVersion,
        string customProfileId,
        string mcDir,
        Action<string> log,
        CancellationToken ct = default)
    {
        string loadersJson = await Http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}", ct);
        using var loadersDoc = JsonDocument.Parse(loadersJson);
        if (loadersDoc.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException($"{gameVersion} için Fabric Loader bulunamadı.");

        string loaderVer = loadersDoc.RootElement[0].GetProperty("loader").GetProperty("version").GetString()!;
        string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVer}/profile/json";
        string profileJson = await Http.GetStringAsync(profileUrl, ct);

        using var profDoc = JsonDocument.Parse(profileJson);
        var root = profDoc.RootElement;

        string targetVerDir = Path.Combine(mcDir, "versions", customProfileId);
        Directory.CreateDirectory(targetVerDir);

        string modifiedJson = profileJson
            .Replace($"\"id\":\"fabric-loader-{loaderVer}-{gameVersion}\"", $"\"id\":\"{customProfileId}\"")
            .Replace($"\"id\": \"fabric-loader-{loaderVer}-{gameVersion}\"", $"\"id\": \"{customProfileId}\"");

        await File.WriteAllTextAsync(Path.Combine(targetVerDir, $"{customProfileId}.json"), modifiedJson, ct);

        // Download all Fabric Maven libraries into mcDir/libraries
        string librariesDir = Path.Combine(mcDir, "libraries");
        if (root.TryGetProperty("libraries", out var libsArr) && libsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var lib in libsArr.EnumerateArray())
            {
                string name = lib.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                string baseUrl = lib.TryGetProperty("url", out var uProp) ? (uProp.GetString() ?? "https://maven.fabricmc.net/") : "https://maven.fabricmc.net/";
                if (string.IsNullOrWhiteSpace(name)) continue;

                string[] parts = name.Split(':');
                if (parts.Length < 3) continue;
                string relPath = $"{parts[0].Replace('.', '/')}/{parts[1]}/{parts[2]}/{parts[1]}-{parts[2]}.jar";
                string fullDest = Path.Combine(librariesDir, relPath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(fullDest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fullDest)!);
                    string dlUrl = baseUrl.TrimEnd('/') + "/" + relPath;
                    try
                    {
                        using var resp = await Http.GetAsync(dlUrl, ct);
                        resp.EnsureSuccessStatusCode();
                        await using var fs = new FileStream(fullDest, FileMode.Create, FileAccess.Write, FileShare.None);
                        await resp.Content.CopyToAsync(fs, ct);
                    }
                    catch (Exception ex)
                    {
                        log($"[Fabric Lib Uyarı] {name}: {ex.Message}");
                    }
                }
            }
        }

        log($"[Fabric Kurucu] '{customProfileId}' (Fabric {loaderVer}) başarıyla kuruldu.");
    }

    public static async Task DownloadModrinthModsForVersionAsync(
        string gameVersion,
        string modsDir,
        Action<string> log,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(modsDir);
        string[] projects = ["sodium", "fabric-api", "lithium", "ferrite-core", "entityculling"];

        foreach (string slug in projects)
        {
            try
            {
                string apiUrl = $"https://api.modrinth.com/v2/project/{slug}/version?game_versions=[\"{gameVersion}\"]&loaders=[\"fabric\"]";
                string json = await Http.GetStringAsync(apiUrl, ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.GetArrayLength() == 0) continue;

                var firstFile = doc.RootElement[0].GetProperty("files")[0];
                string fileName = firstFile.GetProperty("filename").GetString()!;
                string fileUrl = firstFile.GetProperty("url").GetString()!;

                string destPath = Path.Combine(modsDir, fileName);
                if (!File.Exists(destPath))
                {
                    log($"[Mod İndirici] {fileName} indiriliyor...");
                    using var resp = await Http.GetAsync(fileUrl, ct);
                    resp.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs, ct);
                }
            }
            catch (Exception ex)
            {
                log($"[Mod Uyarı] {slug} indirilemedi: {ex.Message}");
            }
        }
    }
}
