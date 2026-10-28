@echo off
setlocal
title Build Panda Browser
echo.
echo  Panda Browser - Windows EXE Builder
echo  -----------------------------------
echo.
echo This creates PandaBrowser.exe using Microsoft Edge WebView2.
echo It requires .NET 8 SDK and the WebView2 Runtime.
echo.
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: .NET SDK was not found.
  echo Install the free .NET 8 SDK from:
  echo https://dotnet.microsoft.com/download/dotnet/8.0
  echo Then run this file again.
  pause
  exit /b 1
)
dotnet publish "%~dp0PandaBrowser.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0publish"
if errorlevel 1 (
  echo.
  echo Build failed. Check the error messages above.
  pause
  exit /b 1
)
echo.
echo Success! Your EXE is:
echo %~dp0publish\PandaBrowser.exe
pause
