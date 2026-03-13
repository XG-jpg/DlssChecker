# Changelog

## v0.0.3

### RU

- **автообнаружение игр**: при запуске приложение сканирует Steam, Epic Games и GOG — найденные игры с DLSS отображаются плитками с иконками прямо в интерфейсе; клик на плитку автоматически подставляет путь и запускает сканирование
- **статус DLSSTweaks**: кнопка «Проверить загрузку» заменена на автоматический цветной индикатор — зелёный (загружен), жёлтый (запустите игру для проверки), красный (нужен NVIDIA Override); кнопка «Включить Override» появляется только когда требуется
- **иконка приложения**: добавлена кастомная иконка с чипом GPU
- **исправлено**: окно теперь фиксированной ширины, ручное изменение размера отключено
- **исправлено**: дублирование игр в списке при нескольких библиотеках Steam
- **SmartScreen**: в README добавлен раздел с объяснением предупреждения Windows и инструкцией как запустить

### EN

- **game auto-detection**: on startup the app scans Steam, Epic Games, and GOG — games with DLSS are shown as tiles with icons directly in the UI; clicking a tile fills in the path and triggers a scan automatically
- **DLSSTweaks status**: the "Check load" button is replaced by an automatic colored indicator — green (loaded), yellow (launch the game to verify), red (NVIDIA Override required); the "Enable Override" button only appears when needed
- **app icon**: added a custom GPU chip icon
- **fix**: window is now fixed-width, manual resizing disabled
- **fix**: duplicate game entries when Steam has multiple library folders
- **SmartScreen**: added a README section explaining the Windows warning and how to run the app

## v0.0.2

### RU

- **исправлено**: встроенные архивы `DLSS.zip` и `DLSSTweaks.zip` не попадали в папку при публикации — приложение просило выбрать файл вручную
- **обновление приложения**: теперь скачивает и применяет обновление прямо на месте — перезаписывает файлы рядом с `.exe` и перезапускается автоматически, без ручного скачивания
- **после обновления**: показывает окно с изменениями текущей версии
- **источник DLSS**: теперь сначала скачивает актуальный `nvngx_dlss.dll` с официального репозитория `NVIDIA/DLSS` на GitHub, при неудаче — использует встроенный архив
- **исправлено**: `OnRollback` падал с необработанным исключением если файл был заблокирован (игра запущена)
- **исправлено**: сканирование папки теперь выполняется в фоне, не блокирует интерфейс
- **исправлено**: `DlssScanner` падал с `UnauthorizedAccessException` при попытке обхода защищённых вложенных папок

### EN

- **fix**: bundled `DLSS.zip` and `DLSSTweaks.zip` were missing from the publish output — the app was asking the user to pick the file manually
- **app auto-update**: now downloads and applies the update in-place — overwrites files next to the `.exe` and restarts automatically, no manual download needed
- **post-update changelog**: shows a window with changes for the current version after an update
- **DLSS source**: now attempts to download the latest `nvngx_dlss.dll` from the official `NVIDIA/DLSS` GitHub repository first, falls back to the bundled archive on failure
- **fix**: `OnRollback` crashed with an unhandled exception if the DLL file was locked (game running)
- **fix**: folder scan now runs in the background and no longer blocks the UI
- **fix**: `DlssScanner` crashed with `UnauthorizedAccessException` when traversing protected subfolders

## v0.0.1

### RU

- проверка `nvngx_dlss*.dll` в выбранной папке игры
- отображение версии DLSS в игре и последней доступной версии
- обновление DLSS из встроенного архива с созданием бэкапа
- откат DLSS из созданного бэкапа
- установка и удаление `DLSSTweaks`
- готовые пресеты для RTX 4000/5000 и RTX 3000
- проверка загрузки `DLSSTweaks` по `dlsstweaks.log`
- предложение включить `NVIDIA signature override`, если это может потребоваться
- кнопка GitHub в шапке приложения
- проверка обновлений приложения через GitHub Releases
- переход по прямой ссылке на скачивание asset-файла из latest release

### EN

- checks `nvngx_dlss*.dll` inside the selected game folder
- shows the DLSS version used by the game and the latest available version
- updates DLSS from the bundled archive and creates a backup
- rolls DLSS back from the created backup
- installs and removes `DLSSTweaks`
- includes ready presets for RTX 4000/5000 and RTX 3000
- verifies `DLSSTweaks` loading by checking `dlsstweaks.log`
- offers to enable `NVIDIA signature override` when it may be needed
- adds a GitHub button in the app header
- checks for app updates through GitHub Releases
- opens the direct asset download link from the latest release
