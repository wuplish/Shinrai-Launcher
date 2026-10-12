@echo off
chcp 65001 >nul
title Shinrai Lunar Client - GitHub (wuplish/Shinrai-Launcher) Otomatik Yukleyici
echo ===========================================================================
echo   SHINRAI LUNAR CLIENT - GITHUB OTOMATIK REPO OLUSTURUCU VE YUKLEYICI
echo   Hesap: wuplish  ^|  Repo: https://github.com/wuplish/Shinrai-Launcher
echo ===========================================================================
echo.
echo 1. Tarayicinizda https://github.com/new adresinden 'Shinrai-Launcher' adinda
echo    Public (Bos) bir repo olusturun (veya asagida GitHub Token girin).
echo.
set /p GH_TOKEN="GitHub Token varsa yapistirin (Yoksa tarayici ile giris icin direkt ENTER'a basin): "

if not "%GH_TOKEN%"=="" (
    echo [API] wuplish/Shinrai-Launcher Public deposu olusturuluyor...
    powershell -Command "$headers = @{ Authorization = 'token %GH_TOKEN%'; Accept = 'application/vnd.github+json' }; $body = @{ name = 'Shinrai-Launcher'; description = 'Shinrai Lunar Client - 1.21.11 (Sodium/Fabric/OptiFine) & 1.12.2 Portable Offline Launcher'; private = $false } | ConvertTo-Json; try { Invoke-RestMethod -Uri 'https://api.github.com/user/repos' -Method Post -Headers $headers -Body $body -ContentType 'application/json' | Out-Null; Write-Host 'Repo basariyla olusturuldu!' -ForegroundColor Green } catch { Write-Host 'Repo zaten var veya devam ediliyor...' -ForegroundColor Yellow }"
    git remote remove origin 2>nul
    git remote add origin https://wuplish:%GH_TOKEN%@github.com/wuplish/Shinrai-Launcher.git
) else (
    echo [Bilgi] Tarayicida GitHub giris penceresi acilacak.
    echo Eger 'Shinrai-Launcher' adinda bos repo olusturmadiysaniz simdi acilan sayfadan olusturun:
    start https://github.com/new?name=Shinrai-Launcher^&visibility=public
    pause
    git remote remove origin 2>nul
    git remote add origin https://github.com/wuplish/Shinrai-Launcher.git
)

echo.
echo [Git] Tum dosyalar (Kaynak Kodlari + Lunar EXE + 1.21.11 Sodium + 1.12.2) yukleniyor...
git branch -M main
git push -u origin main --force

echo.
echo ===========================================================================
echo   TAMAMLANDI! Repo adresiniz: https://github.com/wuplish/Shinrai-Launcher
echo ===========================================================================
pause
