using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShinraiLauncher.Models;

namespace ShinraiLauncher.Services;

public static class OfflineSkinService
{
    private static readonly string[] ModernDefaultSkinNames =
    [
        "steve", "alex", "ari", "efe", "kai", "makena", "noor", "sunny", "zuri"
    ];

    public static void EnsureDefaultSkinsAndCapes()
    {
        string skinsDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "skins");
        string capesDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "capes");
        Directory.CreateDirectory(skinsDir);
        Directory.CreateDirectory(capesDir);

        CreatePresetSkinIfMissing(Path.Combine(skinsDir, "Shinrai_Shadow_Ninja.png"),
            skinColor: Color.FromRgb(225, 185, 155),
            hairColor: Color.FromRgb(20, 20, 28),
            eyeColor: Color.FromRgb(239, 68, 68),
            shirtColor: Color.FromRgb(24, 24, 34),
            accentColor: Color.FromRgb(220, 38, 38),
            pantsColor: Color.FromRgb(18, 18, 26),
            hasMask: true);

        CreatePresetSkinIfMissing(Path.Combine(skinsDir, "Crimson_Samurai.png"),
            skinColor: Color.FromRgb(235, 195, 165),
            hairColor: Color.FromRgb(153, 27, 27),
            eyeColor: Color.FromRgb(251, 191, 36),
            shirtColor: Color.FromRgb(127, 29, 29),
            accentColor: Color.FromRgb(245, 158, 11),
            pantsColor: Color.FromRgb(30, 27, 36),
            hasMask: false);

        CreatePresetSkinIfMissing(Path.Combine(skinsDir, "Cyber_Neon_Runner.png"),
            skinColor: Color.FromRgb(215, 180, 155),
            hairColor: Color.FromRgb(6, 182, 212),
            eyeColor: Color.FromRgb(34, 211, 238),
            shirtColor: Color.FromRgb(15, 23, 42),
            accentColor: Color.FromRgb(6, 182, 212),
            pantsColor: Color.FromRgb(17, 24, 39),
            hasMask: true);

        CreatePresetSkinIfMissing(Path.Combine(skinsDir, "Emerald_Hacker.png"),
            skinColor: Color.FromRgb(230, 190, 160),
            hairColor: Color.FromRgb(16, 185, 129),
            eyeColor: Color.FromRgb(52, 211, 153),
            shirtColor: Color.FromRgb(6, 78, 59),
            accentColor: Color.FromRgb(16, 185, 129),
            pantsColor: Color.FromRgb(20, 25, 30),
            hasMask: false);

        CreatePresetCapeIfMissing(Path.Combine(capesDir, "Shinrai_Crimson_Cape.png"),
            bgColor: Color.FromRgb(127, 29, 29),
            borderColor: Color.FromRgb(239, 68, 68),
            emblemColor: Color.FromRgb(251, 191, 36));

        CreatePresetCapeIfMissing(Path.Combine(capesDir, "Cyber_Cyan_Cape.png"),
            bgColor: Color.FromRgb(15, 23, 42),
            borderColor: Color.FromRgb(6, 182, 212),
            emblemColor: Color.FromRgb(56, 189, 248));

        CreatePresetCapeIfMissing(Path.Combine(capesDir, "Emerald_Elite_Cape.png"),
            bgColor: Color.FromRgb(6, 78, 59),
            borderColor: Color.FromRgb(16, 185, 129),
            emblemColor: Color.FromRgb(167, 243, 208));
    }

    private static void CreatePresetSkinIfMissing(
        string filePath,
        Color skinColor,
        Color hairColor,
        Color eyeColor,
        Color shirtColor,
        Color accentColor,
        Color pantsColor,
        bool hasMask)
    {
        if (File.Exists(filePath))
            return;

        var bmp = new WriteableBitmap(64, 64, 96, 96, PixelFormats.Bgra32, null);
        byte[] pixels = new byte[64 * 64 * 4];

        void FillRect(int rx, int ry, int rw, int rh, Color c)
        {
            for (int y = ry; y < ry + rh && y < 64; y++)
            {
                for (int x = rx; x < rx + rw && x < 64; x++)
                {
                    if (x < 0 || y < 0) continue;
                    int idx = (y * 64 + x) * 4;
                    pixels[idx + 0] = c.B;
                    pixels[idx + 1] = c.G;
                    pixels[idx + 2] = c.R;
                    pixels[idx + 3] = c.A;
                }
            }
        }

        // Head cube (0,0 to 32,16)
        FillRect(8, 0, 16, 8, hairColor);     // Top & Bottom head
        FillRect(0, 8, 32, 8, skinColor);     // All 4 sides of head
        FillRect(8, 8, 8, 3, hairColor);      // Front hair bangs
        FillRect(0, 8, 8, 4, hairColor);      // Right side hair
        FillRect(16, 8, 16, 8, hairColor);    // Left & back hair

        // Eyes on front face (x:8..15, y:8..15)
        FillRect(9, 12, 1, 1, Colors.White);
        FillRect(10, 12, 1, 1, eyeColor);
        FillRect(13, 12, 1, 1, eyeColor);
        FillRect(14, 12, 1, 1, Colors.White);

        if (hasMask)
        {
            FillRect(8, 13, 8, 3, shirtColor);
            FillRect(10, 14, 4, 1, accentColor);
        }

        // Body (16,16 to 40,32)
        FillRect(20, 16, 16, 4, shirtColor);
        FillRect(16, 20, 24, 12, shirtColor);
        // Chest emblem / stripe
        FillRect(22, 21, 4, 10, accentColor);
        FillRect(23, 22, 2, 8, shirtColor);

        // Right Arm (40,16 to 56,32) & Left Arm (32,48 to 48,64)
        FillRect(44, 16, 8, 4, shirtColor);
        FillRect(40, 20, 16, 9, shirtColor);
        FillRect(40, 26, 16, 1, accentColor); // Armband
        FillRect(40, 29, 16, 3, skinColor);   // Hands

        FillRect(36, 48, 8, 4, shirtColor);
        FillRect(32, 52, 16, 9, shirtColor);
        FillRect(32, 58, 16, 1, accentColor);
        FillRect(32, 61, 16, 3, skinColor);

        // Right Leg (0,16 to 16,32) & Left Leg (16,48 to 32,64)
        FillRect(4, 16, 8, 4, pantsColor);
        FillRect(0, 20, 16, 10, pantsColor);
        FillRect(0, 30, 16, 2, accentColor); // Boots

        FillRect(20, 48, 8, 4, pantsColor);
        FillRect(16, 52, 16, 10, pantsColor);
        FillRect(16, 62, 16, 2, accentColor);

        bmp.WritePixels(new Int32Rect(0, 0, 64, 64), pixels, 64 * 4, 0);
        SaveBitmapToPng(bmp, filePath);
    }

    private static void CreatePresetCapeIfMissing(string filePath, Color bgColor, Color borderColor, Color emblemColor)
    {
        if (File.Exists(filePath))
            return;

        var bmp = new WriteableBitmap(64, 32, 96, 96, PixelFormats.Bgra32, null);
        byte[] pixels = new byte[64 * 32 * 4];

        void FillRect(int rx, int ry, int rw, int rh, Color c)
        {
            for (int y = ry; y < ry + rh && y < 32; y++)
            {
                for (int x = rx; x < rx + rw && x < 64; x++)
                {
                    if (x < 0 || y < 0) continue;
                    int idx = (y * 64 + x) * 4;
                    pixels[idx + 0] = c.B;
                    pixels[idx + 1] = c.G;
                    pixels[idx + 2] = c.R;
                    pixels[idx + 3] = c.A;
                }
            }
        }

        // Standard Minecraft cape UV: front/back at (1,1) width 10, height 16
        FillRect(1, 1, 20, 16, bgColor);
        FillRect(1, 1, 10, 1, borderColor);
        FillRect(1, 16, 10, 1, borderColor);
        FillRect(1, 1, 1, 16, borderColor);
        FillRect(10, 1, 1, 16, borderColor);

        // Center Shinrai Diamond Emblem on Cape
        FillRect(4, 5, 4, 6, emblemColor);
        FillRect(5, 4, 2, 8, emblemColor);
        FillRect(5, 7, 2, 2, bgColor);

        bmp.WritePixels(new Int32Rect(0, 0, 64, 32), pixels, 64 * 4, 0);
        SaveBitmapToPng(bmp, filePath);
    }

    private static void SaveBitmapToPng(BitmapSource bmp, string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        encoder.Save(fs);
    }

    /// <summary>
    /// Seçilen skin (.png) dosyasından Launcher arayüzünde göstermek için 2D Karakter Önizlemesi oluşturur.
    /// </summary>
    public static BitmapSource? RenderSkinFrontPreview(string skinFilePath)
    {
        if (string.IsNullOrWhiteSpace(skinFilePath) || !File.Exists(skinFilePath))
            return null;

        try
        {
            var src = new BitmapImage();
            src.BeginInit();
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.UriSource = new Uri(Path.GetFullPath(skinFilePath));
            src.EndInit();
            src.Freeze();

            int w = src.PixelWidth;
            int h = src.PixelHeight;
            if (w < 64 || h < 32)
                return src;

            var formatted = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            byte[] srcPixels = new byte[w * h * 4];
            formatted.CopyPixels(srcPixels, w * 4, 0);

            // Canvas is 16x32 logical pixels, scaled up 8x -> 128x256
            const int logicalW = 16;
            const int logicalH = 32;
            byte[] canvas = new byte[logicalW * logicalH * 4];

            void Blit(int sx, int sy, int sw, int sh, int dx, int dy, bool flipX = false)
            {
                for (int y = 0; y < sh; y++)
                {
                    for (int x = 0; x < sw; x++)
                    {
                        int srcX = sx + (flipX ? (sw - 1 - x) : x);
                        int srcY = sy + y;
                        if (srcX >= w || srcY >= h) continue;

                        int sIdx = (srcY * w + srcX) * 4;
                        byte a = srcPixels[sIdx + 3];
                        if (a == 0) continue;

                        int destX = dx + x;
                        int destY = dy + y;
                        if (destX < 0 || destX >= logicalW || destY < 0 || destY >= logicalH) continue;

                        int dIdx = (destY * logicalW + destX) * 4;
                        canvas[dIdx + 0] = srcPixels[sIdx + 0];
                        canvas[dIdx + 1] = srcPixels[sIdx + 1];
                        canvas[dIdx + 2] = srcPixels[sIdx + 2];
                        canvas[dIdx + 3] = 255;
                    }
                }
            }

            // Head (8,8 8x8) -> (4, 0)
            Blit(8, 8, 8, 8, 4, 0);
            // Hat overlay (40,8 8x8) -> (4, 0)
            Blit(40, 8, 8, 8, 4, 0);

            // Body (20,20 8x12) -> (4, 8)
            Blit(20, 20, 8, 12, 4, 8);
            if (h >= 64)
                Blit(20, 36, 8, 12, 4, 8); // Jacket overlay

            // Right Arm (44,20 4x12) -> (0, 8)
            Blit(44, 20, 4, 12, 0, 8);
            if (h >= 64)
                Blit(44, 36, 4, 12, 0, 8);

            // Left Arm: 64x64 skins have (36,52 4x12), 64x32 mirrors right arm
            if (h >= 64)
            {
                Blit(36, 52, 4, 12, 12, 8);
                Blit(52, 52, 4, 12, 12, 8);
            }
            else
            {
                Blit(44, 20, 4, 12, 12, 8, flipX: true);
            }

            // Right Leg (4,20 4x12) -> (4, 20)
            Blit(4, 20, 4, 12, 4, 20);
            if (h >= 64)
                Blit(4, 36, 4, 12, 4, 20);

            // Left Leg (20,52 4x12) -> (8, 20)
            if (h >= 64)
            {
                Blit(20, 52, 4, 12, 8, 20);
                Blit(4, 52, 4, 12, 8, 20);
            }
            else
            {
                Blit(4, 20, 4, 12, 8, 20, flipX: true);
            }

            // Scale up 8x nearest-neighbor for crisp pixel art display
            const int scale = 8;
            int outW = logicalW * scale;
            int outH = logicalH * scale;
            byte[] scaled = new byte[outW * outH * 4];

            for (int y = 0; y < outH; y++)
            {
                int ly = y / scale;
                for (int x = 0; x < outW; x++)
                {
                    int lx = x / scale;
                    int sIdx = (ly * logicalW + lx) * 4;
                    int dIdx = (y * outW + x) * 4;
                    scaled[dIdx + 0] = canvas[sIdx + 0];
                    scaled[dIdx + 1] = canvas[sIdx + 1];
                    scaled[dIdx + 2] = canvas[sIdx + 2];
                    scaled[dIdx + 3] = canvas[sIdx + 3];
                }
            }

            var outBmp = new WriteableBitmap(outW, outH, 96, 96, PixelFormats.Bgra32, null);
            outBmp.WritePixels(new Int32Rect(0, 0, outW, outH), scaled, outW * 4, 0);
            outBmp.Freeze();
            return outBmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tamamen local / internetsiz ortamda seçilen Skin ve Pelerin'in hem Vanilla hem Modlu (OptiFine/Sodium/CustomSkinLoader)
    /// sürümlerde görünmesi için Local ResourcePack + CustomSkinLoader yerel profilini otomatik kurar.
    /// </summary>
    public static void ApplyOfflineSkinToJava(LauncherConfig config, Action<string>? log = null)
    {
        if (!config.ApplySkinLocally || string.IsNullOrWhiteSpace(config.SelectedSkinPath) || !File.Exists(config.SelectedSkinPath))
            return;

        try
        {
            string mcDir = config.GetMinecraftDir();
            string packDir = Path.Combine(mcDir, "resourcepacks", "ShinraiLocalSkin");
            Directory.CreateDirectory(packDir);

            // 1. Write pack.mcmeta (supported across 1.8 - 1.21.11+)
            string packMcmeta = """
            {
              "pack": {
                "pack_format": 15,
                "supported_formats": [1, 64],
                "min_format": 1,
                "max_format": 99,
                "description": "§c§lShinrai Offline Skin & Cape Pack (100% Local)"
              }
            }
            """;
            File.WriteAllText(Path.Combine(packDir, "pack.mcmeta"), packMcmeta);

            // 2. Copy skin to legacy entity path (1.8 - 1.19.2)
            string entityDir = Path.Combine(packDir, "assets", "minecraft", "textures", "entity");
            Directory.CreateDirectory(entityDir);
            File.Copy(config.SelectedSkinPath, Path.Combine(entityDir, "steve.png"), overwrite: true);
            File.Copy(config.SelectedSkinPath, Path.Combine(entityDir, "alex.png"), overwrite: true);

            // 3. Copy skin to modern player wide & slim paths (1.19.3 - 1.21+)
            string wideDir = Path.Combine(entityDir, "player", "wide");
            string slimDir = Path.Combine(entityDir, "player", "slim");
            Directory.CreateDirectory(wideDir);
            Directory.CreateDirectory(slimDir);

            foreach (string name in ModernDefaultSkinNames)
            {
                File.Copy(config.SelectedSkinPath, Path.Combine(wideDir, $"{name}.png"), overwrite: true);
                File.Copy(config.SelectedSkinPath, Path.Combine(slimDir, $"{name}.png"), overwrite: true);
            }

            // 4. Copy cape if selected
            if (!string.IsNullOrWhiteSpace(config.SelectedCapePath) && File.Exists(config.SelectedCapePath))
            {
                File.Copy(config.SelectedCapePath, Path.Combine(packDir, "pack.png"), overwrite: true);
            }
            else
            {
                File.Copy(config.SelectedSkinPath, Path.Combine(packDir, "pack.png"), overwrite: true);
            }

            // 5. Also configure CustomSkinLoader local skin folder (if CustomSkinLoader mod is used)
            string cslDir = Path.Combine(mcDir, "CustomSkinLoader");
            string cslSkinsDir = Path.Combine(cslDir, "LocalSkin", "skins");
            string cslCapesDir = Path.Combine(cslDir, "LocalSkin", "capes");
            Directory.CreateDirectory(cslSkinsDir);
            Directory.CreateDirectory(cslCapesDir);

            string cleanUser = OfflineAuthService.SanitizeUsername(config.Username);
            File.Copy(config.SelectedSkinPath, Path.Combine(cslSkinsDir, $"{cleanUser}.png"), overwrite: true);

            if (!string.IsNullOrWhiteSpace(config.SelectedCapePath) && File.Exists(config.SelectedCapePath))
            {
                File.Copy(config.SelectedCapePath, Path.Combine(cslCapesDir, $"{cleanUser}.png"), overwrite: true);
            }

            string cslConfig = """
            {
              "enable": true,
              "loadlist": [
                {
                  "name": "ShinraiLocalSkin",
                  "type": "Legacy",
                  "checkPNG": false,
                  "skin": "LocalSkin/skins/{USERNAME}.png",
                  "model": "auto",
                  "cape": "LocalSkin/capes/{USERNAME}.png"
                }
              ]
            }
            """;
            File.WriteAllText(Path.Combine(cslDir, "CustomSkinLoader.json"), cslConfig);

            // 6. Auto-enable ShinraiLocalSkin in options.txt so user doesn't even have to enable it manually!
            EnsureResourcePackInOptionsTxt(mcDir, "file/ShinraiLocalSkin");

            log?.Invoke($"[Skin] Offline skin ('{Path.GetFileName(config.SelectedSkinPath)}') yerel olarak entegre edildi.");
        }
        catch (Exception ex)
        {
            log?.Invoke($"[Skin Uyarı] Yerel skin uygulanırken hata: {ex.Message}");
        }
    }

    private static void EnsureResourcePackInOptionsTxt(string mcDir, string packEntry)
    {
        string optionsPath = Path.Combine(mcDir, "options.txt");
        if (!File.Exists(optionsPath))
        {
            File.WriteAllText(optionsPath, $"resourcePacks:[\"{packEntry}\"]\n");
            return;
        }

        string[] lines = File.ReadAllLines(optionsPath);
        bool found = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("resourcePacks:"))
            {
                found = true;
                if (!lines[i].Contains(packEntry))
                {
                    if (lines[i].Trim() == "resourcePacks:[]")
                        lines[i] = $"resourcePacks:[\"{packEntry}\"]";
                    else
                        lines[i] = lines[i].Replace("resourcePacks:[", $"resourcePacks:[\"{packEntry}\",");
                }
                break;
            }
        }

        if (!found)
        {
            var list = lines.ToList();
            list.Add($"resourcePacks:[\"{packEntry}\"]");
            File.WriteAllLines(optionsPath, list);
        }
        else
        {
            File.WriteAllLines(optionsPath, lines);
        }
    }
}
