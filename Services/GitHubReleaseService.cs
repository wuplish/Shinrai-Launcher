using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public class GitHubReleaseAssetInfo
{
    public string Name { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SizeFormatted => $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
    public override string ToString() => $"{Name} ({SizeFormatted})";
}

public static class GitHubReleaseService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ShinraiMinecraftLauncher/1.0");
        return client;
    }

    /// <summary>
    /// Kullanıcının GitHub deposundaki (örn: kullanici/shinrai-launcher) son Release içindeki .zip paketlerini listeler.
    /// </summary>
    public static async Task<List<GitHubReleaseAssetInfo>> FetchReleaseAssetsAsync(string repoSlug, string tag = "latest")
    {
        var assets = new List<GitHubReleaseAssetInfo>();
        string cleanSlug = repoSlug.Trim().Trim('/');
        if (cleanSlug.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            cleanSlug = cleanSlug["https://github.com/".Length..].Trim('/');
        }

        string apiUrl = string.IsNullOrWhiteSpace(tag) || tag.Equals("latest", StringComparison.OrdinalIgnoreCase)
            ? $"https://api.github.com/repos/{cleanSlug}/releases/latest"
            : $"https://api.github.com/repos/{cleanSlug}/releases/tags/{tag}";

        using var response = await Http.GetAsync(apiUrl);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("assets", out var assetsArr) && assetsArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in assetsArr.EnumerateArray())
            {
                string name = item.TryGetProperty("name", out var nProp) ? (nProp.GetString() ?? "") : "";
                string url = item.TryGetProperty("browser_download_url", out var uProp) ? (uProp.GetString() ?? "") : "";
                long size = item.TryGetProperty("size", out var sProp) ? sProp.GetInt64() : 0;

                if (!string.IsNullOrWhiteSpace(url))
                {
                    assets.Add(new GitHubReleaseAssetInfo
                    {
                        Name = name,
                        DownloadUrl = url,
                        SizeBytes = size
                    });
                }
            }
        }

        return assets;
    }

    /// <summary>
    /// Doğrudan GitHub Release URL'sinden (API limitine takılmadan) veya seçilen Asset URL'sinden ZIP indirip game_data içine açar.
    /// </summary>
    public static async Task DownloadAndExtractPackageAsync(
        string downloadUrl,
        Action<double, string> progressCallback,
        Action<string> log,
        CancellationToken ct = default)
    {
        string rootDir = LauncherConfig.GetPortableRootDir();
        string gameDataDir = Path.Combine(rootDir, "game_data");
        Directory.CreateDirectory(gameDataDir);

        string tempZip = Path.Combine(rootDir, $"shinrai_download_{Guid.NewGuid():N}.zip");
        try
        {
            log($"[GitHub İndirici] Paket indiriliyor: {downloadUrl}");
            progressCallback(2, "GitHub sunucusuna bağlanılıyor...");

            using (var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                await using var netStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

                byte[] buffer = new byte[65536];
                long readTotal = 0;
                int read;
                while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    readTotal += read;

                    if (totalBytes > 0)
                    {
                        double pct = (double)readTotal / totalBytes * 85.0;
                        double mbRead = readTotal / (1024.0 * 1024.0);
                        double mbTotal = totalBytes / (1024.0 * 1024.0);
                        progressCallback(pct, $"İndiriliyor: {mbRead:F1} MB / {mbTotal:F1} MB");
                    }
                    else
                    {
                        double mbRead = readTotal / (1024.0 * 1024.0);
                        progressCallback(45, $"İndiriliyor: {mbRead:F1} MB...");
                    }
                }
            }

            progressCallback(88, "Arşiv 'game_data' klasörüne çıkartılıyor...");
            log("[GitHub İndirici] İndirme tamamlandı. Dosyalar çıkartılıyor...");

            await InstallFromLocalZipAsync(tempZip, progressCallback, log);
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    public static async Task InstallFromLocalZipAsync(
        string zipFilePath,
        Action<double, string> progressCallback,
        Action<string> log)
    {
        string rootDir = LauncherConfig.GetPortableRootDir();
        string gameDataDir = Path.Combine(rootDir, "game_data");
        Directory.CreateDirectory(gameDataDir);

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            int total = Math.Max(1, archive.Entries.Count);
            int current = 0;

            // Detect GitHub repo branch archive (e.g., "Shinrai-Launcher-main/game_data/...")
            bool isRepoBranchZip = archive.Entries.Any(e =>
                e.FullName.Contains("/game_data/", StringComparison.OrdinalIgnoreCase) ||
                e.FullName.Contains("\\game_data\\", StringComparison.OrdinalIgnoreCase));

            // Check if archive already has top-level "game_data/" prefix
            bool hasGameDataPrefix = archive.Entries.Any(e =>
                e.FullName.StartsWith("game_data/", StringComparison.OrdinalIgnoreCase) ||
                e.FullName.StartsWith("game_data\\", StringComparison.OrdinalIgnoreCase));

            string baseTargetDir = (isRepoBranchZip || hasGameDataPrefix) ? rootDir : gameDataDir;

            foreach (var entry in archive.Entries)
            {
                current++;
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                string relEntryPath = entry.FullName.Replace('\\', '/');
                if (isRepoBranchZip)
                {
                    int gdIdx = relEntryPath.IndexOf("/game_data/", StringComparison.OrdinalIgnoreCase);
                    if (gdIdx < 0)
                        continue; // Only extract game_data/ contents when pulling from full repo zip
                    relEntryPath = relEntryPath[(gdIdx + 1)..]; // Keep "game_data/..."
                }

                string destPath = Path.GetFullPath(Path.Combine(baseTargetDir, relEntryPath));
                string? destFolder = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destFolder))
                    Directory.CreateDirectory(destFolder);

                entry.ExtractToFile(destPath, overwrite: true);

                if (current % 40 == 0 || current == total)
                {
                    double pct = 88.0 + ((double)current / total * 12.0);
                    progressCallback(pct, $"Çıkartılıyor ({current}/{total}): {entry.Name}");
                }
            }
        });

        progressCallback(100, "Kurulum tamamlandı! Artık %100 local ve internetsiz oynayabilirsiniz.");
        log("[Paket Kurucu] Tüm oyun dosyaları başarıyla 'game_data' içine kuruldu!");
    }

    /// <summary>
    /// Evde hazırlanan './game_data' klasörünü GitHub Releases'a yüklemeye hazır ZIP paketine dönüştürür.
    /// </summary>
    public static async Task<string> CreateGitHubReleaseBundleAsync(
        Action<double, string> progressCallback,
        Action<string> log)
    {
        string rootDir = LauncherConfig.GetPortableRootDir();
        string gameDataDir = Path.Combine(rootDir, "game_data");
        string outputDir = Path.Combine(rootDir, "Release_Output");
        Directory.CreateDirectory(outputDir);

        string bundlePath = Path.Combine(outputDir, "shinrai-game-bundle.zip");
        if (File.Exists(bundlePath))
            File.Delete(bundlePath);

        log($"[Paketleyici] 'game_data' klasörü GitHub Release için sıkıştırılıyor: {bundlePath}");
        progressCallback(10, "Dosyalar taranıyor...");

        await Task.Run(() =>
        {
            var allFiles = Directory.GetFiles(gameDataDir, "*", SearchOption.AllDirectories);
            int total = Math.Max(1, allFiles.Length);
            int count = 0;

            using var fs = new FileStream(bundlePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (string file in allFiles)
            {
                count++;
                string relPath = Path.GetRelativePath(gameDataDir, file);
                archive.CreateEntryFromFile(file, relPath, CompressionLevel.Optimal);

                if (count % 25 == 0 || count == total)
                {
                    double pct = 10.0 + ((double)count / total * 90.0);
                    progressCallback(pct, $"Paketleniyor ({count}/{total}): {Path.GetFileName(file)}");
                }
            }
        });

        long sizeMb = new FileInfo(bundlePath).Length / (1024 * 1024);
        log($"[Paketleyici] GitHub Release paketi hazır ({sizeMb} MB): {bundlePath}");
        progressCallback(100, $"Paket hazır! ({sizeMb} MB) -> Release_Output/shinrai-game-bundle.zip");
        return bundlePath;
    }

    /// <summary>
    /// Evdeki bilgisayarda zaten kurulu olan (%AppData%\.minecraft veya TL Legacy) sürümlerini tespit eder.
    /// </summary>
    public static List<(string VersionId, string SourceMcDir)> ScanSystemMinecraftVersions()
    {
        var results = new List<(string VersionId, string SourceMcDir)>();
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        string[] candidateMcDirs =
        [
            Path.Combine(appData, ".minecraft"),
            Path.Combine(appData, ".tlauncher", "legacy", "Minecraft", "game")
        ];

        foreach (string mcDir in candidateMcDirs)
        {
            string verDir = Path.Combine(mcDir, "versions");
            if (!Directory.Exists(verDir)) continue;

            foreach (string dir in Directory.GetDirectories(verDir))
            {
                string id = Path.GetFileName(dir);
                if (File.Exists(Path.Combine(dir, $"{id}.json")))
                {
                    results.Add((id, mcDir));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Evdeki bilgisayarda kurulu bir sürümü (kütüphaneleri, varlıkları ve varsa Java runtime ile birlikte)
    /// Shinrai'nin taşınabilir 'game_data/.minecraft' klasörüne kopyalar.
    /// </summary>
    public static async Task ImportVersionFromSystemAsync(
        string versionId,
        string sourceMcDir,
        LauncherConfig config,
        Action<double, string> progress,
        Action<string> log)
    {
        string targetMcDir = config.GetMinecraftDir();

        await Task.Run(() =>
        {
            log($"[İçe Aktarıcı] '{versionId}' sürümü taşınabilir klasöre kopyalanıyor...");
            progress(5, $"'{versionId}' sürüm dosyaları kopyalanıyor...");

            // 1. Copy version folder
            string srcVerDir = Path.Combine(sourceMcDir, "versions", versionId);
            string dstVerDir = Path.Combine(targetMcDir, "versions", versionId);
            CopyDirectoryRecursive(srcVerDir, dstVerDir);

            // Check if it inheritsFrom a base version (e.g. Fabric / OptiFine) and copy base version too!
            string jsonPath = Path.Combine(dstVerDir, $"{versionId}.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
            if (doc.RootElement.TryGetProperty("inheritsFrom", out var inhProp) && !string.IsNullOrWhiteSpace(inhProp.GetString()))
            {
                string parentId = inhProp.GetString()!;
                string srcParentDir = Path.Combine(sourceMcDir, "versions", parentId);
                string dstParentDir = Path.Combine(targetMcDir, "versions", parentId);
                if (Directory.Exists(srcParentDir))
                {
                    log($"[İçe Aktarıcı] Taban sürüm '{parentId}' kopyalanıyor...");
                    CopyDirectoryRecursive(srcParentDir, dstParentDir);
                }
            }

            // 2. Copy libraries
            progress(25, "Kütüphaneler (libraries) taşınabilir klasöre aktarılıyor...");
            string srcLibDir = Path.Combine(sourceMcDir, "libraries");
            string dstLibDir = Path.Combine(targetMcDir, "libraries");
            if (Directory.Exists(srcLibDir))
                CopyDirectoryRecursive(srcLibDir, dstLibDir);

            // 3. Copy assets
            progress(65, "Oyun ses ve doku varlıkları (assets) aktarılıyor...");
            string srcAssetsDir = Path.Combine(sourceMcDir, "assets");
            string dstAssetsDir = Path.Combine(targetMcDir, "assets");
            if (Directory.Exists(srcAssetsDir))
                CopyDirectoryRecursive(srcAssetsDir, dstAssetsDir);

            // 4. Copy mods if present
            string srcModsDir = Path.Combine(sourceMcDir, "mods");
            string dstModsDir = Path.Combine(targetMcDir, "mods");
            if (Directory.Exists(srcModsDir))
                CopyDirectoryRecursive(srcModsDir, dstModsDir);

            progress(100, $"'{versionId}' tamamen taşınabilir hale getirildi!");
            log($"[İçe Aktarıcı] '{versionId}' ve tüm bağımlılıkları 'game_data/.minecraft' içine kopyalandı.");
        });
    }

    /// <summary>
    /// Evde hazırlık aşamasında Mojang'dan temiz bir Vanilla sürümü (Client JAR + Libraries + Natives) doğrudan portable klasöre indirir.
    /// </summary>
    public static async Task DownloadVanillaVersionForOfflineUseAsync(
        string targetVersionId,
        LauncherConfig config,
        Action<double, string> progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        string mcDir = config.GetMinecraftDir();
        progress(2, "Sürüm manifestosu alınıyor...");
        log($"[Ev Hazırlık İndiricisi] '{targetVersionId}' sürümü tamamen local kullanım için indiriliyor...");

        string manifestJson = await Http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json", ct);
        using var manifestDoc = JsonDocument.Parse(manifestJson);
        string? versionUrl = null;

        foreach (var v in manifestDoc.RootElement.GetProperty("versions").EnumerateArray())
        {
            if (string.Equals(v.GetProperty("id").GetString(), targetVersionId, StringComparison.OrdinalIgnoreCase))
            {
                versionUrl = v.GetProperty("url").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(versionUrl))
            throw new InvalidOperationException($"Sürüm '{targetVersionId}' resmi listede bulunamadı.");

        string verDir = Path.Combine(mcDir, "versions", targetVersionId);
        Directory.CreateDirectory(verDir);

        string versionJson = await Http.GetStringAsync(versionUrl, ct);
        string versionJsonPath = Path.Combine(verDir, $"{targetVersionId}.json");
        await File.WriteAllTextAsync(versionJsonPath, versionJson, ct);

        using var verDoc = JsonDocument.Parse(versionJson);
        var root = verDoc.RootElement;

        // 1. Download Client JAR
        string clientUrl = root.GetProperty("downloads").GetProperty("client").GetProperty("url").GetString()!;
        string clientJarPath = Path.Combine(verDir, $"{targetVersionId}.jar");
        progress(10, $"Minecraft {targetVersionId}.jar indiriliyor...");
        await DownloadFileSimpleAsync(clientUrl, clientJarPath, ct);

        // 2. Download Asset Index & Local Asset Objects
        if (root.TryGetProperty("assetIndex", out var aiElem))
        {
            string aiId = aiElem.GetProperty("id").GetString()!;
            string aiUrl = aiElem.GetProperty("url").GetString()!;
            string indexPath = Path.Combine(mcDir, "assets", "indexes", $"{aiId}.json");
            string fullIndexPath = Path.Combine(mcDir, "assets", "indexes", $"{aiId}.full.json");
            Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
            await DownloadFileSimpleAsync(aiUrl, fullIndexPath, ct);
            if (!File.Exists(indexPath))
                File.Copy(fullIndexPath, indexPath, overwrite: true);

            progress(18, $"Minecraft {targetVersionId} temel doku, font ve ses varlıkları indiriliyor...");
            await JavaLaunchEngine.EnsureLocalAssetsAndSanitizeIndexAsync(mcDir, aiId, log);
        }

        // 3. Download all Windows Libraries & Natives
        string librariesDir = Path.Combine(mcDir, "libraries");
        if (root.TryGetProperty("libraries", out var libsArr) && libsArr.ValueKind == JsonValueKind.Array)
        {
            var downloadItems = new List<(string Url, string DestPath)>();
            foreach (var lib in libsArr.EnumerateArray())
            {
                if (lib.TryGetProperty("downloads", out var dlElem))
                {
                    if (dlElem.TryGetProperty("artifact", out var artElem))
                    {
                        string? url = artElem.GetProperty("url").GetString();
                        string? path = artElem.GetProperty("path").GetString();
                        if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(path))
                        {
                            downloadItems.Add((url, Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar))));
                        }
                    }

                    if (dlElem.TryGetProperty("classifiers", out var classElem))
                    {
                        string[] winKeys = ["natives-windows", "natives-windows-64", "natives-windows-x86_64"];
                        foreach (string k in winKeys)
                        {
                            if (classElem.TryGetProperty(k, out var natElem))
                            {
                                string? url = natElem.GetProperty("url").GetString();
                                string? path = natElem.GetProperty("path").GetString();
                                if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(path))
                                {
                                    downloadItems.Add((url, Path.Combine(librariesDir, path.Replace('/', Path.DirectorySeparatorChar))));
                                }
                            }
                        }
                    }
                }
            }

            int total = Math.Max(1, downloadItems.Count);
            int current = 0;
            foreach (var (url, dest) in downloadItems)
            {
                current++;
                double pct = 20.0 + ((double)current / total * 80.0);
                progress(pct, $"Kütüphane indiriliyor ({current}/{total}): {Path.GetFileName(dest)}");

                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    await DownloadFileSimpleAsync(url, dest, ct);
                }
            }
        }

        progress(100, $"Minecraft {targetVersionId} tamamen local çalışmaya hazır!");
        log($"[Ev Hazırlık İndiricisi] '{targetVersionId}' tüm kütüphaneleriyle 'game_data/.minecraft' içine indirildi.");
    }

    /// <summary>
    /// Okul bilgisayarlarında Java kurulu olmasa bile çalışması için Adoptium (Temurin) Portable JRE (Java 21 / 17 / 8)
    /// arşivini doğrudan 'game_data/runtime/java-{major}' klasörüne indirir ve açar.
    /// </summary>
    public static async Task DownloadPortableJavaJreAsync(
        int javaMajor,
        LauncherConfig config,
        Action<double, string> progress,
        Action<string> log,
        CancellationToken ct = default)
    {
        string runtimeDir = Path.Combine(config.GetRuntimeDir(), $"java-{javaMajor}");
        Directory.CreateDirectory(runtimeDir);

        string url = $"https://api.adoptium.net/v3/binary/latest/{javaMajor}/ga/windows/x64/jre/hotspot/normal/eclipse";
        string tempZip = Path.Combine(config.GetRuntimeDir(), $"jre_{javaMajor}_temp.zip");

        try
        {
            log($"[Portable Java] Java {javaMajor} JRE (Windows x64 Portable) indiriliyor...");
            progress(5, $"Portable Java {javaMajor} JRE indiriliyor...");

            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                await using var netStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

                byte[] buffer = new byte[65536];
                long totalRead = 0;
                int read;
                while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                    totalRead += read;
                    if (totalBytes > 0)
                    {
                        double pct = (double)totalRead / totalBytes * 85.0;
                        progress(pct, $"Portable Java {javaMajor} indiriliyor ({totalRead / (1024 * 1024)} MB / {totalBytes / (1024 * 1024)} MB)...");
                    }
                }
            }

            progress(90, $"Portable Java {javaMajor} 'game_data/runtime/java-{javaMajor}' içine çıkartılıyor...");
            await Task.Run(() => ZipFile.ExtractToDirectory(tempZip, runtimeDir, overwriteFiles: true), ct);

            progress(100, $"Portable Java {javaMajor} hazır! Okul bilgisayarında Java kurulu olmasa da çalışacak.");
            log($"[Portable Java] Java {javaMajor} başarıyla '{runtimeDir}' içine kuruldu.");
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try { File.Delete(tempZip); } catch { }
            }
        }
    }

    /// <summary>
    /// Evdeki bilgisayarda bulunan mevcut Java çalışma zamanını 'game_data/runtime/java-portable' içine kopyalar.
    /// </summary>
    public static async Task CopySystemJavaToPortableAsync(
        LauncherConfig config,
        Action<double, string> progress,
        Action<string> log)
    {
        string? javaExe = JavaLaunchEngine.FindJavaExecutable(config, 21) ?? JavaLaunchEngine.FindJavaExecutable(config, 17) ?? JavaLaunchEngine.FindJavaExecutable(config, 8);
        if (string.IsNullOrWhiteSpace(javaExe) || !File.Exists(javaExe))
            throw new FileNotFoundException("Sistemde kopyalanacak kurulu bir Java bulunamadı. 'Portable Java İndir' butonunu kullanabilirsiniz.");

        string? binDir = Path.GetDirectoryName(javaExe);
        string? jreRoot = binDir != null ? Directory.GetParent(binDir)?.FullName : null;
        if (string.IsNullOrWhiteSpace(jreRoot) || !Directory.Exists(jreRoot))
            throw new DirectoryNotFoundException("Java kök dizini tespit edilemedi.");

        string targetDir = Path.Combine(config.GetRuntimeDir(), "java-portable");
        log($"[Portable Java] '{jreRoot}' -> '{targetDir}' klasörüne kopyalanıyor...");
        progress(20, "Sistemdeki Java çalışma zamanı taşınabilir klasöre kopyalanıyor...");

        await Task.Run(() => CopyDirectoryRecursive(jreRoot, targetDir));

        progress(100, "Java çalışma zamanı 'game_data/runtime/java-portable' içine kopyalandı!");
        log("[Portable Java] Kopyalama tamamlandı.");
    }

    private static async Task DownloadFileSimpleAsync(string url, string destinationPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await resp.Content.CopyToAsync(fs, ct);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(targetDir, Path.GetFileName(file));
            if (!File.Exists(destFile))
                File.Copy(file, destFile, overwrite: false);
        }

        foreach (string subDir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
        }
    }
}
