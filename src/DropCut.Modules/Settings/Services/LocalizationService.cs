using DropCut.Core.Contracts;
using DropCut.Core.Domain;
using DropCut.Core.Events;
using DropCut.Modules.Settings.Contracts;
using DropCut.Modules.Settings.Models;

namespace DropCut.Modules.Settings.Services;

/// <summary>
/// Localization engine providing pure, single-language strings across 10 supported languages
/// with runtime dynamic switching and domain event notification.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IEventBus _eventBus;
    private readonly IUserPreferenceRepository? _preferenceRepository;
    private string _currentLanguageCode = "ar";

    private static readonly IReadOnlyList<LanguageOption> _supportedLanguages = new List<LanguageOption>
    {
        new("ar", "العربية", "العربية", true),
        new("en", "English", "English", false),
        new("es", "Spanish", "Español", false),
        new("hi", "Hindi", "हिन्दी", false),
        new("zh", "Chinese", "中文", false),
        new("fr", "French", "Français", false),
        new("de", "German", "Deutsch", false),
        new("ru", "Russian", "Русский", false),
        new("ja", "Japanese", "日本語", false),
        new("pt", "Portuguese", "Português", false)
    };

    private static readonly Dictionary<string, Dictionary<string, string>> _dictionaries = new()
    {
        ["ar"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "مساحة العمل",
            ["Nav_Vault"] = "خزينة اللقطات",
            ["Nav_Settings"] = "الإعدادات",
            ["Nav_AuditLog"] = "سجل العمليات",
            ["Tool_Select"] = "تحديد",
            ["Tool_Pen"] = "قلم الرسم",
            ["Tool_Highlighter"] = "قلم التمييز",
            ["Tool_Eraser"] = "ممحاة",
            ["Tool_Ruler"] = "مسطرة القياس",
            ["Tool_Crop"] = "قص الأبعاد",
            ["Tool_Hand"] = "يد السحب والمشاركة",
            ["Action_NewSnip"] = "لقطة جديدة",
            ["Action_Undo"] = "تراجع",
            ["Action_Redo"] = "إعادة",
            ["Action_Save"] = "حفظ",
            ["Action_Copy"] = "نسخ",
            ["Action_Share"] = "مشاركة",
            ["Action_Export"] = "تصدير",
            ["Status_AutoSaving"] = "جارٍ الحفظ التلقائي...",
            ["Status_Saved"] = "تم الحفظ تلقائياً",
            ["Status_Ready"] = "جاهز",
            ["Tab_NewSnippet"] = "لقطة {0}",
            ["Tab_Close"] = "إغلاق",
            ["Tab_CloseOthers"] = "إغلاق النوافذ الأخرى",
            ["Tab_CloseRight"] = "إغلاق جهة اليمين",
            ["Tab_Pin"] = "تثبيت اللسان",
            ["Tab_Favorite"] = "إضافة للمفضلة",
            ["Settings_Title"] = "إعدادات التطبيق",
            ["Settings_Language"] = "اللغة",
            ["Settings_Theme"] = "المظهر والسمة",
            ["Settings_Theme_System"] = "سمة النظام",
            ["Settings_Theme_Light"] = "فاتح",
            ["Settings_Theme_Dark"] = "داكن",
            ["Settings_Theme_Mica"] = "ميكا شفاف",
            ["Settings_AutoSave"] = "الحفظ التلقائي المستمر",
            ["Settings_Debounce"] = "مهلة الحفظ التلقائي (مللي ثانية)",
            ["Settings_VaultPath"] = "مجلد حفظ اللقطات",
            ["Settings_Hotkey"] = "اختصار القص الفوري",
            ["Settings_SaveButton"] = "حفظ الإعدادات",
            ["Settings_ResetButton"] = "استعادة الافتراضيات",
            ["Settings_SavedSuccess"] = "تم حفظ الإعدادات بنجاح"
        },
        ["en"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Workspace",
            ["Nav_Vault"] = "Vault Gallery",
            ["Nav_Settings"] = "Settings",
            ["Nav_AuditLog"] = "Audit Log",
            ["Tool_Select"] = "Select",
            ["Tool_Pen"] = "Pen",
            ["Tool_Highlighter"] = "Highlighter",
            ["Tool_Eraser"] = "Eraser",
            ["Tool_Ruler"] = "Ruler",
            ["Tool_Crop"] = "Crop",
            ["Tool_Hand"] = "Hand Drag & Share",
            ["Action_NewSnip"] = "New Snip",
            ["Action_Undo"] = "Undo",
            ["Action_Redo"] = "Redo",
            ["Action_Save"] = "Save",
            ["Action_Copy"] = "Copy",
            ["Action_Share"] = "Share",
            ["Action_Export"] = "Export",
            ["Status_AutoSaving"] = "Auto-saving...",
            ["Status_Saved"] = "Auto-saved",
            ["Status_Ready"] = "Ready",
            ["Tab_NewSnippet"] = "Snip {0}",
            ["Tab_Close"] = "Close",
            ["Tab_CloseOthers"] = "Close Others",
            ["Tab_CloseRight"] = "Close to the Right",
            ["Tab_Pin"] = "Pin Tab",
            ["Tab_Favorite"] = "Add to Favorites",
            ["Settings_Title"] = "Application Settings",
            ["Settings_Language"] = "Language",
            ["Settings_Theme"] = "Theme & Appearance",
            ["Settings_Theme_System"] = "System Default",
            ["Settings_Theme_Light"] = "Light",
            ["Settings_Theme_Dark"] = "Dark",
            ["Settings_Theme_Mica"] = "Mica Alt",
            ["Settings_AutoSave"] = "Continuous Auto-Save",
            ["Settings_Debounce"] = "Auto-Save Debounce (ms)",
            ["Settings_VaultPath"] = "Vault Storage Path",
            ["Settings_Hotkey"] = "Instant Snipping Hotkey",
            ["Settings_SaveButton"] = "Save Settings",
            ["Settings_ResetButton"] = "Reset to Defaults",
            ["Settings_SavedSuccess"] = "Settings saved successfully"
        },
        ["es"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Área de trabajo",
            ["Nav_Vault"] = "Bóveda de capturas",
            ["Nav_Settings"] = "Configuración",
            ["Nav_AuditLog"] = "Registro de auditoría",
            ["Tool_Select"] = "Seleccionar",
            ["Tool_Pen"] = "Bolígrafo",
            ["Tool_Highlighter"] = "Resaltador",
            ["Tool_Eraser"] = "Borrador",
            ["Tool_Ruler"] = "Regla",
            ["Tool_Crop"] = "Recortar",
            ["Tool_Hand"] = "Mano de arrastre y compartir",
            ["Action_NewSnip"] = "Nueva captura",
            ["Action_Undo"] = "Deshacer",
            ["Action_Redo"] = "Rehacer",
            ["Action_Save"] = "Guardar",
            ["Action_Copy"] = "Copiar",
            ["Action_Share"] = "Compartir",
            ["Action_Export"] = "Exportar",
            ["Status_AutoSaving"] = "Guardado automático...",
            ["Status_Saved"] = "Guardado automáticamente",
            ["Status_Ready"] = "Listo",
            ["Tab_NewSnippet"] = "Captura {0}",
            ["Tab_Close"] = "Cerrar",
            ["Tab_CloseOthers"] = "Cerrar otros",
            ["Tab_CloseRight"] = "Cerrar a la derecha",
            ["Tab_Pin"] = "Fijar pestaña",
            ["Tab_Favorite"] = "Agregar a favoritos",
            ["Settings_Title"] = "Configuración de la aplicación",
            ["Settings_Language"] = "Idioma",
            ["Settings_Theme"] = "Tema y apariencia",
            ["Settings_Theme_System"] = "Predeterminado del sistema",
            ["Settings_Theme_Light"] = "Claro",
            ["Settings_Theme_Dark"] = "Oscuro",
            ["Settings_Theme_Mica"] = "Mica translúcida",
            ["Settings_AutoSave"] = "Guardado automático continuo",
            ["Settings_Debounce"] = "Retardo de guardado (ms)",
            ["Settings_VaultPath"] = "Ruta de almacenamiento",
            ["Settings_Hotkey"] = "Atajo de captura rápida",
            ["Settings_SaveButton"] = "Guardar cambios",
            ["Settings_ResetButton"] = "Restablecer valores",
            ["Settings_SavedSuccess"] = "Configuración guardada correctamente"
        },
        ["hi"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "कार्यक्षेत्र",
            ["Nav_Vault"] = "स्निप वॉल्ट",
            ["Nav_Settings"] = "सेटिंग्स",
            ["Nav_AuditLog"] = "ऑडिट लॉग",
            ["Tool_Select"] = "चयन करें",
            ["Tool_Pen"] = "पेन",
            ["Tool_Highlighter"] = "हाइलाइटर",
            ["Tool_Eraser"] = "इरेज़र",
            ["Tool_Ruler"] = "रूलर",
            ["Tool_Crop"] = "काटें",
            ["Tool_Hand"] = "खींचें और साझा करें",
            ["Action_NewSnip"] = "नया स्निप",
            ["Action_Undo"] = "पूर्ववत करें",
            ["Action_Redo"] = "फिर से करें",
            ["Action_Save"] = "सहेजें",
            ["Action_Copy"] = "कॉपी करें",
            ["Action_Share"] = "साझा करें",
            ["Action_Export"] = "निर्यात करें",
            ["Status_AutoSaving"] = "स्वतः सहेजा जा रहा है...",
            ["Status_Saved"] = "स्वतः सहेजा गया",
            ["Status_Ready"] = "तैयार",
            ["Tab_NewSnippet"] = "स्निप {0}",
            ["Tab_Close"] = "बंद करें",
            ["Tab_CloseOthers"] = "अन्य बंद करें",
            ["Tab_CloseRight"] = "दाईं ओर बंद करें",
            ["Tab_Pin"] = "पिन करें",
            ["Tab_Favorite"] = "पसंदीदा में जोड़ें",
            ["Settings_Title"] = "एप्लिकेशन सेटिंग्स",
            ["Settings_Language"] = "भाषा",
            ["Settings_Theme"] = "थीम और दिखावट",
            ["Settings_Theme_System"] = "सिस्टम डिफ़ॉल्ट",
            ["Settings_Theme_Light"] = "हल्का",
            ["Settings_Theme_Dark"] = "गहरा",
            ["Settings_Theme_Mica"] = "मीका पारभासी",
            ["Settings_AutoSave"] = "निरंतर स्वतः सहेजें",
            ["Settings_Debounce"] = "सहेजने का अंतराल (ms)",
            ["Settings_VaultPath"] = "संग्रह फ़ोल्डर",
            ["Settings_Hotkey"] = "त्वरित स्निप शॉर्टकट",
            ["Settings_SaveButton"] = "सेटिंग्स सहेजें",
            ["Settings_ResetButton"] = "रीसेट करें",
            ["Settings_SavedSuccess"] = "सेटिंग्स सफलतापूर्वक सहेजी गईं"
        },
        ["zh"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "工作区",
            ["Nav_Vault"] = "截图库",
            ["Nav_Settings"] = "设置",
            ["Nav_AuditLog"] = "审计日志",
            ["Tool_Select"] = "选择",
            ["Tool_Pen"] = "画笔",
            ["Tool_Highlighter"] = "荧光笔",
            ["Tool_Eraser"] = "橡皮擦",
            ["Tool_Ruler"] = "标尺",
            ["Tool_Crop"] = "裁剪",
            ["Tool_Hand"] = "拖拽与共享手势",
            ["Action_NewSnip"] = "新建截图",
            ["Action_Undo"] = "撤销",
            ["Action_Redo"] = "重做",
            ["Action_Save"] = "保存",
            ["Action_Copy"] = "复制",
            ["Action_Share"] = "分享",
            ["Action_Export"] = "导出",
            ["Status_AutoSaving"] = "正在自动保存...",
            ["Status_Saved"] = "已自动保存",
            ["Status_Ready"] = "就绪",
            ["Tab_NewSnippet"] = "截图 {0}",
            ["Tab_Close"] = "关闭",
            ["Tab_CloseOthers"] = "关闭其他",
            ["Tab_CloseRight"] = "关闭右侧",
            ["Tab_Pin"] = "固定标签页",
            ["Tab_Favorite"] = "添加到收藏夹",
            ["Settings_Title"] = "应用设置",
            ["Settings_Language"] = "语言",
            ["Settings_Theme"] = "主题与外观",
            ["Settings_Theme_System"] = "跟随系统",
            ["Settings_Theme_Light"] = "浅色",
            ["Settings_Theme_Dark"] = "深色",
            ["Settings_Theme_Mica"] = "云母半透明",
            ["Settings_AutoSave"] = "持续自动保存",
            ["Settings_Debounce"] = "防抖间隔 (毫秒)",
            ["Settings_VaultPath"] = "存储路径",
            ["Settings_Hotkey"] = "截图快捷键",
            ["Settings_SaveButton"] = "保存设置",
            ["Settings_ResetButton"] = "恢复默认值",
            ["Settings_SavedSuccess"] = "设置已成功保存"
        },
        ["fr"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Espace de travail",
            ["Nav_Vault"] = "Galerie des captures",
            ["Nav_Settings"] = "Paramètres",
            ["Nav_AuditLog"] = "Journal d'audit",
            ["Tool_Select"] = "Sélectionner",
            ["Tool_Pen"] = "Stylo",
            ["Tool_Highlighter"] = "Surligneur",
            ["Tool_Eraser"] = "Gomme",
            ["Tool_Ruler"] = "Règle",
            ["Tool_Crop"] = "Recadrer",
            ["Tool_Hand"] = "Main glisser-partager",
            ["Action_NewSnip"] = "Nouvelle capture",
            ["Action_Undo"] = "Annuler",
            ["Action_Redo"] = "Rétablir",
            ["Action_Save"] = "Enregistrer",
            ["Action_Copy"] = "Copier",
            ["Action_Share"] = "Partager",
            ["Action_Export"] = "Exporter",
            ["Status_AutoSaving"] = "Enregistrement automatique...",
            ["Status_Saved"] = "Enregistré automatiquement",
            ["Status_Ready"] = "Prêt",
            ["Tab_NewSnippet"] = "Capture {0}",
            ["Tab_Close"] = "Fermer",
            ["Tab_CloseOthers"] = "Fermer les autres",
            ["Tab_CloseRight"] = "Fermer à droite",
            ["Tab_Pin"] = "Épingler l'onglet",
            ["Tab_Favorite"] = "Ajouter aux favoris",
            ["Settings_Title"] = "Paramètres de l'application",
            ["Settings_Language"] = "Langue",
            ["Settings_Theme"] = "Thème et apparence",
            ["Settings_Theme_System"] = "Par défaut du système",
            ["Settings_Theme_Light"] = "Clair",
            ["Settings_Theme_Dark"] = "Sombre",
            ["Settings_Theme_Mica"] = "Mica translucide",
            ["Settings_AutoSave"] = "Enregistrement automatique continu",
            ["Settings_Debounce"] = "Délai de temporisation (ms)",
            ["Settings_VaultPath"] = "Dossier de stockage",
            ["Settings_Hotkey"] = "Raccourci de capture",
            ["Settings_SaveButton"] = "Enregistrer les paramètres",
            ["Settings_ResetButton"] = "Réinitialiser",
            ["Settings_SavedSuccess"] = "Paramètres enregistrés avec succès"
        },
        ["de"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Arbeitsbereich",
            ["Nav_Vault"] = "Aufnahmen-Tresor",
            ["Nav_Settings"] = "Einstellungen",
            ["Nav_AuditLog"] = "Audit-Protokoll",
            ["Tool_Select"] = "Auswählen",
            ["Tool_Pen"] = "Stift",
            ["Tool_Highlighter"] = "Textmarker",
            ["Tool_Eraser"] = "Radiergummi",
            ["Tool_Ruler"] = "Lineal",
            ["Tool_Crop"] = "Zuschneiden",
            ["Tool_Hand"] = "Hand zum Ziehen & Teilen",
            ["Action_NewSnip"] = "Neuer Ausschnitt",
            ["Action_Undo"] = "Rückgängig",
            ["Action_Redo"] = "Wiederholen",
            ["Action_Save"] = "Speichern",
            ["Action_Copy"] = "Kopieren",
            ["Action_Share"] = "Teilen",
            ["Action_Export"] = "Exportieren",
            ["Status_AutoSaving"] = "Automatisches Speichern...",
            ["Status_Saved"] = "Automatisch gespeichert",
            ["Status_Ready"] = "Bereit",
            ["Tab_NewSnippet"] = "Ausschnitt {0}",
            ["Tab_Close"] = "Schließen",
            ["Tab_CloseOthers"] = "Andere schließen",
            ["Tab_CloseRight"] = "Rechts schließen",
            ["Tab_Pin"] = "Tab anheften",
            ["Tab_Favorite"] = "Zu Favoriten hinzufügen",
            ["Settings_Title"] = "Programmeinstellungen",
            ["Settings_Language"] = "Sprache",
            ["Settings_Theme"] = "Design & Erscheinungsbild",
            ["Settings_Theme_System"] = "Systemstandard",
            ["Settings_Theme_Light"] = "Hell",
            ["Settings_Theme_Dark"] = "Dunkel",
            ["Settings_Theme_Mica"] = "Mica Transluzent",
            ["Settings_AutoSave"] = "Kontinuierliches automatisches Speichern",
            ["Settings_Debounce"] = "Speicherverzögerung (ms)",
            ["Settings_VaultPath"] = "Speicherordner",
            ["Settings_Hotkey"] = "Schnellerfassungs-Hotkey",
            ["Settings_SaveButton"] = "Einstellungen speichern",
            ["Settings_ResetButton"] = "Auf Standard zurücksetzen",
            ["Settings_SavedSuccess"] = "Einstellungen erfolgreich gespeichert"
        },
        ["ru"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Рабочая область",
            ["Nav_Vault"] = "Хранилище снимков",
            ["Nav_Settings"] = "Настройки",
            ["Nav_AuditLog"] = "Журнал аудита",
            ["Tool_Select"] = "Выделение",
            ["Tool_Pen"] = "Перо",
            ["Tool_Highlighter"] = "Маркер",
            ["Tool_Eraser"] = "Ластик",
            ["Tool_Ruler"] = "Линейка",
            ["Tool_Crop"] = "Обрезка",
            ["Tool_Hand"] = "Рука перетаскивания и отправки",
            ["Action_NewSnip"] = "Создать снимок",
            ["Action_Undo"] = "Отменить",
            ["Action_Redo"] = "Повторить",
            ["Action_Save"] = "Сохранить",
            ["Action_Copy"] = "Копировать",
            ["Action_Share"] = "Поделиться",
            ["Action_Export"] = "Экспорт",
            ["Status_AutoSaving"] = "Автосохранение...",
            ["Status_Saved"] = "Автосохранено",
            ["Status_Ready"] = "Готово",
            ["Tab_NewSnippet"] = "Снимок {0}",
            ["Tab_Close"] = "Закрыть",
            ["Tab_CloseOthers"] = "Закрыть остальные",
            ["Tab_CloseRight"] = "Закрыть справа",
            ["Tab_Pin"] = "Закрепить вкладку",
            ["Tab_Favorite"] = "Добавить в избранное",
            ["Settings_Title"] = "Настройки приложения",
            ["Settings_Language"] = "Язык",
            ["Settings_Theme"] = "Тема и оформление",
            ["Settings_Theme_System"] = "Системная",
            ["Settings_Theme_Light"] = "Светлая",
            ["Settings_Theme_Dark"] = "Темная",
            ["Settings_Theme_Mica"] = "Полупрозрачный Mica",
            ["Settings_AutoSave"] = "Непрерывное автосохранение",
            ["Settings_Debounce"] = "Задержка сохранения (мс)",
            ["Settings_VaultPath"] = "Папка хранилища",
            ["Settings_Hotkey"] = "Горячая клавиша снимка",
            ["Settings_SaveButton"] = "Сохранить настройки",
            ["Settings_ResetButton"] = "Сбросить по умолчанию",
            ["Settings_SavedSuccess"] = "Настройки успешно сохранены"
        },
        ["ja"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "ワークスペース",
            ["Nav_Vault"] = "切り取り保管庫",
            ["Nav_Settings"] = "設定",
            ["Nav_AuditLog"] = "監査ログ",
            ["Tool_Select"] = "選択",
            ["Tool_Pen"] = "ペン",
            ["Tool_Highlighter"] = "蛍光ペン",
            ["Tool_Eraser"] = "消しゴム",
            ["Tool_Ruler"] = "定規",
            ["Tool_Crop"] = "トリミング",
            ["Tool_Hand"] = "ドラッグ＆共有の手",
            ["Action_NewSnip"] = "新規切り取り",
            ["Action_Undo"] = "元に戻す",
            ["Action_Redo"] = "やり直し",
            ["Action_Save"] = "保存",
            ["Action_Copy"] = "コピー",
            ["Action_Share"] = "共有",
            ["Action_Export"] = "エクスポート",
            ["Status_AutoSaving"] = "自動保存中...",
            ["Status_Saved"] = "自動保存完了",
            ["Status_Ready"] = "準備完了",
            ["Tab_NewSnippet"] = "スニップ {0}",
            ["Tab_Close"] = "閉じる",
            ["Tab_CloseOthers"] = "他を閉じる",
            ["Tab_CloseRight"] = "右側を閉じる",
            ["Tab_Pin"] = "タブを固定",
            ["Tab_Favorite"] = "お気に入りに追加",
            ["Settings_Title"] = "アプリケーション設定",
            ["Settings_Language"] = "言語",
            ["Settings_Theme"] = "テーマと外観",
            ["Settings_Theme_System"] = "システム標準",
            ["Settings_Theme_Light"] = "ライト",
            ["Settings_Theme_Dark"] = "ダーク",
            ["Settings_Theme_Mica"] = "Mica 半透明",
            ["Settings_AutoSave"] = "継続的自動保存",
            ["Settings_Debounce"] = "保存遅延時間 (ミリ秒)",
            ["Settings_VaultPath"] = "保存先フォルダ",
            ["Settings_Hotkey"] = "クイック切り取りショートカット",
            ["Settings_SaveButton"] = "設定を保存",
            ["Settings_ResetButton"] = "初期状態に戻す",
            ["Settings_SavedSuccess"] = "設定が正常に保存されました"
        },
        ["pt"] = new Dictionary<string, string>
        {
            ["App_Title"] = "DropCut",
            ["Nav_Workspace"] = "Área de trabalho",
            ["Nav_Vault"] = "Galeria de capturas",
            ["Nav_Settings"] = "Configurações",
            ["Nav_AuditLog"] = "Registro de auditoria",
            ["Tool_Select"] = "Selecionar",
            ["Tool_Pen"] = "Caneta",
            ["Tool_Highlighter"] = "Marcador",
            ["Tool_Eraser"] = "Borracha",
            ["Tool_Ruler"] = "Régua",
            ["Tool_Crop"] = "Recortar",
            ["Tool_Hand"] = "Mão para arrastar e compartilhar",
            ["Action_NewSnip"] = "Nova captura",
            ["Action_Undo"] = "Desfazer",
            ["Action_Redo"] = "Refazer",
            ["Action_Save"] = "Salvar",
            ["Action_Copy"] = "Copiar",
            ["Action_Share"] = "Compartilhar",
            ["Action_Export"] = "Exportar",
            ["Status_AutoSaving"] = "Salvando automaticamente...",
            ["Status_Saved"] = "Salvo automaticamente",
            ["Status_Ready"] = "Pronto",
            ["Tab_NewSnippet"] = "Captura {0}",
            ["Tab_Close"] = "Fechar",
            ["Tab_CloseOthers"] = "Fechar outros",
            ["Tab_CloseRight"] = "Fechar à direita",
            ["Tab_Pin"] = "Fixar guia",
            ["Tab_Favorite"] = "Adicionar aos favoritos",
            ["Settings_Title"] = "Configurações do aplicativo",
            ["Settings_Language"] = "Idioma",
            ["Settings_Theme"] = "Tema e aparência",
            ["Settings_Theme_System"] = "Padrão do sistema",
            ["Settings_Theme_Light"] = "Claro",
            ["Settings_Theme_Dark"] = "Escuro",
            ["Settings_Theme_Mica"] = "Mica translúcida",
            ["Settings_AutoSave"] = "Salvamento automático contínuo",
            ["Settings_Debounce"] = "Atraso de salvamento (ms)",
            ["Settings_VaultPath"] = "Pasta de armazenamento",
            ["Settings_Hotkey"] = "Atalho de captura rápida",
            ["Settings_SaveButton"] = "Salvar configurações",
            ["Settings_ResetButton"] = "Restaurar padrões",
            ["Settings_SavedSuccess"] = "Configurações salvas com sucesso"
        }
    };

    public string CurrentLanguageCode => _currentLanguageCode;

    public bool IsRtl => _currentLanguageCode.Equals("ar", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<LanguageOption> SupportedLanguages => _supportedLanguages;

    public LocalizationService(IEventBus eventBus, IUserPreferenceRepository? preferenceRepository = null)
    {
        _eventBus = Guard.AgainstNull(eventBus, nameof(eventBus));
        _preferenceRepository = preferenceRepository;
    }

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (_dictionaries.TryGetValue(_currentLanguageCode, out var dict) && dict.TryGetValue(key, out var value))
        {
            return value;
        }

        // Fallback to Arabic (the master default)
        if (_dictionaries.TryGetValue("ar", out var arDict) && arDict.TryGetValue(key, out var arVal))
        {
            return arVal;
        }

        return key;
    }

    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        if (args is null || args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public async Task SetLanguageAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(languageCode, nameof(languageCode));

        var target = _supportedLanguages.FirstOrDefault(l => l.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        _currentLanguageCode = target.Code;

        if (_preferenceRepository is not null)
        {
            try
            {
                await _preferenceRepository.SetValueAsync("LanguageCode", _currentLanguageCode, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Non-fatal persistence error
            }
        }

        await _eventBus.PublishAsync(new LanguageChangedEvent(_currentLanguageCode, target.IsRtl), cancellationToken).ConfigureAwait(false);
    }
}
