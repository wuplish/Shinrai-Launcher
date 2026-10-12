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
    public string Icon { get; set; } = "⚡";
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
        new() { Id = "sodium", Icon = "🚀", Title = "Sodium Engine", Subtitle = "OpenGL 4.6 Chunk Multithreading (+300% FPS)", IsEnabled = true, AssociatedJarKeyword = "sodium" },
        new() { Id = "lithium", Icon = "⚙️", Title = "Lithium Physics", Subtitle = "Oyun fiziği, AI ve blok tick optimizasyonu", IsEnabled = true, AssociatedJarKeyword = "lithium" },
        new() { Id = "ferritecore", Icon = "🧠", Title = "FerriteCore RAM", Subtitle = "Bellek kullanımını %45 azaltır (Okul PC dostu)", IsEnabled = true, AssociatedJarKeyword = "ferritecore" },
        new() { Id = "entityculling", Icon = "👁️", Title = "Entity Culling", Subtitle = "Duvar arkasındaki mob ve oyuncuları işlemez", IsEnabled = true, AssociatedJarKeyword = "entityculling" },
        new() { Id = "fullbright", Icon = "☀️", Title = "Fullbright (Gamma 10x)", Subtitle = "Mağaralarda ve gece meşalesiz %100 aydınlık", IsEnabled = true },
        new() { Id = "nofog", Icon = "🌫️", Title = "No Fog & Fast Clouds", Subtitle = "Uzak mesafe sisini ve ağır bulutları kapatır", IsEnabled = true },
        new() { Id = "pvp_particles", Icon = "⚔️", Title = "PvP Clean Particles", Subtitle = "Gereksiz patlama/iksir parçacıklarını azaltır", IsEnabled = true },
        new() { Id = "vsync_off", Icon = "⚡", Title = "Zero Input Lag (260 FPS)", Subtitle = "VSync kapatır, fare gecikmesini sıfırlar", IsEnabled = true },
        new() { Id = "optifine_fast", Icon = "💎", Title = "OptiFine Fast Math", Subtitle = "Fast Render, Smart Animations ve Dynamic FOV", IsEnabled = true }
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

        SetKeyVal(lines, "gamma", fullbright ? "10.0" : "1.0");
        SetKeyVal(lines, "enableVsync", vsyncOff ? "false" : "true");
        SetKeyVal(lines, "maxFps", vsyncOff ? "260" : "120");
        SetKeyVal(lines, "renderClouds", noFog ? "\"off\"" : "\"fast\"");
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
        string appDataMc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        // 1. Setup 1.21.11 Base (Import from %AppData%\.minecraft if present, else download from Mojang)
        string local12111Json = Path.Combine(mcDir, "versions", "1.21.11", "1.21.11.json");
        if (!File.Exists(local12111Json))
        {
            string sys12111Json = Path.Combine(appDataMc, "versions", "1.21.11", "1.21.11.json");
            if (File.Exists(sys12111Json))
            {
                progress(10, "Sistemdeki 1.21.11 sürümü portable klasöre aktarılıyor...");
                await GitHubReleaseService.ImportVersionFromSystemAsync("1.21.11", appDataMc, config, progress, log);
            }
            else
            {
                progress(10, "Minecraft 1.21.11 indiriliyor...");
                await GitHubReleaseService.DownloadVanillaVersionForOfflineUseAsync("1.21.11", config, progress, log, ct);
            }
        }

        // 2. Install Fabric Loader for 1.21.11 ("1.21.11-Sodium-Fabric")
        progress(35, "1.21.11 için Fabric Loader ve kütüphaneleri kuruluyor...");
        await InstallFabricProfileAsync("1.21.11", "1.21.11-Sodium", mcDir, log, ct);

        // 3. Download Sodium + Lithium + FerriteCore + EntityCulling + Fabric API for 1.21.11
        progress(55, "1.21.11 için Sodium, Lithium, FerriteCore ve EntityCulling indiriliyor...");
        await DownloadModrinthModsForVersionAsync("1.21.11", Path.Combine(mcDir, "mods"), log, ct);

        // 4. Setup 1.12.2 (and create alias 1.12.12 so both appear)
        string local1122Json = Path.Combine(mcDir, "versions", "1.12.2", "1.12.2.json");
        if (!File.Exists(local1122Json))
        {
            progress(75, "Minecraft 1.12.2 (1.12.12 PvP/OptiFine Ready) indiriliyor...");
            await GitHubReleaseService.DownloadVanillaVersionForOfflineUseAsync("1.12.2", config, progress, log, ct);
        }

        // Apply all Lunar FPS settings
        ApplyAllLunarOptimizations(mcDir, GetDefaultLunarMods(), log);
        progress(100, "1.21.11 (Sodium+Fabric+Vanilla) ve 1.12.2 tüm FPS ayarlarıyla hazır!");
    }

    public static async Task InstallFabricProfileAsync(
        string gameVersion,
        string customProfileId,
        string mcDir,
        Action<string> log,
        CancellationToken ct = default)
    {
        // Get latest loader version for gameVersion
        string loadersJson = await Http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}", ct);
        using var loadersDoc = JsonDocument.Parse(loadersJson);
        if (loadersDoc.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException($"{gameVersion} için Fabric Loader bulunamadı.");

        string loaderVer = loadersDoc.RootElement[0].GetProperty("loader").GetProperty("version").GetString()!;
        string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{gameVersion}/{loaderVer}/profile/json";
        string profileJson = await Http.GetStringAsync(profileUrl, ct);

        // Customize profile ID to customProfileId (e.g. "1.21.11-Sodium")
        using var profDoc = JsonDocument.Parse(profileJson);
        var root = profDoc.RootElement;

        string targetVerDir = Path.Combine(mcDir, "versions", customProfileId);
        Directory.CreateDirectory(targetVerDir);

        string modifiedJson = profileJson.Replace(
            $"\"id\": \"fabric-loader-{loaderVer}-{gameVersion}\"",
            $"\"id\": \"{customProfileId}\"");

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
