# 信 Shinrai Minecraft Launcher (Java & Bedrock • %100 Local & Cracked • School Portable)

**Shinrai Minecraft Launcher**, okul bilgisayarlarında (veya internet kısıtlaması olan ortamlarda) **hiçbir dış sunucuya (Mojang / Microsoft) bağımlı olmadan**, **Yönetici (Admin) yetkisi gerektirmeden** ve **tamamen local (taşınabilir)** olarak hem **Minecraft Java Edition** hem de **Minecraft Bedrock Edition** oynayabilmeniz için geliştirilmiş yerel bir C# / WPF başlatıcısıdır.

---

## ✨ Temel Özellikler

1. **☕ Minecraft Java Edition (%100 Local & Cracked):**
   - `OfflinePlayer:<kullaniciadi>` standardında **Offline UUID v3** üretir (LAN dünyalarında ve offline sunucularda envanteriniz kaybolmaz).
   - **Tamamen Offline Ağ İzolasyonu:** Mojang auth/session sunucu sorgularını `127.0.0.1`'e yönlendirerek okul güvenlik duvarlarında (MEB / FortiGuard vb.) oyunun açılışta takılmasını engeller.
   - **Vanilla + OptiFine + Fabric (Sodium)** desteği (`inheritsFrom` profil birleştirme ve otomatik Windows `natives` `.dll` çıkartma).
   - **Portable Java Desteği:** Okul bilgisayarında Java kurulu olmasa bile `./game_data/runtime/` içindeki taşınabilir Java'yı otomatik algılar ve kullanır.

2. **🪨 Minecraft Bedrock Edition (Adminsiz Portable Klasör Modu):**
   - `./game_data/bedrock/Minecraft.Windows.exe` içindeki taşınabilir (Win32 / GDK) Bedrock sürümünü **Yönetici (Admin) izni istemeden** doğrudan başlatır.
   - Seçtiğiniz **Offline Oyuncu Adını (Gamertag)**, **Offline Skin'i (`custom.png`)** ve **FPS ayarlarını** Bedrock'un yerel `options.txt` dosyasına otomatik işler.

3. **👕 %100 Offline Skin & Pelerin (Cape) Motoru:**
   - İnternet bağlantısı olmadan kendi `.png` skin ve pelerinizi kullanmanızı sağlar.
   - Seçilen skin'i hem **Yerel ResourcePack (`ShinraiLocalSkin`)** olarak `1.8 - 1.21.4+` tüm sürümlere otomatik entegre eder, hem **CustomSkinLoader** yerel dizinine yazar, hem de **Bedrock Edition** içine aktarır.
   - Launcher içinde canlı **2D Pixel-Art Karakter Önizlemesi** ve hazır Shinrai skin/pelerin şablonları bulunur.

4. **⚡ Okul Bilgisayarı FPS Artırıcı & JVM Optimizasyonu:**
   - Dahili GPU'lu (Intel UHD / Vega) okul bilgisayarları için özel **Düşük Sistem G1GC**, **Aikar's Flags** ve **Java 21 ZGC** profilleri.
   - Tek tıkla `options.txt` içine yüksek FPS ayarları (Fast Graphics, VSync Kapalı, Düşük Gölge) uygulama.
   - Yerel `mods/` klasörü yöneticisi (Sodium, Lithium, OptiFine `.jar` modlarını tek tıkla açıp kapatma).

5. **🌐 Okul Ağı (Local Wi-Fi / Ethernet) Beraber Oynama Merkezi:**
   - Sınıftaki açık **Java (`224.0.2.60:4445`)** ve **Bedrock (`UDP 19132`)** LAN dünyalarını otomatik tarar.
   - **Okul Multicast Engeli Aşıcı (LAN Relay):** Okul switch/modemleri LAN yayınlarını engelliyorsa arkadaşınızın `IP:Port` adresini yazarak o sunucuyu kendi Çok Oyunculu listenize yerel LAN dünyası olarak yansıtabilir veya tek tıkla doğrudan bağlanabilirsiniz.

---

## 🚀 Evde Hazırlık -> GitHub'a Yükleme -> Okulda Oynama Rehberi

### Adım 1: Evde Oyun Paketini Hazırlama
1. `ShinraiLauncher.exe` uygulamasını açın ve **📦 GitHub & Paketleyici** sekmesine gidin.
2. **Sürüm Ekleme:**
   - Bilgisayarınızda zaten `.minecraft` veya `TL Legacy` kuruluysa **"📥 Seçili Sürümü Portable Klasöre Aktar"** butonuna basın.
   - Veya **"B) Evde Temiz Vanilla Sürümü İndir"** kısmından istediğiniz sürümü (`1.21.1`, `1.20.1`, `1.16.5`, `1.8.9` vb.) seçip indirin.
3. **Portable Java Ekleme (Okul PC'lerinde Java yoksa şarttır):**
   - **"⬇️ Portable Java İndir"** veya **"☕ Sistemdeki Java'yı Kopyala"** butonuna basarak Java'yı `game_data/runtime` içine ekleyin.
4. **Bedrock Ekleme (İsteğe Bağlı):**
   - Taşınabilir Bedrock (`Minecraft.Windows.exe` ve oyun dosyaları) klasörünüzü `game_data/bedrock/` içine kopyalayın.
5. **Release ZIP Oluşturma:**
   - **"📦 GitHub Release Paketi Oluştur (.zip)"** butonuna tıklayın.
   - Launcher tüm `game_data` klasörünüzü `Release_Output/shinrai-game-bundle.zip` olarak paketleyecektir.

### Adım 2: GitHub'a ve GitHub Releases'a Yükleme
1. Kodu ve `Portable_Build/ShinraiLauncher.exe` dosyasını kendi GitHub deponuza yükleyin.
2. GitHub deponuzda sağdaki **Releases -> Draft a new release** sayfasına girin.
3. Bir etiket (örn: `v1.0`) oluşturun ve `Release_Output/shinrai-game-bundle.zip` dosyasını (ve `ShinraiLauncher.exe`'yi) sürükleyip yayınlayın (**Publish release**).
   *(Not: GitHub Releases tek dosya için 2 GB'a kadar ücretsiz izin verir ve okul ağlarında GitHub genellikle açıktır!)*

### Adım 3: Okulda İndirip Oynama
1. Okul bilgisayarında GitHub deponuzdan `ShinraiLauncher.exe` dosyasını indirin ve çalıştırın.
2. **📦 GitHub & Paketleyici** sekmesinde kendi GitHub deponuzu (`kullaniciadi/repo-adi`) yazıp **"⬇️ Seçili Paketi İndir & Kur"** butonuna tıklayın (veya ZIP'i tarayıcıdan indirip **"📂 Bilgisayardan / USB'den .zip Kur"** seçin).
3. Paket çıkartıldıktan sonra **▶ OYUNU BAŞLAT** butonuna basın — artık tamamen internetsiz ve local olarak oynayabilirsiniz!
