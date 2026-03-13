# DLSS Checker

[![Stars](https://img.shields.io/github/stars/XG-jpg/DllsChecker?style=for-the-badge)](https://github.com/XG-jpg/DllsChecker/stargazers)
[![Last Commit](https://img.shields.io/github/last-commit/XG-jpg/DllsChecker?style=for-the-badge)](https://github.com/XG-jpg/DllsChecker/commits/main)
[![Windows](https://img.shields.io/badge/platform-Windows-0078D6?style=for-the-badge)](#)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge)](#)

## RU

Windows-утилита для проверки и обновления `nvngx_dlss*.dll` в играх, установки `DLSSTweaks` с готовыми пресетами, создания бэкапа и отката изменений.

### Что умеет приложение

- ищет `nvngx_dlss*.dll` в папке игры
- показывает версию DLSS в игре и последнюю доступную версию
- обновляет DLSS из встроенного архива
- создает бэкап и позволяет откатить DLSS
- устанавливает и удаляет `DLSSTweaks`
- применяет готовые пресеты для RTX 4000/5000 и RTX 3000
- проверяет загрузку `DLSSTweaks` по `dlsstweaks.log`
- при необходимости предлагает включить `NVIDIA signature override`
- проверяет новую версию приложения через GitHub Releases

### Локализация

Файл локализации: `src/DlssChecker/Resources/Localization.json`

- `ru` — русский интерфейс
- `en` — английский интерфейс
- можно добавлять новые языки по той же структуре
- приложение выбирает язык по языку системы
- если язык не найден, используется `defaultLanguage`

### Важные файлы

- `src/DlssChecker/Resources/VersionInfo.json` — версия и источник DLSS
- `src/DlssChecker/Assets/Presets/4000_5000.ini` — пресет для RTX 4000/5000
- `src/DlssChecker/Assets/Presets/3000.ini` — пресет для RTX 3000
- `src/DlssChecker/Services/GitHubReleaseService.cs` — проверка обновлений приложения

### Сборка

```powershell
dotnet restore src/DlssChecker/DlssChecker.csproj
dotnet build src/DlssChecker/DlssChecker.csproj -c Release
```

### Portable publish

```powershell
dotnet publish src/DlssChecker/DlssChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Готовая self-contained сборка попадает в папку `artifacts/`.

## EN

Windows utility for checking and updating `nvngx_dlss*.dll` in games, installing `DLSSTweaks` with ready presets, creating backups, and rolling changes back when needed.

### Features

- finds `nvngx_dlss*.dll` inside the selected game folder
- shows the DLSS version used by the game and the latest available version
- updates DLSS from the bundled archive
- creates a backup and supports rollback
- installs and removes `DLSSTweaks`
- applies ready presets for RTX 4000/5000 and RTX 3000
- checks whether `DLSSTweaks` loaded by looking for `dlsstweaks.log`
- offers to enable `NVIDIA signature override` when needed
- checks for a newer app version via GitHub Releases

### Localization

Localization file: `src/DlssChecker/Resources/Localization.json`

- `ru` — Russian UI
- `en` — English UI
- new languages can be added using the same structure
- the app auto-detects the system language
- `defaultLanguage` is used as fallback

### Important files

- `src/DlssChecker/Resources/VersionInfo.json` — DLSS version source
- `src/DlssChecker/Assets/Presets/4000_5000.ini` — RTX 4000/5000 preset
- `src/DlssChecker/Assets/Presets/3000.ini` — RTX 3000 preset
- `src/DlssChecker/Services/GitHubReleaseService.cs` — app update check

### Build

```powershell
dotnet restore src/DlssChecker/DlssChecker.csproj
dotnet build src/DlssChecker/DlssChecker.csproj -c Release
```

### Portable publish

```powershell
dotnet publish src/DlssChecker/DlssChecker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The self-contained build is written to `artifacts/`.
