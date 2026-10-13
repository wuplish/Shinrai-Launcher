using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ShinraiLauncher.Models;
using ShinraiLauncher.Services;

namespace ShinraiLauncher;

public partial class MainWindow : Window
{
    private LauncherConfig _config = null!;
    private readonly LanNetworkService _lanService = new();
    private readonly ObservableCollection<LanServerEntry> _lanServers = new();
    private List<LunarModToggleItem> _lunarMods = new();
    private List<(string VersionId, string SourceMcDir)> _systemScannedVersions = new();
    private bool _isInitializing = true;
    private bool _isRelayRunning = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _lanService.Dispose();
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeWindow_Click(sender, e);
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _config = LauncherConfig.Load();
        _config.EnsureDirectories();
        OfflineSkinService.EnsureDefaultSkinsAndCapes();

        // Populate Lunar Mod Menu
        _lunarMods = LunarOptimizerService.GetDefaultLunarMods();
        LunarModsItemsControl.ItemsSource = _lunarMods;

        // Apply Lunar FPS configs immediately
        LunarOptimizerService.ApplyAllLunarOptimizations(_config.GetMinecraftDir(), _lunarMods);

        UsernameTextBox.Text = _config.Username;
        if (GreetingUsernameText != null)
            GreetingUsernameText.Text = _config.Username;
        UpdateUuidPreview();

        RamSlider.Value = Math.Clamp(_config.AllocatedRamMb, 1024, 12288);
        RamValueLabel.Text = $"{_config.AllocatedRamMb} MB";
        ActiveRamStatusText.Text = $"Ayrılan Bellek: {_config.AllocatedRamMb} MB (Lunar Aikar's G1GC)";

        JvmPresetComboBox.SelectedIndex = (int)_config.JvmPreset;
        CustomJvmArgsTextBox.Text = JavaLaunchEngine.BuildJvmOptimizationArgs(_config);

        OfflineIsolationCheckBox.IsChecked = _config.FullOfflineIsolation;
        AutoFpsOptionsCheckBox.IsChecked = _config.AutoApplyFpsOptions;
        ApplySkinCheckBox.IsChecked = _config.ApplySkinLocally;
        SlimSkinCheckBox.IsChecked = _config.IsSlimSkin;

        GitHubRepoTextBox.Text = _config.GitHubRepo;
        DirectZipUrlTextBox.Text = string.IsNullOrWhiteSpace(_config.DirectPackUrl)
            ? $"https://github.com/{_config.GitHubRepo}/archive/refs/heads/main.zip"
            : _config.DirectPackUrl;
        DirectLanIpTextBox.Text = _config.LastDirectConnectIp;

        LoadSkinAndCapeDropdowns();
        RefreshInstalledVersions();
        RefreshBedrockStatus();
        RefreshLocalModsList();
        ScanSystemVersionsInternal();

        LanServersListBox.ItemsSource = _lanServers;
        _lanService.ServerDiscovered += OnLanServerDiscovered;
        _lanService.StartListening();

        var localIps = LanNetworkService.GetLocalLanIpAddresses();
        string primaryIp = localIps.FirstOrDefault() ?? "127.0.0.1";
        CardLocalIpText.Text = primaryIp;
        LanTabIpText.Text = string.Join("  |  ", localIps);

        SetActiveEdition(_config.SelectedEdition == "Bedrock" ? "Bedrock" : "Java");

        _isInitializing = false;
        AppendLog("Shinrai Lunar Client v2.0 başlatıldı (1.21.11 Sodium/OptiFine & 1.12.2 Hazır).");
    }

    private void AppendLog(string message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            ConsoleTextBox.AppendText(line);
            ConsoleTextBox.ScrollToEnd();
        });
    }

    private void UpdateProgress(double percent, string statusText)
    {
        Dispatcher.InvokeAsync(() =>
        {
            double clamped = Math.Clamp(percent, 0, 100);
            MainProgressBar.Value = clamped;
            StatusPercentText.Text = $"{clamped:F0}%";
            StatusActionText.Text = statusText;
        });
    }

    private void UpdateUuidPreview()
    {
        string clean = OfflineAuthService.SanitizeUsername(UsernameTextBox.Text);
        string uuid = OfflineAuthService.GenerateOfflineUuid(clean);
        UuidPreviewText.Text = $"UUID: {uuid}";
        if (GreetingUsernameText != null)
            GreetingUsernameText.Text = clean;
    }

    private void UsernameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        _config.Username = OfflineAuthService.SanitizeUsername(UsernameTextBox.Text);
        UpdateUuidPreview();
        _config.Save();
    }

    // ==================== NAVIGATION ====================
    private void NavButton_Checked(object sender, RoutedEventArgs e)
    {
        if (ViewPlay == null) return;

        ViewPlay.Visibility = NavPlayBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewFps.Visibility = NavFpsBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewSkin.Visibility = NavSkinBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewLan.Visibility = NavLanBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewGithub.Visibility = NavGithubBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (TopBreadcrumbText != null)
        {
            if (NavPlayBtn.IsChecked == true) TopBreadcrumbText.Text = "Launchpad";
            else if (NavFpsBtn.IsChecked == true) TopBreadcrumbText.Text = "Mods & FPS Engine";
            else if (NavLanBtn.IsChecked == true) TopBreadcrumbText.Text = "School LAN Satellite";
            else if (NavSkinBtn.IsChecked == true) TopBreadcrumbText.Text = "Skin & Cape Studio";
            else if (NavGithubBtn.IsChecked == true) TopBreadcrumbText.Text = "GitHub Cloud & Pack";
        }
    }

    private void GoToGitHubTab_Click(object sender, RoutedEventArgs e) => NavGithubBtn.IsChecked = true;
    private void OpenFpsSettingsFromHero_Click(object sender, RoutedEventArgs e) => NavFpsBtn.IsChecked = true;
    private void OpenLanTabFromSidebar_Click(object sender, RoutedEventArgs e) => NavLanBtn.IsChecked = true;

    private void OpenGameDataFolder_Click(object sender, RoutedEventArgs e)
    {
        string dir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
    }

    private void ToggleConsoleBtn_Click(object sender, RoutedEventArgs e)
    {
        ConsoleDrawer.Visibility = ConsoleDrawer.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ClearConsoleBtn_Click(object sender, RoutedEventArgs e) => ConsoleTextBox.Clear();

    private void UpdateHeroLaunchButtonLabels(string versionOrEdition)
    {
        if (LaunchBtnTitleText == null || LaunchBtnSubtitleText == null)
            return;

        LaunchBtnTitleText.Text = "LAUNCH GAME";
        string lower = versionOrEdition.ToLowerInvariant();
        if (lower.Contains("bedrock"))
            LaunchBtnSubtitleText.Text = "Shinrai Bedrock Edition (Local)";
        else if (lower.Contains("sodium") || lower.Contains("fabric"))
            LaunchBtnSubtitleText.Text = "Shinrai 1.21.11 with 🚀 Fabric / Sodium";
        else if (lower.Contains("1.12"))
            LaunchBtnSubtitleText.Text = "Shinrai 1.12.2 with ⚔️ Classic PvP";
        else
            LaunchBtnSubtitleText.Text = $"Shinrai {versionOrEdition} with 💎 Vanilla+";
    }

    // ==================== QUICK PROFILE SELECTION ====================
    private void QuickSelect12111Sodium_Click(object sender, RoutedEventArgs e) => SelectOrTargetJavaVersion("1.21.11-Sodium");
    private void QuickSelect12111Vanilla_Click(object sender, RoutedEventArgs e) => SelectOrTargetJavaVersion("1.21.11");
    private void QuickSelect1122_Click(object sender, RoutedEventArgs e) => SelectOrTargetJavaVersion("1.12.2");

    private void SelectOrTargetJavaVersion(string targetId)
    {
        SetActiveEdition("Java");
        _config.SelectedJavaVersion = targetId;
        _config.Save();
        RefreshInstalledVersions();
        UpdateHeroLaunchButtonLabels(targetId);
        BottomEditionBadge.Text = $"{targetId.ToUpperInvariant()} • LUNAR";
    }

    private void SelectJavaEditionBtn_Click(object sender, RoutedEventArgs e) => SetActiveEdition("Java");
    private void SelectBedrockEditionBtn_Click(object sender, RoutedEventArgs e) => SetActiveEdition("Bedrock");

    private void SetActiveEdition(string edition)
    {
        _config.SelectedEdition = edition;
        if (!_isInitializing) _config.Save();

        if (edition == "Bedrock")
        {
            JavaEditionCard.Visibility = Visibility.Collapsed;
            BedrockEditionCard.Visibility = Visibility.Visible;
            BottomEditionBadge.Text = "BEDROCK EDITION";
            UpdateHeroLaunchButtonLabels("Bedrock");
            RefreshBedrockStatus();
        }
        else
        {
            JavaEditionCard.Visibility = Visibility.Visible;
            BedrockEditionCard.Visibility = Visibility.Collapsed;
            RefreshInstalledVersions();
        }
    }

    private void RefreshVersionsBtn_Click(object sender, RoutedEventArgs e)
    {
        RefreshInstalledVersions();
        RefreshBedrockStatus();
        RefreshLocalModsList();
        AppendLog("Yerel sürümler ve modlar yenilendi.");
    }

    private void RefreshInstalledVersions()
    {
        string mcDir = _config.GetMinecraftDir();
        var versions = JavaLaunchEngine.ScanInstalledVersions(mcDir);

        JavaVersionComboBox.ItemsSource = versions;

        if (versions.Count > 0)
        {
            var match = versions.FirstOrDefault(v => v.Id.Equals(_config.SelectedJavaVersion, StringComparison.OrdinalIgnoreCase)) ?? versions[0];
            JavaVersionComboBox.SelectedItem = match;
            VersionStatusHintText.Text = $"✓ {versions.Count} yerel profil hazır (1.21.11 Sodium/Vanilla & 1.12.2).";
            UpdateHeroLaunchButtonLabels(match.Id);
            BottomEditionBadge.Text = $"{match.Id.ToUpperInvariant()} • LUNAR";
        }
        else
        {
            VersionStatusHintText.Text = "⚠️ LAUNCH butonuna bastığınızda tüm sürümler otomatik kurulacaktır.";
            JavaRuntimeStatusText.Text = "Otomatik Kurulum Hazır";
            UpdateHeroLaunchButtonLabels(_config.SelectedJavaVersion);
        }
    }

    private void JavaVersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (JavaVersionComboBox.SelectedItem is InstalledVersionItem item)
        {
            _config.SelectedJavaVersion = item.Id;
            if (!_isInitializing) _config.Save();

            UpdateHeroLaunchButtonLabels(item.Id);
            BottomEditionBadge.Text = $"{item.Id.ToUpperInvariant()} • LUNAR";

            string? javaPath = JavaLaunchEngine.FindJavaExecutable(_config, item.RequiredJavaMajor);
            if (!string.IsNullOrWhiteSpace(javaPath))
            {
                bool isPortable = javaPath.StartsWith(LauncherConfig.GetPortableRootDir(), StringComparison.OrdinalIgnoreCase);
                string modeTag = isPortable ? "Portable (game_data/runtime)" : "Sistem Java";
                JavaRuntimeStatusText.Text = $"✓ Java {item.RequiredJavaMajor}+ Hazır ({modeTag})";
            }
            else
            {
                JavaRuntimeStatusText.Text = $"⚠️ Java {item.RequiredJavaMajor} Bulunamadı ('☁️ GITHUB & KURULUM' sekmesinden Portable Java indirin)";
            }
        }
    }

    private void RefreshBedrockStatus()
    {
        var status = BedrockLaunchEngine.CheckBedrockStatus(_config);
        BedrockStatusText.Text = status.StatusSummary;
    }

    private void BrowseBedrockExeBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Portable Minecraft Bedrock (Minecraft.Windows.exe) Seçin",
            Filter = "Minecraft Bedrock Exe (Minecraft.Windows.exe;*.exe)|Minecraft.Windows.exe;*.exe"
        };

        if (dlg.ShowDialog() == true)
        {
            _config.CustomBedrockExePath = dlg.FileName;
            _config.Save();
            RefreshBedrockStatus();
            AppendLog($"Özel Bedrock yürütülebilir dosyası seçildi: {dlg.FileName}");
        }
    }

    // ==================== AUTO SETUP 1.21.11 + SODIUM & 1.12.2 ====================
    private async void AutoSetupTargetVersionsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            await LunarOptimizerService.SetupTargetVersionsWithSodiumAsync(_config, UpdateProgress, AppendLog);
            _config.SelectedJavaVersion = "1.21.11-Sodium";
            _config.Save();
            RefreshInstalledVersions();
            RefreshLocalModsList();
            MessageBox.Show(
                "✓ 1.21.11 (Fabric + Sodium + Lithium + FerriteCore + EntityCulling)\n" +
                "✓ 1.21.11 (Vanilla / OptiFine)\n" +
                "✓ 1.12.2 (1.12.12 Classic PvP)\n" +
                "✓ Tüm Lunar FPS ve Fullbright ayarları\n\n" +
                "başarıyla 'game_data/.minecraft' içine kuruldu! Şimdi doğrudan LAUNCH butonuna basabilirsiniz.",
                "Shinrai Lunar Kurulumu Tamamlandı",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"[Otomatik Kurulum Hatası] {ex.Message}");
            MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }

    // ==================== LUNAR MOD MENU & FPS ====================
    private void LunarModCardToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modId)
        {
            var mod = _lunarMods.FirstOrDefault(m => m.Id == modId);
            if (mod != null)
            {
                mod.IsEnabled = !mod.IsEnabled;
                LunarModsItemsControl.ItemsSource = null;
                LunarModsItemsControl.ItemsSource = _lunarMods;

                LunarOptimizerService.ApplyAllLunarOptimizations(_config.GetMinecraftDir(), _lunarMods, AppendLog);
                RefreshLocalModsList();
                UpdateProgress(100, $"{mod.Title}: {(mod.IsEnabled ? "ENABLED" : "DISABLED")}");
            }
        }
    }

    private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RamValueLabel == null) return;
        int mb = (int)RamSlider.Value;
        RamValueLabel.Text = $"{mb} MB";
        ActiveRamStatusText.Text = $"Ayrılan Bellek: {mb} MB (Lunar Aikar's G1GC)";

        if (!_isInitializing)
        {
            _config.AllocatedRamMb = mb;
            _config.Save();
        }
    }

    private void JvmPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        _config.JvmPreset = (JvmOptimizationPreset)Math.Clamp(JvmPresetComboBox.SelectedIndex, 0, 3);
        CustomJvmArgsTextBox.Text = JavaLaunchEngine.BuildJvmOptimizationArgs(_config);
        _config.Save();
    }

    private void CustomJvmArgsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (_config.JvmPreset == JvmOptimizationPreset.Custom)
        {
            _config.CustomJvmArgs = CustomJvmArgsTextBox.Text;
            _config.Save();
        }
    }

    private void FpsCheckBoxes_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _config.FullOfflineIsolation = OfflineIsolationCheckBox.IsChecked == true;
        _config.AutoApplyFpsOptions = AutoFpsOptionsCheckBox.IsChecked == true;
        _config.Save();
    }

    private void ApplyFpsOptionsNow_Click(object sender, RoutedEventArgs e)
    {
        LunarOptimizerService.ApplyAllLunarOptimizations(_config.GetMinecraftDir(), _lunarMods, AppendLog);
        BedrockLaunchEngine.ApplyOfflineProfileToBedrock(_config, AppendLog);
        UpdateProgress(100, "Tüm Lunar FPS, Sodium, OptiFine ve Fullbright ayarları uygulandı!");
    }

    private void BrowseJavaExeBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Portable Java (java.exe veya javaw.exe) Seçin",
            Filter = "Java Executable (java.exe;javaw.exe)|java.exe;javaw.exe"
        };

        if (dlg.ShowDialog() == true)
        {
            _config.CustomJavaExePath = dlg.FileName;
            _config.Save();
            RefreshInstalledVersions();
            AppendLog($"Özel Java çalışma zamanı ayarlandı: {dlg.FileName}");
        }
    }

    private void RefreshLocalModsList()
    {
        string modsDir = Path.Combine(_config.GetMinecraftDir(), "mods");
        Directory.CreateDirectory(modsDir);

        var items = new List<LocalModItem>();
        foreach (string file in Directory.GetFiles(modsDir))
        {
            string name = Path.GetFileName(file);
            if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase))
                continue;

            bool enabled = name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
            long kb = new FileInfo(file).Length / 1024;
            string badge = enabled ? "● ENABLED" : "○ DISABLED";

            items.Add(new LocalModItem
            {
                FileName = name,
                FullPath = file,
                IsEnabled = enabled,
                SizeText = $"{kb} KB",
                CategoryBadge = badge
            });
        }

        ModsListBox.ItemsSource = items;
    }

    private void AddModJarBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Minecraft Mod Dosyası (.jar) Seçin",
            Filter = "Java Mod Dosyası (*.jar)|*.jar",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            string modsDir = Path.Combine(_config.GetMinecraftDir(), "mods");
            Directory.CreateDirectory(modsDir);
            foreach (string src in dlg.FileNames)
            {
                string dest = Path.Combine(modsDir, Path.GetFileName(src));
                File.Copy(src, dest, overwrite: true);
                AppendLog($"Mod eklendi: {Path.GetFileName(src)}");
            }
            RefreshLocalModsList();
        }
    }

    private void OpenModsFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        string modsDir = Path.Combine(_config.GetMinecraftDir(), "mods");
        Directory.CreateDirectory(modsDir);
        Process.Start(new ProcessStartInfo("explorer.exe", modsDir) { UseShellExecute = true });
    }

    private void ToggleModItemBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string fullPath && File.Exists(fullPath))
        {
            string newPath = fullPath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? fullPath[..^".disabled".Length]
                : fullPath + ".disabled";

            File.Move(fullPath, newPath, overwrite: true);
            RefreshLocalModsList();
        }
    }

    // ==================== OFFLINE SKIN & CAPE ====================
    private void LoadSkinAndCapeDropdowns()
    {
        string skinsDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "skins");
        string capesDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "capes");

        var skinFiles = Directory.Exists(skinsDir) ? Directory.GetFiles(skinsDir, "*.png") : [];
        var capeFiles = Directory.Exists(capesDir) ? Directory.GetFiles(capesDir, "*.png") : [];

        PresetSkinsComboBox.ItemsSource = skinFiles.Select(Path.GetFileName).ToList();
        PresetCapesComboBox.ItemsSource = capeFiles.Select(Path.GetFileName).ToList();

        if (string.IsNullOrWhiteSpace(_config.SelectedSkinPath) || !File.Exists(_config.SelectedSkinPath))
        {
            if (skinFiles.Length > 0)
                _config.SelectedSkinPath = skinFiles[0];
        }

        if (string.IsNullOrWhiteSpace(_config.SelectedCapePath) || !File.Exists(_config.SelectedCapePath))
        {
            if (capeFiles.Length > 0)
                _config.SelectedCapePath = capeFiles[0];
        }

        PresetSkinsComboBox.SelectedItem = Path.GetFileName(_config.SelectedSkinPath);
        PresetCapesComboBox.SelectedItem = Path.GetFileName(_config.SelectedCapePath);

        UpdateSkinPreviewUI();
    }

    private void UpdateSkinPreviewUI()
    {
        if (!string.IsNullOrWhiteSpace(_config.SelectedSkinPath) && File.Exists(_config.SelectedSkinPath))
        {
            SelectedSkinPathText.Text = $"Seçili Skin: {_config.SelectedSkinPath}";
            CardSkinSummaryText.Text = Path.GetFileName(_config.SelectedSkinPath);
            SkinPreviewImage.Source = OfflineSkinService.RenderSkinFrontPreview(_config.SelectedSkinPath);
        }

        if (!string.IsNullOrWhiteSpace(_config.SelectedCapePath) && File.Exists(_config.SelectedCapePath))
        {
            SelectedCapePathText.Text = $"Seçili Pelerin: {_config.SelectedCapePath}";
        }
    }

    private void PresetSkinsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PresetSkinsComboBox.SelectedItem is string fileName)
        {
            string full = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "skins", fileName);
            if (File.Exists(full))
            {
                _config.SelectedSkinPath = full;
                _config.Save();
                UpdateSkinPreviewUI();
            }
        }
    }

    private void PresetCapesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (PresetCapesComboBox.SelectedItem is string fileName)
        {
            string full = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "capes", fileName);
            if (File.Exists(full))
            {
                _config.SelectedCapePath = full;
                _config.Save();
                UpdateSkinPreviewUI();
            }
        }
    }

    private void BrowseSkinFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Minecraft Skin (.png - 64x64 veya 64x32) Seçin",
            Filter = "PNG Skin Dosyası (*.png)|*.png"
        };

        if (dlg.ShowDialog() == true)
        {
            string skinsDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "skins");
            Directory.CreateDirectory(skinsDir);
            string dest = Path.Combine(skinsDir, Path.GetFileName(dlg.FileName));
            if (!string.Equals(dlg.FileName, dest, StringComparison.OrdinalIgnoreCase))
                File.Copy(dlg.FileName, dest, overwrite: true);

            _config.SelectedSkinPath = dest;
            _config.Save();
            LoadSkinAndCapeDropdowns();
        }
    }

    private void BrowseCapeFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Minecraft Pelerin / Cape (.png - 64x32) Seçin",
            Filter = "PNG Pelerin Dosyası (*.png)|*.png"
        };

        if (dlg.ShowDialog() == true)
        {
            string capesDir = Path.Combine(LauncherConfig.GetPortableRootDir(), "game_data", "capes");
            Directory.CreateDirectory(capesDir);
            string dest = Path.Combine(capesDir, Path.GetFileName(dlg.FileName));
            if (!string.Equals(dlg.FileName, dest, StringComparison.OrdinalIgnoreCase))
                File.Copy(dlg.FileName, dest, overwrite: true);

            _config.SelectedCapePath = dest;
            _config.Save();
            LoadSkinAndCapeDropdowns();
        }
    }

    private void SkinSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _config.ApplySkinLocally = ApplySkinCheckBox.IsChecked == true;
        _config.IsSlimSkin = SlimSkinCheckBox.IsChecked == true;
        _config.Save();
    }

    private void ApplySkinNowBtn_Click(object sender, RoutedEventArgs e)
    {
        OfflineSkinService.ApplyOfflineSkinToJava(_config, AppendLog);
        BedrockLaunchEngine.ApplyOfflineProfileToBedrock(_config, AppendLog);
        UpdateProgress(100, "Offline Skin & Pelerin yerel olarak uygulandı!");
    }

    // ==================== SCHOOL LAN CENTER ====================
    private void OnLanServerDiscovered(LanServerEntry entry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var existing = _lanServers.FirstOrDefault(s => s.Endpoint == entry.Endpoint && s.Edition == entry.Edition);
            if (existing != null)
            {
                int idx = _lanServers.IndexOf(existing);
                _lanServers[idx] = entry;
            }
            else
            {
                _lanServers.Add(entry);
                AppendLog($"[LAN Keşif] {entry.Edition} sunucusu bulundu: {entry.Motd} ({entry.Endpoint})");
            }
        });
    }

    private void CopyLocalIpBtn_Click(object sender, RoutedEventArgs e)
    {
        var ips = LanNetworkService.GetLocalLanIpAddresses();
        string rawIp = ips.FirstOrDefault()?.Split(' ')[0] ?? "127.0.0.1";
        Clipboard.SetText(rawIp);
        UpdateProgress(100, $"Yerel IP adresin panoya kopyalandı: {rawIp}");
    }

    private void RefreshLanListBtn_Click(object sender, RoutedEventArgs e)
    {
        _lanServers.Clear();
        _lanService.StartListening();
        UpdateProgress(100, "Okul LAN ağı yeniden taranıyor...");
    }

    private async void ProbeDirectIpBtn_Click(object sender, RoutedEventArgs e)
    {
        string target = DirectLanIpTextBox.Text.Trim();
        _config.LastDirectConnectIp = target;
        _config.Save();

        UpdateProgress(40, $"'{target}' adresi doğrudan yoklanıyor...");
        var found = await _lanService.ProbeDirectEndpointAsync(target);
        if (found != null)
        {
            OnLanServerDiscovered(found);
            UpdateProgress(100, $"Sunucu aktif! ({found.Edition} - {found.Endpoint})");
        }
        else
        {
            UpdateProgress(100, $"'{target}' adresinden yanıt alınamadı.");
        }
    }

    private void ToggleRelayBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_isRelayRunning)
        {
            _lanService.StopLocalLanRelay();
            _isRelayRunning = false;
            ToggleRelayBtn.Content = "📡 Çok Oyunculu Listeme Yansıt (Relay)";
            UpdateProgress(100, "Yerel LAN yansıtıcı durduruldu.");
        }
        else
        {
            string target = DirectLanIpTextBox.Text.Trim();
            _config.LastDirectConnectIp = target;
            _config.Save();

            _lanService.StartLocalLanRelay(target, "Okul Arkadaş Sunucusu", AppendLog);
            _isRelayRunning = true;
            ToggleRelayBtn.Content = "⏹ Yansıtmayı Durdur (Aktif)";
            UpdateProgress(100, $"'{target}' adresi Çok Oyunculu -> LAN Dünyaları listesine yansıtılıyor!");
        }
    }

    private async void ConnectSelectedLanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (LanServersListBox.SelectedItem is LanServerEntry entry)
        {
            if (entry.Edition == "Bedrock")
            {
                SetActiveEdition("Bedrock");
                await LaunchActiveGameAsync();
            }
            else
            {
                SetActiveEdition("Java");
                await LaunchActiveGameAsync(entry.IpAddress, entry.Port);
            }
        }
    }

    private async void DirectLaunchToIpBtn_Click(object sender, RoutedEventArgs e)
    {
        string ep = DirectLanIpTextBox.Text.Trim();
        _config.LastDirectConnectIp = ep;
        _config.Save();

        string ip = ep;
        int port = 25565;
        if (ep.Contains(':'))
        {
            string[] p = ep.Split(':');
            ip = p[0].Trim();
            int.TryParse(p[1].Trim(), out port);
        }

        SetActiveEdition("Java");
        await LaunchActiveGameAsync(ip, port);
    }

    // ==================== GITHUB RELEASES & PACKAGER ====================
    private async void FetchReleaseAssetsBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.GitHubRepo = GitHubRepoTextBox.Text.Trim();
            _config.Save();

            UpdateProgress(25, $"GitHub '{_config.GitHubRepo}' üzerindeki Release paketleri kontrol ediliyor...");
            var assets = await GitHubReleaseService.FetchReleaseAssetsAsync(_config.GitHubRepo);
            ReleaseAssetsComboBox.ItemsSource = assets;

            if (assets.Count > 0)
            {
                ReleaseAssetsComboBox.SelectedIndex = 0;
                UpdateProgress(100, $"{assets.Count} adet Release paketi bulundu!");
            }
            else
            {
                UpdateProgress(100, "Release bulundu ancak içinde .zip dosyası yok.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[GitHub Hata] {ex.Message}");
            UpdateProgress(100, $"GitHub Release sorgulanamadı: {ex.Message}");
        }
    }

    private async void DownloadSelectedAssetBtn_Click(object sender, RoutedEventArgs e)
    {
        if (ReleaseAssetsComboBox.SelectedItem is GitHubReleaseAssetInfo asset)
            await RunPackageDownloadAsync(asset.DownloadUrl);
    }

    private async void DownloadDirectUrlBtn_Click(object sender, RoutedEventArgs e)
    {
        string url = DirectZipUrlTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url)) return;

        _config.DirectPackUrl = url;
        _config.Save();
        await RunPackageDownloadAsync(url);
    }

    private async Task RunPackageDownloadAsync(string url)
    {
        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            await GitHubReleaseService.DownloadAndExtractPackageAsync(url, UpdateProgress, AppendLog);
            RefreshInstalledVersions();
            RefreshBedrockStatus();
            RefreshLocalModsList();
        }
        catch (Exception ex)
        {
            AppendLog($"[İndirme Hatası] {ex.Message}");
            MessageBox.Show($"Paket indirilirken hata:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }

    private async void InstallLocalZipBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Shinrai Oyun Paketi (.zip) Seçin",
            Filter = "ZIP Arşivi (*.zip)|*.zip"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                LaunchGameBtn.IsEnabled = false;
                ConsoleDrawer.Visibility = Visibility.Visible;
                await GitHubReleaseService.InstallFromLocalZipAsync(dlg.FileName, UpdateProgress, AppendLog);
                RefreshInstalledVersions();
                RefreshBedrockStatus();
                RefreshLocalModsList();
            }
            catch (Exception ex)
            {
                AppendLog($"[ZIP Kurulum Hatası] {ex.Message}");
                MessageBox.Show($"ZIP çıkartılırken hata:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LaunchGameBtn.IsEnabled = true;
            }
        }
    }

    private void ScanSystemVersionsBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanSystemVersionsInternal();
        UpdateProgress(100, $"Sistemde {_systemScannedVersions.Count} adet kurulu Minecraft sürümü bulundu.");
    }

    private void ScanSystemVersionsInternal()
    {
        _systemScannedVersions = GitHubReleaseService.ScanSystemMinecraftVersions();
        SystemVersionsComboBox.ItemsSource = _systemScannedVersions
            .Select(x => $"{x.VersionId}  ({x.SourceMcDir})")
            .ToList();

        if (_systemScannedVersions.Count > 0)
            SystemVersionsComboBox.SelectedIndex = 0;
    }

    private async void ImportSystemVersionBtn_Click(object sender, RoutedEventArgs e)
    {
        int idx = SystemVersionsComboBox.SelectedIndex;
        if (idx < 0 || idx >= _systemScannedVersions.Count) return;

        var (verId, srcDir) = _systemScannedVersions[idx];
        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            await GitHubReleaseService.ImportVersionFromSystemAsync(verId, srcDir, _config, UpdateProgress, AppendLog);
            RefreshInstalledVersions();
            RefreshLocalModsList();
        }
        catch (Exception ex)
        {
            AppendLog($"[Aktarım Hatası] {ex.Message}");
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }

    private async void DownloadVanillaAtHomeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (VanillaDownloadComboBox.SelectedItem is ComboBoxItem item && item.Content is string verId)
        {
            try
            {
                LaunchGameBtn.IsEnabled = false;
                ConsoleDrawer.Visibility = Visibility.Visible;
                await GitHubReleaseService.DownloadVanillaVersionForOfflineUseAsync(verId, _config, UpdateProgress, AppendLog);
                RefreshInstalledVersions();
            }
            catch (Exception ex)
            {
                AppendLog($"[İndirme Hatası] {ex.Message}");
            }
            finally
            {
                LaunchGameBtn.IsEnabled = true;
            }
        }
    }

    private async void DownloadPortableJavaBtn_Click(object sender, RoutedEventArgs e)
    {
        int major = 21;
        if (PortableJavaMajorComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr &&
            int.TryParse(tagStr, out int parsed))
        {
            major = parsed;
        }

        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            await GitHubReleaseService.DownloadPortableJavaJreAsync(major, _config, UpdateProgress, AppendLog);
            RefreshInstalledVersions();
        }
        catch (Exception ex)
        {
            AppendLog($"[Portable Java Hatası] {ex.Message}");
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }

    private async void CreateReleaseBundleBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            string zipPath = await GitHubReleaseService.CreateGitHubReleaseBundleAsync(UpdateProgress, AppendLog);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{zipPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog($"[Paketleme Hatası] {ex.Message}");
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }

    // ==================== MAIN LAUNCH ACTION ====================
    private async void LaunchGameBtn_Click(object sender, RoutedEventArgs e)
    {
        await LaunchActiveGameAsync();
    }

    private async Task LaunchActiveGameAsync(string? directServerIp = null, int? directServerPort = null)
    {
        try
        {
            LaunchGameBtn.IsEnabled = false;
            ConsoleDrawer.Visibility = Visibility.Visible;
            _config.Save();

            if (_config.SelectedEdition == "Bedrock")
            {
                UpdateProgress(40, "Minecraft Bedrock Edition başlatılıyor...");
                BedrockLaunchEngine.LaunchBedrockEdition(_config, AppendLog);
                UpdateProgress(100, "Minecraft Bedrock Edition başlatıldı!");
            }
            else
            {
                // Apply Lunar FPS & Mod optimizations before launch
                LunarOptimizerService.ApplyAllLunarOptimizations(_config.GetMinecraftDir(), _lunarMods, AppendLog);

                string targetId = (JavaVersionComboBox.SelectedItem as InstalledVersionItem)?.Id
                                  ?? _config.SelectedJavaVersion
                                  ?? "1.21.11-Sodium";

                string expectedJson = Path.Combine(_config.GetMinecraftDir(), "versions", targetId, $"{targetId}.json");
                if (!File.Exists(expectedJson))
                {
                    string repoZipUrl = $"https://github.com/{_config.GitHubRepo}/archive/refs/heads/main.zip";
                    AppendLog($"[GitHub Otomatik Kurulum] Sadece 'ShinraiLauncher.exe' algılandı! '{_config.GitHubRepo}' deponuzdan tüm 'game_data' (Java 21/8 + 1.21.11 Sodium + 1.12.2) indiriliyor...");
                    try
                    {
                        await GitHubReleaseService.DownloadAndExtractPackageAsync(repoZipUrl, UpdateProgress, AppendLog);
                    }
                    catch (Exception repoEx)
                    {
                        AppendLog($"[GitHub Arşiv Uyarı] {repoEx.Message} -> Doğrudan kurulum motoruna geçiliyor...");
                        await LunarOptimizerService.SetupTargetVersionsWithSodiumAsync(_config, UpdateProgress, AppendLog);
                    }
                    RefreshInstalledVersions();
                    RefreshLocalModsList();
                }

                UpdateProgress(35, $"Minecraft {targetId} (Lunar Offline Engine) başlatılıyor...");
                var proc = await JavaLaunchEngine.LaunchJavaEditionAsync(
                    _config,
                    targetId,
                    AppendLog,
                    directServerIp,
                    directServerPort);

                if (proc != null)
                {
                    UpdateProgress(100, $"Minecraft {targetId} çalışıyor (Oyuncu: {_config.Username})");
                    if (InstancesRunningText != null)
                        InstancesRunningText.Text = "1 Instance Running ↗";
                    proc.Exited += (_, _) => Dispatcher.InvokeAsync(() =>
                    {
                        if (InstancesRunningText != null)
                            InstancesRunningText.Text = "0 Instances Running ↗";
                    });
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"[Başlatma Hatası] {ex.Message}");
            UpdateProgress(0, "Başlatma sırasında hata oluştu.");
            MessageBox.Show(ex.Message, "Shinrai Lunar Başlatma Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LaunchGameBtn.IsEnabled = true;
        }
    }
}
