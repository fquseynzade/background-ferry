using System.Collections.Generic;
using System.Windows;

namespace BackgroundFerry.App;

public static class UiText
{
    public static string Language { get; private set; } = "ru";
    private static readonly Dictionary<string, (string En, string Ru)> Strings = new()
    {
        ["Mixer"] = ("Mixer", "Микшер"), ["Tabs"] = ("Chrome tabs", "Вкладки Chrome"),
        ["Settings"] = ("Settings", "Настройки"), ["Title"] = ("Your sound, in balance.", "Твой звук. В балансе."),
        ["Subtitle"] = ("Music that makes room.", "Музыка, которая уступает место."),
        ["Music"] = ("Background music", "Фоновая музыка"), ["Priority"] = ("Priority audio", "Приоритетный звук"),
        ["MusicTip"] = ("Play music, then select its app. Its original Windows volume is the maximum. For music and video in the same browser, use Chrome tabs.", "Включи музыку и выбери её приложение. Исходная громкость в Windows станет максимальной. Для музыки и видео в одном браузере используй «Вкладки Chrome»."),
        ["PriorityTip"] = ("Check the apps that should lower your music. Unselected apps are ignored. The music app cannot also be priority.", "Отметь приложения, при звуке которых музыка должна стихать. Остальные игнорируются. Музыкальное приложение не может быть приоритетным."),
        ["MixControl"] = ("Mixing control", "Управление миксом"), ["Steady"] = ("Steady", "Постоянный"), ["Adaptive"] = ("Adaptive", "Адаптивный"),
        ["ModeTip"] = ("Steady keeps music consistently quiet during priority audio. Adaptive varies its level within a small quiet band. Neither mode recognizes speech.", "Постоянный режим держит музыку на одном тихом уровне. Адаптивный немного меняет её громкость по уровню основного звука. Распознавания речи нет."),
        ["Duck"] = ("Music remaining", "Уровень фона"), ["DuckTip"] = ("Percentage of the original app volume while priority audio plays. 20% of an original 50% becomes 10%. In silence the original volume returns.", "Доля исходной громкости во время приоритетного звука. 20% от исходных 50% — это 10%. В тишине вернётся исходный уровень."),
        ["Sensitivity"] = ("Signal threshold", "Порог звука"), ["SensitivityTip"] = ("Lower values detect quieter priority audio. If a quiet video does not lower music, move the slider left. Notifications in selected apps can also trigger mixing.", "Меньшие значения учитывают более тихий звук. Если тихое видео не приглушает музыку, сдвинь ползунок влево. Уведомления выбранных приложений тоже могут срабатывать."),
        ["Attack"] = ("Fade down", "Приглушение"), ["AttackTip"] = ("How quickly music fades down when priority audio starts. The duration reaches about 95% of the change.", "Как быстро музыка стихает при появлении основного звука. За указанное время выполняется примерно 95% перехода."),
        ["Hold"] = ("Silence hold", "Пауза в тишине"), ["HoldTip"] = ("Wait this long in silence before bringing music back. Prevents volume jumps between words.", "Столько тишины нужно перед возвращением музыки. Помогает избежать скачков между словами."),
        ["Release"] = ("Fade back", "Возвращение"), ["ReleaseTip"] = ("How gradually music returns after the silence hold. Longer times sound softer.", "Как плавно музыка возвращается после паузы в тишине. Большее время даёт более мягкий переход."),
        ["Startup"] = ("Start with Windows", "Запускать с Windows"), ["StartupTip"] = ("Launch the app in the tray at sign-in. Mixing starts off. Keep the portable folder in a permanent location.", "Открывать программу в трее при входе в Windows. Микширование изначально выключено. Папка программы должна оставаться на постоянном месте."),
        ["Minimized"] = ("Start in tray", "Запускать в трее"), ["MinimizedTip"] = ("Hide the window at launch. Double-click the tray icon to open it.", "Скрывать окно при запуске. Двойной щелчок по значку в трее откроет его."),
        ["Language"] = ("Language", "Язык"), ["LanguageTip"] = ("Changes the interface immediately and remembers your choice. The Chrome companion has its own language selector.", "Меняет язык сразу и сохраняет выбор. У расширения Chrome есть собственный переключатель языка."),
        ["Start"] = ("Start mixing", "Включить микс"), ["Stop"] = ("Stop mixing", "Остановить"),
        ["Ready"] = ("Ready", "Готово"), ["Off"] = ("Off", "Выключено"), ["Ducking"] = ("Making room", "Приглушение"),
        ["Holding"] = ("Holding", "Ждём тишины"), ["Returning"] = ("Returning music", "Возвращаем музыку"), ["Music free"] = ("Music is free", "Музыка свободна"),
        ["Waiting"] = ("Waiting for audio", "Ожидание звука"), ["Playing"] = ("Playing", "Воспроизведение"), ["Idle"] = ("Idle", "Тишина"),
        ["Volume"] = ("App volume", "Громкость приложения"), ["Gain"] = ("Music gain", "Уровень музыки"),
        ["SelectSources"] = ("Choose music and at least one priority app.", "Выбери музыку и хотя бы одно приоритетное приложение."),
        ["Restored"] = ("Original volume restored; manual changes kept.", "Исходная громкость восстановлена. Ручные изменения сохранены."),
        ["SettingsChanged"] = ("Settings changed. Start the mix to apply them.", "Настройки изменены. Включи микс для их применения."),
        ["Override"] = ("Manual volume change detected. Restart the mix to control that session again.", "Громкость изменена вручную. Перезапусти микс, чтобы снова управлять этой сессией."),
        ["Open"] = ("Open Background Ferry", "Открыть Background Ferry"), ["Exit"] = ("Exit and restore volume", "Выйти и вернуть громкость"),
        ["TrayTitle"] = ("Background Ferry is in the tray", "Background Ferry в трее"),
        ["TrayText"] = ("Mixing continues. Use the tray menu to stop or exit.", "Микширование продолжается. Остановить его или выйти можно через меню в трее."),
        ["BadSettings"] = ("Could not read settings. Defaults loaded.", "Не удалось прочитать настройки. Загружены значения по умолчанию."),
        ["AudioError"] = ("Audio unavailable", "Аудио недоступно"), ["StartupError"] = ("Could not update startup", "Не удалось изменить автозапуск"),
        ["GuardianError"] = ("Could not start volume recovery helper.", "Не удалось запустить восстановление громкости."),
        ["ErrorStopped"] = ("Mixing stopped after an error. Restoring volume where possible.", "Микширование остановлено из-за ошибки. Восстанавливаем громкость, где это возможно."),
        ["AlreadyRunning"] = ("Background Ferry is already running. Open it from the tray.", "Background Ferry уже запущен. Открой его через трей."),
        ["RecoveryBusy"] = ("Volume recovery is still running. Try again in a moment.", "Громкость ещё восстанавливается. Попробуй через несколько секунд."),
        ["CannotStart"] = ("Background Ferry could not start", "Не удалось запустить Background Ferry"),
        ["TabsTitle"] = ("One browser. Separate sound.", "Один браузер. Разный звук."),
        ["TabsIntro"] = ("Experimental Chrome companion for mixing individual tabs.", "Экспериментальный режим для звука отдельных вкладок Chrome."),
        ["TabsSteps"] = ("1. Open chrome://extensions and enable Developer mode.\n2. Click Load unpacked and choose the chrome-extension folder.\n3. Open your music tab, click the extension and choose Music.\n4. Do the same on your video tab, choosing Priority. Start the mix in the extension.", "1. Открой chrome://extensions и включи режим разработчика.\n2. Нажми «Загрузить распакованное расширение» и выбери папку chrome-extension.\n3. На вкладке музыки открой расширение и нажми «Музыка».\n4. На вкладке видео выбери «Приоритет». Включи микс в расширении."),
        ["TabsTip"] = ("Chrome requires you to authorize each tab by clicking the extension. It processes audio locally, does not record it, and restores normal playback when released. Desktop and tab mixes have separate settings; avoid applying both to Chrome at once.", "Chrome требует разрешить каждую вкладку нажатием на расширение. Звук обрабатывается локально, без записи; при отключении возвращается обычное воспроизведение. Настройки расширения и программы независимы. Не применяй оба микса к Chrome одновременно."),
        ["OpenFolder"] = ("Open extension folder", "Папка расширения"), ["CopyAddress"] = ("Copy Chrome address", "Скопировать адрес Chrome"),
        ["Copied"] = ("chrome://extensions copied. Paste it into Chrome.", "Адрес chrome://extensions скопирован. Вставь его в Chrome."),
        ["FolderMissing"] = ("Extension folder not found. Extract the complete release ZIP.", "Папка расширения не найдена. Распакуй весь архив программы."),
        ["Help"] = ("Information", "Подсказка"), ["Ms"] = ("ms", "мс"),
        ["SettingsSubtitle"] = ("Make it feel like yours.", "Настрой под себя."),
        ["Overrides"] = ("manual overrides", "ручных изменений")
    };
    public static string T(string key) => Strings.TryGetValue(key, out var pair) ? (Language == "ru" ? pair.Ru : pair.En) : key;
    public static void Apply(string language)
    {
        Language = language == "en" ? "en" : "ru";
        foreach (string key in Strings.Keys) Application.Current.Resources[key] = T(key);
    }
}
