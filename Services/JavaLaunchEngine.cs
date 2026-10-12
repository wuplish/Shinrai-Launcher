using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public static class JavaLaunchEngine
{
    public static List<InstalledVersionItem> ScanInstalledVersions(string mcDir)
    {
        var result = new List<InstalledVersionItem>();
        string versionsDir = Path.Combine(mcDir, "versions");
        if (!Directory.Exists(versionsDir))
            return result;

        foreach (string dir in Directory.GetDirectories(versionsDir))
        {
            string id = Path.GetFileName(dir);
            string jsonPath = Path.Combine(dir, $"{id}.json");
            if (!File.Exists(jsonPath))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = doc.RootElement;

                string type = root.TryGetProperty("type", out var tProp) ? (tProp.GetString() ?? "release") : "release";
                string? inheritsFrom = root.TryGetProperty("inheritsFrom", out var inhProp) ? inhProp.GetString() : null;
                string mainClass = root.TryGetProperty("mainClass", out var mcProp) ? (mcProp.GetString() ?? "") : "";

                int javaMajor = 8;
                if (root.TryGetProperty("javaVersion", out var jvProp) &&
                    jvProp.TryGetProperty("majorVersion", out var mvProp) &&
                    mvProp.TryGetInt32(out int parsedMajor))
                {
                    javaMajor = parsedMajor;
                }
                else
                {
                    javaMajor = InferJavaVersionFromId(inheritsFrom ?? id);
                }

                string lowerId = id.ToLowerInvariant();
                string badge = "Vanilla";
                if (lowerId.Contains("optifine"))
                    badge = "OptiFine • FPS+";
                else if (lowerId.Contains("fabric") || lowerId.Contains("sodium"))
                    badge = "Fabric / Sodium • FPS++";
                else if (lowerId.Contains("forge") || lowerId.Contains("neoforge"))
                    badge = "Forge • Modlu";
                else if (lowerId.Contains("quilt"))
                    badge = "Quilt • Modlu";

                string jarPath = Path.Combine(dir, $"{id}.jar");
                if (!File.Exists(jarPath) && !string.IsNullOrEmpty(inheritsFrom))
                {
                    jarPath = Path.Combine(versionsDir, inheritsFrom, $"{inheritsFrom}.jar");
                }

                result.Add(new InstalledVersionItem
                {
                    Id = id,
                    Type = type,
                    InheritsFrom = inheritsFrom,
                    MainClass = mainClass,
                    RequiredJavaMajor = javaMajor,
                    ProfileBadge = badge,
                    HasClientJar = File.Exists(jarPath)
                });
            }
            catch
            {
                // Skip invalid version json
            }
        }

        return result.OrderByDescending(v => v.Id).ToList();
    }

    private static int InferJavaVersionFromId(string versionId)
    {
        if (versionId.StartsWith("1.21") || versionId.StartsWith("1.20.5") || versionId.StartsWith("1.20.6"))
            return 21;
        if (versionId.StartsWith("1.18") || versionId.StartsWith("1.19") || versionId.StartsWith("1.20"))
            return 17;
        if (versionId.StartsWith("1.17"))
            return 16;
        return 8;
    }

    public static string? FindJavaExecutable(LauncherConfig config, int requiredJavaMajor, Action<string>? log = null)
    {
        // 1. User custom path
        if (!string.IsNullOrWhiteSpace(config.CustomJavaExePath) && File.Exists(config.CustomJavaExePath))
            return config.CustomJavaExePath;

        // 2. Exact Portable runtime directory inside game_data/runtime/java-{major}
        string runtimeDir = config.GetRuntimeDir();
        string exactPortableDir = Path.Combine(runtimeDir, $"java-{requiredJavaMajor}");
        if (Directory.Exists(exactPortableDir))
        {
            var exactFiles = Directory.GetFiles(exactPortableDir, "java.exe", SearchOption.AllDirectories);
            if (exactFiles.Length > 0)
                return exactFiles[0];
        }

        // Check any portable java in game_data/runtime
        if (Directory.Exists(runtimeDir))
        {
            var candidateFiles = Directory.GetFiles(runtimeDir, "java.exe", SearchOption.AllDirectories);
            if (candidateFiles.Length > 0)
                return candidateFiles[0];
        }

        return null;
    }

    public static string BuildJvmOptimizationArgs(LauncherConfig config)
    {
        return config.JvmPreset switch
        {
            JvmOptimizationPreset.SchoolLowSpec =>
                "-XX:+UseG1GC -XX:+UnlockExperimentalVMOptions -XX:MaxGCPauseMillis=50 " +
                "-XX:G1NewSizePercent=20 -XX:G1ReservePercent=20 -XX:G1HeapRegionSize=16M " +
                "-XX:+DisableExplicitGC -Dsun.rmi.dgc.server.gcInterval=2147483646",

            JvmOptimizationPreset.AikarsPerformance =>
                "-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 " +
                "-XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch " +
                "-XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M " +
                "-XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 " +
                "-XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 " +
                "-XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem " +
                "-XX:MaxTenuringThreshold=1 -Dusing.aikars.flags=https://mcflags.emc.gs",

            JvmOptimizationPreset.ZgcUltraLowLatency =>
                "-XX:+UnlockExperimentalVMOptions -XX:+UseZGC -XX:+DisableExplicitGC -XX:+AlwaysPreTouch",

            JvmOptimizationPreset.Custom => config.CustomJvmArgs ?? "",
            _ => "-XX:+UseG1GC"
        };
    }

    public static void ApplyFpsBoosterToOptionsTxt(string mcDir, string? targetVersionId = null, Action<string>? log = null)
    {
        try
        {
            string optionsPath = Path.Combine(mcDir, "options.txt");
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var existingLines = new List<string>();
            bool isLegacy112 = !string.IsNullOrWhiteSpace(targetVersionId) && targetVersionId.StartsWith("1.12", StringComparison.OrdinalIgnoreCase);

            if (File.Exists(optionsPath))
            {
                foreach (string line in File.ReadAllLines(optionsPath))
                {
                    int sep = line.IndexOf(':');
                    if (sep > 0)
                    {
                        string k = line[..sep];
                        string v = line[(sep + 1)..];
                        // Prevent 1.21 string keybinds (key.keyboard.*) from polluting 1.12 integer keybinds and vice-versa
                        if (k.StartsWith("key_", StringComparison.OrdinalIgnoreCase))
                        {
                            bool isModernKeybind = v.StartsWith("key.", StringComparison.OrdinalIgnoreCase);
                            if (isLegacy112 && isModernKeybind) continue;
                            if (!isLegacy112 && !isModernKeybind && !string.IsNullOrWhiteSpace(targetVersionId)) continue;
                        }
                        if (k.Equals("lastServer", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(v))
                            continue;

                        dict[k] = v;
                    }
                    existingLines.Add(line);
                }
            }

            SetOption(existingLines, "gamma", "1.0");
            SetOption(existingLines, "enableVsync", "false");
            SetOption(existingLines, "maxFps", "260");
            SetOption(existingLines, "graphicsMode", "0");
            SetOption(existingLines, "renderClouds", isLegacy112 ? "false" : "\"false\"");
            SetOption(existingLines, "entityShadows", "false");
            SetOption(existingLines, "biomeBlendRadius", "0");
            if (!dict.ContainsKey("renderDistance"))
                SetOption(existingLines, "renderDistance", "8");
            if (!dict.ContainsKey("simulationDistance"))
                SetOption(existingLines, "simulationDistance", "6");

            File.WriteAllLines(optionsPath, existingLines);
            log?.Invoke("[FPS Boost] Okul bilgisayarı için yüksek FPS ayarları (Fast Graphics, VSync Kapalı, Düşük Gölge) options.txt'ye uygulandı.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[FPS Boost Uyarı] options.txt güncellenemedi: {ex.Message}");
        }
    }

    private static void SetOption(List<string> lines, string key, string value)
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

    private static readonly string[] CoreSoundPrefixes =
    [
        "minecraft/sounds/ui/",
        "minecraft/sounds/random/",
        "minecraft/sounds/damage/",
        "minecraft/sounds/step/",
        "minecraft/sounds/dig/",
        "minecraft/sounds/fire/",
        "minecraft/sounds/liquid/",
        "minecraft/sounds/note/",
        "minecraft/sounds/item/",
        "minecraft/sounds/portal/"
    ];

    private static bool ShouldIncludeEssentialOrCoreAsset(string name)
    {
        if (!name.StartsWith("minecraft/sounds/", StringComparison.OrdinalIgnoreCase))
        {
            if (!name.Contains("/lang/", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/tr_tr.json", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/en_us.json", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/tr_tr.lang", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/en_us.lang", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        foreach (string pref in CoreSoundPrefixes)
        {
            if (name.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static async Task EnsureLocalAssetsAndSanitizeIndexAsync(string mcDir, string assetIndex, Action<string>? log = null)
    {
        string indexesDir = Path.Combine(mcDir, "assets", "indexes");
        string objectsDir = Path.Combine(mcDir, "assets", "objects");
        Directory.CreateDirectory(indexesDir);
        Directory.CreateDirectory(objectsDir);

        string activeIndexPath = Path.Combine(indexesDir, $"{assetIndex}.json");
        string fullIndexPath = Path.Combine(indexesDir, $"{assetIndex}.full.json");

        if (!File.Exists(fullIndexPath) && File.Exists(activeIndexPath))
        {
            File.Copy(activeIndexPath, fullIndexPath, overwrite: true);
        }

        string sourceIndexPath = File.Exists(fullIndexPath) ? fullIndexPath : activeIndexPath;
        if (!File.Exists(sourceIndexPath))
            return;

        string sysObjectsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft", "assets", "objects");

        var missingHashes = new List<string>();
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sourceIndexPath)))
        {
            if (doc.RootElement.TryGetProperty("objects", out var objs))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in objs.EnumerateObject())
                {
                    if (!ShouldIncludeEssentialOrCoreAsset(prop.Name))
                        continue;

                    string? hash = prop.Value.TryGetProperty("hash", out var hProp) ? hProp.GetString() : null;
                    if (string.IsNullOrWhiteSpace(hash) || hash.Length < 2 || !seen.Add(hash))
                        continue;

                    string prefix = hash[..2];
                    string localObjPath = Path.Combine(objectsDir, prefix, hash);
                    if (File.Exists(localObjPath) && new FileInfo(localObjPath).Length > 0)
                        continue;

                    string sysObjPath = Path.Combine(sysObjectsDir, prefix, hash);
                    if (File.Exists(sysObjPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localObjPath)!);
                        File.Copy(sysObjPath, localObjPath, overwrite: true);
                        continue;
                    }

                    missingHashes.Add(hash);
                }
            }
        }

        if (missingHashes.Count > 0)
        {
            log?.Invoke($"[Varlık Tamamlayıcı] {assetIndex} için {missingHashes.Count} temel doku/font/ses varlığı indiriliyor...");
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var sem = new SemaphoreSlim(24);
            var tasks = missingHashes.Select(async hash =>
            {
                await sem.WaitAsync();
                try
                {
                    string prefix = hash[..2];
                    string dest = Path.Combine(objectsDir, prefix, hash);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    byte[] bytes = await http.GetByteArrayAsync($"https://resources.download.minecraft.net/{prefix}/{hash}");
                    await File.WriteAllBytesAsync(dest, bytes);
                }
                catch
                {
                    // Offline or unreachable; sanitizer below will exclude missing hashes so LinkFileSystem never crashes
                }
                finally
                {
                    sem.Release();
                }
            });
            await Task.WhenAll(tasks);
        }

        // Write active index containing ONLY objects physically present in assets/objects so Minecraft 1.21+ LinkFileSystem never throws NoSuchFileException
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(sourceIndexPath)))
        await using (var ms = new MemoryStream())
        {
            await using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var topProp in doc.RootElement.EnumerateObject())
                {
                    if (topProp.NameEquals("objects"))
                    {
                        writer.WritePropertyName("objects");
                        writer.WriteStartObject();
                        foreach (var objProp in topProp.Value.EnumerateObject())
                        {
                            string? h = objProp.Value.TryGetProperty("hash", out var hp) ? hp.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(h) && h.Length >= 2)
                            {
                                string p = Path.Combine(objectsDir, h[..2], h);
                                if (File.Exists(p) && new FileInfo(p).Length > 0)
                                {
                                    writer.WritePropertyName(objProp.Name);
                                    objProp.Value.WriteTo(writer);
                                }
                            }
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        topProp.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }
            await File.WriteAllBytesAsync(activeIndexPath, ms.ToArray());
        }
    }

    private static async Task EnsureMissingLibrariesDownloadedAsync(JsonElement rootElem, string librariesDir, Action<string> log)
    {
        if (!rootElem.TryGetProperty("libraries", out var libsElem) || libsElem.ValueKind != JsonValueKind.Array)
            return;

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };

        foreach (var lib in libsElem.EnumerateArray())
        {
            if (!IsLibraryAllowedOnWindows(lib))
                continue;

            // 1. Standard artifact
            string? relPath = null;
            string? dlUrl = null;

            if (lib.TryGetProperty("downloads", out var dlElem) &&
                dlElem.TryGetProperty("artifact", out var artElem))
            {
                if (artElem.TryGetProperty("path", out var pElem)) relPath = pElem.GetString();
                if (artElem.TryGetProperty("url", out var uElem)) dlUrl = uElem.GetString();
            }

            if (string.IsNullOrWhiteSpace(relPath) && lib.TryGetProperty("name", out var nameElem))
            {
                relPath = MavenNameToRelativePath(nameElem.GetString() ?? "");
                string baseUrl = lib.TryGetProperty("url", out var baseElem)
                    ? (baseElem.GetString() ?? "https://maven.fabricmc.net/")
                    : "https://libraries.minecraft.net/";
                if (relPath != null)
                    dlUrl = baseUrl.TrimEnd('/') + "/" + relPath;
            }

            if (!string.IsNullOrWhiteSpace(relPath) && !string.IsNullOrWhiteSpace(dlUrl))
            {
                string fullDest = Path.Combine(librariesDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullDest))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(fullDest)!);
                        log($"[Kütüphane Tamamlayıcı] Eksik dosya indiriliyor: {Path.GetFileName(fullDest)}");
                        using var resp = await http.GetAsync(dlUrl);
                        resp.EnsureSuccessStatusCode();
                        await using var fs = new FileStream(fullDest, FileMode.Create, FileAccess.Write, FileShare.None);
                        await resp.Content.CopyToAsync(fs);
                    }
                    catch (Exception ex)
                    {
                        log($"[Kütüphane Uyarı] {Path.GetFileName(fullDest)}: {ex.Message}");
                    }
                }
            }

            // 2. Classifiers (natives-windows)
            if (lib.TryGetProperty("downloads", out var dl2) &&
                dl2.TryGetProperty("classifiers", out var classElem))
            {
                string[] winKeys = ["natives-windows", "natives-windows-64", "natives-windows-x86_64"];
                foreach (string k in winKeys)
                {
                    if (classElem.TryGetProperty(k, out var natElem))
                    {
                        string? natPath = natElem.TryGetProperty("path", out var np) ? np.GetString() : null;
                        string? natUrl = natElem.TryGetProperty("url", out var nu) ? nu.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(natPath) && !string.IsNullOrWhiteSpace(natUrl))
                        {
                            string fullNat = Path.Combine(librariesDir, natPath.Replace('/', Path.DirectorySeparatorChar));
                            if (!File.Exists(fullNat))
                            {
                                try
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(fullNat)!);
                                    using var resp = await http.GetAsync(natUrl);
                                    resp.EnsureSuccessStatusCode();
                                    await using var fs = new FileStream(fullNat, FileMode.Create, FileAccess.Write, FileShare.None);
                                    await resp.Content.CopyToAsync(fs);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }
    }

    public static async Task<Process?> LaunchJavaEditionAsync(
        LauncherConfig config,
        string versionId,
        Action<string> log,
        string? directConnectServer = null,
        int? directConnectPort = null)
    {
        string mcDir = config.GetMinecraftDir();
        string versionsDir = Path.Combine(mcDir, "versions");
        string versionDir = Path.Combine(versionsDir, versionId);
        string jsonPath = Path.Combine(versionDir, $"{versionId}.json");

        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException(
                $"Seçilen sürüm dosyası bulunamadı: {jsonPath}\nLütfen '☁️ GITHUB & KURULUM' sekmesinden sürüm paketini indirin.");
        }

        OfflineSkinService.ApplyOfflineSkinToJava(config, log);
        if (config.AutoApplyFpsOptions)
            ApplyFpsBoosterToOptionsTxt(mcDir, versionId, log);

        using var childDoc = JsonDocument.Parse(await File.ReadAllTextAsync(jsonPath));
        var childRoot = childDoc.RootElement;

        JsonDocument? parentDoc = null;
        JsonElement? parentRoot = null;

        string? inheritsFrom = childRoot.TryGetProperty("inheritsFrom", out var inhProp) ? inhProp.GetString() : null;
        if (!string.IsNullOrWhiteSpace(inheritsFrom))
        {
            string parentJsonPath = Path.Combine(versionsDir, inheritsFrom, $"{inheritsFrom}.json");
            if (!File.Exists(parentJsonPath))
                throw new FileNotFoundException($"Modlu profilin taban sürümü ('{inheritsFrom}') bulunamadı: {parentJsonPath}");

            parentDoc = JsonDocument.Parse(await File.ReadAllTextAsync(parentJsonPath));
            parentRoot = parentDoc.RootElement;
            log($"[Motor] Modlu profil algılandı: '{versionId}' -> Taban sürüm: '{inheritsFrom}'");
        }

        try
        {
            // Determine required Java version
            int javaMajor = InferJavaVersionFromId(inheritsFrom ?? versionId);
            if (childRoot.TryGetProperty("javaVersion", out var jv) && jv.TryGetProperty("majorVersion", out var mv))
                javaMajor = mv.GetInt32();
            else if (parentRoot.HasValue && parentRoot.Value.TryGetProperty("javaVersion", out var pjv) && pjv.TryGetProperty("majorVersion", out var pmv))
                javaMajor = pmv.GetInt32();

            string? javaExe = FindJavaExecutable(config, javaMajor, log);
            if (string.IsNullOrWhiteSpace(javaExe) || !File.Exists(javaExe))
            {
                log($"[Portable Java] 'game_data/runtime/java-{javaMajor}' bulunamadı, kendi Portable Java {javaMajor} çalışma zamanımız indiriliyor...");
                await GitHubReleaseService.DownloadPortableJavaJreAsync(javaMajor, config, (_, _) => { }, log);
                javaExe = FindJavaExecutable(config, javaMajor, log);
            }

            if (string.IsNullOrWhiteSpace(javaExe) || !File.Exists(javaExe))
            {
                throw new FileNotFoundException($"Portable Java {javaMajor} ('game_data/runtime/java-{javaMajor}') bulunamadı!");
            }

            log($"[Motor] Kullanılan Portable Java: {javaExe}");

            // Determine mainClass
            string mainClass = "net.minecraft.client.main.Main";
            if (childRoot.TryGetProperty("mainClass", out var mcProp) && !string.IsNullOrWhiteSpace(mcProp.GetString()))
                mainClass = mcProp.GetString()!;
            else if (parentRoot.HasValue && parentRoot.Value.TryGetProperty("mainClass", out var pmcProp) && !string.IsNullOrWhiteSpace(pmcProp.GetString()))
                mainClass = pmcProp.GetString()!;

            // Determine assetIndex
            string assetIndex = inheritsFrom ?? versionId;
            if (childRoot.TryGetProperty("assetIndex", out var aiProp) && aiProp.TryGetProperty("id", out var aiId))
                assetIndex = aiId.GetString() ?? assetIndex;
            else if (parentRoot.HasValue && parentRoot.Value.TryGetProperty("assetIndex", out var paiProp) && paiProp.TryGetProperty("id", out var paiId))
                assetIndex = paiId.GetString() ?? assetIndex;
            else if (childRoot.TryGetProperty("assets", out var aProp))
                assetIndex = aProp.GetString() ?? assetIndex;
            else if (parentRoot.HasValue && parentRoot.Value.TryGetProperty("assets", out var paProp))
                assetIndex = paProp.GetString() ?? assetIndex;

            // Collect libraries & extract natives
            string nativesDir = Path.Combine(versionDir, "natives");
            Directory.CreateDirectory(nativesDir);

            string librariesDir = Path.Combine(mcDir, "libraries");
            string sysMcDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
            string sysLibrariesDir = Path.Combine(sysMcDir, "libraries");

            // Ensure all required libraries & local asset objects exist locally
            await EnsureMissingLibrariesDownloadedAsync(childRoot, librariesDir, log);
            if (parentRoot.HasValue)
                await EnsureMissingLibrariesDownloadedAsync(parentRoot.Value, librariesDir, log);

            await EnsureLocalAssetsAndSanitizeIndexAsync(mcDir, assetIndex, log);

            var classPathList = new List<string>();
            var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addedGroupArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void ProcessLibrariesFromRoot(JsonElement rootElem)
            {
                if (!rootElem.TryGetProperty("libraries", out var libsElem) || libsElem.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var lib in libsElem.EnumerateArray())
                {
                    if (!IsLibraryAllowedOnWindows(lib))
                        continue;

                    string? mavenName = lib.TryGetProperty("name", out var nameElem) ? nameElem.GetString() : null;
                    string? groupArtifactKey = null;
                    if (!string.IsNullOrWhiteSpace(mavenName))
                    {
                        string[] gp = mavenName.Split(':');
                        if (gp.Length >= 2)
                            groupArtifactKey = gp.Length >= 4 ? $"{gp[0]}:{gp[1]}:{gp[3]}" : $"{gp[0]}:{gp[1]}";
                    }

                    string? relPath = null;
                    if (lib.TryGetProperty("downloads", out var dlElem) &&
                        dlElem.TryGetProperty("artifact", out var artElem) &&
                        artElem.TryGetProperty("path", out var pathElem))
                    {
                        relPath = pathElem.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(relPath) && !string.IsNullOrWhiteSpace(mavenName))
                    {
                        relPath = MavenNameToRelativePath(mavenName);
                    }

                    if (!string.IsNullOrWhiteSpace(relPath))
                    {
                        string normRel = relPath.Replace('/', Path.DirectorySeparatorChar);
                        string fullLibPath = Path.Combine(librariesDir, normRel);
                        if (!File.Exists(fullLibPath))
                        {
                            string fallbackLibPath = Path.Combine(sysLibrariesDir, normRel);
                            if (File.Exists(fallbackLibPath))
                                fullLibPath = fallbackLibPath;
                        }

                        if (File.Exists(fullLibPath))
                        {
                            if ((groupArtifactKey == null || addedGroupArtifacts.Add(groupArtifactKey)) &&
                                addedPaths.Add(fullLibPath))
                            {
                                classPathList.Add(fullLibPath);
                            }
                        }
                    }

                    ExtractNativesIfPresent(lib, librariesDir, nativesDir, log);
                    if (Directory.Exists(sysLibrariesDir))
                        ExtractNativesIfPresent(lib, sysLibrariesDir, nativesDir, log);
                }
            }

            ProcessLibrariesFromRoot(childRoot);
            if (parentRoot.HasValue)
                ProcessLibrariesFromRoot(parentRoot.Value);

            string clientJar = Path.Combine(versionDir, $"{versionId}.jar");
            if (!File.Exists(clientJar) && !string.IsNullOrWhiteSpace(inheritsFrom))
                clientJar = Path.Combine(versionsDir, inheritsFrom, $"{inheritsFrom}.jar");
            if (!File.Exists(clientJar) && !string.IsNullOrWhiteSpace(inheritsFrom))
                clientJar = Path.Combine(sysMcDir, "versions", inheritsFrom, $"{inheritsFrom}.jar");

            if (!File.Exists(clientJar))
                throw new FileNotFoundException($"Minecraft istemci JAR dosyası bulunamadı: {clientJar}");

            classPathList.Add(clientJar);
            string classPathString = string.Join(Path.PathSeparator, classPathList);

            string cleanUsername = OfflineAuthService.SanitizeUsername(config.Username);
            string offlineUuid = OfflineAuthService.GenerateOfflineUuid(cleanUsername, dashed: true);
            log($"[Offline Auth] Kullanıcı: {cleanUsername} | Cracked UUID: {offlineUuid}");

            var allArgs = new List<string>();

            allArgs.Add($"-Xms{Math.Min(config.MinRamMb, config.AllocatedRamMb)}M");
            allArgs.Add($"-Xmx{config.AllocatedRamMb}M");

            string optFlags = BuildJvmOptimizationArgs(config);
            if (!string.IsNullOrWhiteSpace(optFlags))
            {
                foreach (string token in optFlags.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    allArgs.Add(token);
            }

            if (config.FullOfflineIsolation)
            {
                allArgs.Add("-Dminecraft.api.env=custom");
                allArgs.Add("-Dminecraft.api.auth.host=http://127.0.0.1");
                allArgs.Add("-Dminecraft.api.account.host=http://127.0.0.1");
                allArgs.Add("-Dminecraft.api.session.host=http://127.0.0.1");
                allArgs.Add("-Dminecraft.api.services.host=http://127.0.0.1");
                allArgs.Add("-Dminecraft.api.profiles.host=http://127.0.0.1");
            }

            if (mainClass.Contains("fabricmc", StringComparison.OrdinalIgnoreCase))
            {
                allArgs.Add("-DFabricMcEmu=net.minecraft.client.main.Main");
            }

            allArgs.Add($"-Djava.library.path={nativesDir}");
            allArgs.Add($"-Djna.tmpdir={nativesDir}");
            allArgs.Add($"-Dorg.lwjgl.system.SharedLibraryExtractPath={nativesDir}");
            allArgs.Add($"-Dio.netty.native.workdir={nativesDir}");
            allArgs.Add("-Dminecraft.launcher.brand=Shinrai-Lunar");
            allArgs.Add("-Dminecraft.launcher.version=2.0.0");
            allArgs.Add("-cp");
            allArgs.Add(classPathString);

            // Main Class
            allArgs.Add(mainClass);

            // Game Arguments
            string assetsDir = Path.Combine(mcDir, "assets");
            if (!File.Exists(Path.Combine(assetsDir, "indexes", $"{assetIndex}.json")) &&
                File.Exists(Path.Combine(sysMcDir, "assets", "indexes", $"{assetIndex}.json")))
            {
                assetsDir = Path.Combine(sysMcDir, "assets");
            }

            var gameArgs = BuildGameArguments(
                childRoot,
                parentRoot,
                cleanUsername,
                versionId,
                mcDir,
                assetsDir,
                assetIndex,
                offlineUuid);

            allArgs.AddRange(gameArgs);

            // Optional Direct Connect to School LAN Server
            if (!string.IsNullOrWhiteSpace(directConnectServer))
            {
                int port = directConnectPort ?? 25565;
                allArgs.Add("--server");
                allArgs.Add(directConnectServer);
                allArgs.Add("--port");
                allArgs.Add(port.ToString());
                log($"[LAN Bağlantı] Doğrudan sunucuya bağlanılıyor: {directConnectServer}:{port}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = javaExe,
                WorkingDirectory = mcDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string arg in allArgs)
                psi.ArgumentList.Add(arg);

            log($"[Motor] {versionId} tamamen local modda başlatılıyor ({classPathList.Count} kütüphane yüklendi)...");
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    log($"[MC] {e.Data}");
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    log($"[MC-ERR] {e.Data}");
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            return proc;
        }
        finally
        {
            parentDoc?.Dispose();
        }
    }

    private static List<string> BuildGameArguments(
        JsonElement childRoot,
        JsonElement? parentRoot,
        string username,
        string versionId,
        string gameDir,
        string assetsDir,
        string assetIndex,
        string uuid)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["${auth_player_name}"] = username,
            ["${version_name}"] = versionId,
            ["${game_directory}"] = gameDir,
            ["${assets_root}"] = assetsDir,
            ["${game_assets}"] = assetsDir,
            ["${assets_index_name}"] = assetIndex,
            ["${auth_uuid}"] = uuid,
            ["${auth_access_token}"] = "0",
            ["${auth_session}"] = "0",
            ["${clientid}"] = "0",
            ["${auth_xuid}"] = "0",
            ["${user_type}"] = "legacy",
            ["${version_type}"] = "Shinrai-Local",
            ["${user_properties}"] = "{}"
        };

        string ReplacePlaceholders(string raw)
        {
            foreach (var kv in replacements)
                raw = raw.Replace(kv.Key, kv.Value);
            return raw;
        }

        var result = new List<string>();

        // Legacy minecraftArguments string (1.7 - 1.12.2)
        if (childRoot.TryGetProperty("minecraftArguments", out var legacyProp) && !string.IsNullOrWhiteSpace(legacyProp.GetString()))
        {
            foreach (string part in legacyProp.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                result.Add(ReplacePlaceholders(part));
            return result;
        }
        if (parentRoot.HasValue && parentRoot.Value.TryGetProperty("minecraftArguments", out var pLegacyProp) && !string.IsNullOrWhiteSpace(pLegacyProp.GetString()))
        {
            foreach (string part in pLegacyProp.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                result.Add(ReplacePlaceholders(part));
            return result;
        }

        // Modern arguments.game array (1.13 - 1.21+)
        void AppendGameArgsFromRoot(JsonElement root)
        {
            if (!root.TryGetProperty("arguments", out var argsObj) ||
                !argsObj.TryGetProperty("game", out var gameArr) ||
                gameArr.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in gameArr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string val = item.GetString() ?? "";
                    if (!val.StartsWith("${quickPlay"))
                        result.Add(ReplacePlaceholders(val));
                }
            }
        }

        if (parentRoot.HasValue)
            AppendGameArgsFromRoot(parentRoot.Value);
        AppendGameArgsFromRoot(childRoot);

        // Fallback if arguments array was empty
        if (result.Count == 0)
        {
            result.AddRange([
                "--username", username,
                "--version", versionId,
                "--gameDir", gameDir,
                "--assetsDir", assetsDir,
                "--assetIndex", assetIndex,
                "--uuid", uuid,
                "--accessToken", "0",
                "--userType", "legacy",
                "--versionType", "Shinrai-Local"
            ]);
        }

        return result;
    }

    private static bool IsLibraryAllowedOnWindows(JsonElement lib)
    {
        if (lib.TryGetProperty("name", out var nElem))
        {
            string n = nElem.GetString() ?? "";
            if (n.Contains("natives-windows-arm64", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("natives-windows-x86", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("natives-macos", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("natives-linux", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!lib.TryGetProperty("rules", out var rulesArr) || rulesArr.ValueKind != JsonValueKind.Array)
            return true;

        bool allowed = false;
        foreach (var rule in rulesArr.EnumerateArray())
        {
            string action = rule.TryGetProperty("action", out var actProp) ? (actProp.GetString() ?? "allow") : "allow";
            bool matchesOs = true;

            if (rule.TryGetProperty("os", out var osProp) && osProp.TryGetProperty("name", out var nameProp))
            {
                string osName = nameProp.GetString() ?? "";
                matchesOs = osName.Equals("windows", StringComparison.OrdinalIgnoreCase);
            }

            if (matchesOs)
                allowed = action.Equals("allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    private static string? MavenNameToRelativePath(string mavenCoords)
    {
        // Format: group:artifact:version[:classifier]
        string[] parts = mavenCoords.Split(':');
        if (parts.Length < 3)
            return null;

        string groupPath = parts[0].Replace('.', '/');
        string artifact = parts[1];
        string version = parts[2];
        string classifier = parts.Length >= 4 ? $"-{parts[3]}" : "";

        return $"{groupPath}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
    }

    private static void ExtractNativesIfPresent(JsonElement lib, string librariesDir, string nativesDir, Action<string> log)
    {
        try
        {
            string? nativeRelPath = null;

            // 1. Check downloads.classifiers["natives-windows"] or ["natives-windows-64"]
            if (lib.TryGetProperty("downloads", out var dlElem) &&
                dlElem.TryGetProperty("classifiers", out var classElem))
            {
                string[] winKeys = ["natives-windows", "natives-windows-64", "natives-windows-x86_64"];
                foreach (string key in winKeys)
                {
                    if (classElem.TryGetProperty(key, out var winClassifier) &&
                        winClassifier.TryGetProperty("path", out var pElem))
                    {
                        nativeRelPath = pElem.GetString();
                        break;
                    }
                }
            }

            // 2. Modern 1.19+ LWJGL native artifacts have ":natives-windows" directly in artifact path
            if (nativeRelPath == null &&
                lib.TryGetProperty("downloads", out var dl2) &&
                dl2.TryGetProperty("artifact", out var art2) &&
                art2.TryGetProperty("path", out var p2))
            {
                string? pStr = p2.GetString();
                if (pStr != null && pStr.Contains("natives-windows", StringComparison.OrdinalIgnoreCase))
                    nativeRelPath = pStr;
            }

            if (string.IsNullOrWhiteSpace(nativeRelPath))
                return;

            string fullJarPath = Path.Combine(librariesDir, nativeRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullJarPath))
                return;

            using var archive = ZipFile.OpenRead(fullJarPath);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string destFile = Path.Combine(nativesDir, Path.GetFileName(entry.FullName));
                    if (!File.Exists(destFile))
                        entry.ExtractToFile(destFile, overwrite: true);
                }
            }
        }
        catch
        {
            // Ignore locked native DLLs if another instance is open
        }
    }
}
