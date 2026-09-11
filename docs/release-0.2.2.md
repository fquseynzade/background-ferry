# Background Ferry 0.2.2 — Beta

Fixes generic mint icons appearing for Chrome and WebView2 in the downloaded Windows release.

## What changed

- Reads executable paths with limited process-query access. The previous MainModule lookup also requested memory-reading access, which restricted browser audio processes can deny.
- Falls back to another running process of the same application when its audio process cannot supply metadata.
- Retries temporary icon failures instead of caching the generic placeholder as a successful result.
- Adds regression checks for limited-access path lookup and background-thread identity resolution.

The failure was reproduced using the actual 0.2.1 GitHub binaries. On the same live Chrome and WebView2 processes, the patched resolver returns native icons where the previous implementation returned Access Denied. No administrator launch is required.

## Update / Обновление

Download **BackgroundFerry-win-x64.zip**, exit the old app using the tray menu, extract the complete archive to a new folder, and run **BackgroundFerry.exe**. Existing preferences are retained.

Скачай **BackgroundFerry-win-x64.zip**, выйди из старой версии через меню в трее, распакуй весь архив в новую папку и запусти **BackgroundFerry.exe**. Настройки сохранятся. Запуск от администратора не нужен.

The Chrome extension has not changed in this patch and does not need reinstalling. Chrome tab capture remains experimental; this patch fixes Windows application icons.
