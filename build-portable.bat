@echo off
chcp 65001 >nul
title Shinrai Minecraft Launcher - Portable Tek-EXE Derleyici
echo ====================================================================
echo   SHINRAI MINECRAFT LAUNCHER - PORTABLE DERLEYICI (OKUL PC UYUMLU)
echo ====================================================================
echo.
echo [1/2] Okul bilgisayarlarinda .NET kurulu olmasa bile calisacak
echo       Bagimsiz Tek-Dosya (Self-Contained Single-File) .EXE uretiliyor...
echo.

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ".\Portable_Build"

if %ERRORLEVEL% NEQ 0 (
    echo [HATA] Derleme basarisiz oldu!
    pause
    exit /b 1
)

echo.
echo [2/2] Tamamlandi!
echo       Cikti Klasoru: .\Portable_Build\ShinraiLauncher.exe
echo ====================================================================
pause
