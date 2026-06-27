namespace Raqmi.Client.Localization;

public static class AppStrings
{
    public static string Get(AppLocale locale, string key) =>
        Table.TryGetValue(locale, out var map) && map.TryGetValue(key, out var value)
            ? value
            : Table[AppLocale.Fr][key];

    public static string Format(AppLocale locale, string key, params (string Name, string Value)[] vars)
    {
        var text = Get(locale, key);
        foreach (var (name, value) in vars)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    public static string Family(AppLocale locale, string familyKey) =>
        Get(locale, "family." + familyKey.ToLowerInvariant());

    private static readonly Dictionary<AppLocale, Dictionary<string, string>> Table = new()
    {
        [AppLocale.Fr] = new()
        {
            ["appName"] = "Raqmi System",
            ["appTagline"] = "ERP modulaire multi-client",
            ["serverSettings"] = "Configuration serveur",
            ["serverSettingsHint"] = "URL complète ou IP locale (port 3000 par défaut).",
            ["serverUrl"] = "Adresse du serveur",
            ["serverUrlExamples"] = "Ex. : 192.168.1.10 · 192.168.1.10:3000 · http://localhost:3000",
            ["serverUnreachable"] = "Serveur inaccessible. Vérifiez l'adresse et que Raqmi Server est démarré.",
            ["saveContinue"] = "Enregistrer et continuer",
            ["email"] = "Email",
            ["password"] = "Mot de passe",
            ["login"] = "Se connecter",
            ["demoAccount"] = "Compte demo :",
            ["loginAsideTitle"] = "Modules activés par licence",
            ["loginAsideText"] = "Le client affiche uniquement les modules autorisés par le serveur, selon le pack Starter, Professional ou Enterprise.",
            ["loginAsideItem1"] = "23 modules ERP",
            ["loginAsideItem2"] = "Contrôle licence côté serveur",
            ["loginAsideItem3"] = "Mode demo sans base de données",
            ["logout"] = "Déconnexion",
            ["dashboard"] = "TABLEAU DE BORD",
            ["welcome"] = "Bienvenue, {name}",
            ["packSummary"] = "Pack {pack} — {enabled} modules actifs sur {total}",
            ["license"] = "Licence",
            ["licenseValid"] = "Valide",
            ["licenseInvalid"] = "Refusée",
            ["licenseDemo"] = "Licence Professional — mode demo",
            ["activeModules"] = "Modules actifs",
            ["families"] = "Familles",
            ["pack"] = "Pack",
            ["mode"] = "Mode",
            ["readonly"] = "Lecture seule",
            ["normal"] = "Normal",
            ["availableModules"] = "Modules disponibles",
            ["modulesHint"] = "Seuls les modules inclus dans votre licence sont activés.",
            ["active"] = "Actif",
            ["blocked"] = "Bloqué",
            ["language"] = "Langue",
            ["family.core"] = "Core",
            ["family.finance"] = "Finance",
            ["family.hr"] = "RH",
            ["family.operations"] = "Opérations",
            ["family.specific"] = "Spécifique",
            ["family.system"] = "Système",
        },
        [AppLocale.En] = new()
        {
            ["appName"] = "Raqmi System",
            ["appTagline"] = "Modular multi-tenant ERP",
            ["serverSettings"] = "Server configuration",
            ["serverSettingsHint"] = "Full URL or local IP (port 3000 by default).",
            ["serverUrl"] = "Server address",
            ["serverUrlExamples"] = "e.g. 192.168.1.10 · 192.168.1.10:3000 · http://localhost:3000",
            ["serverUnreachable"] = "Server unreachable. Check the address and ensure Raqmi Server is running.",
            ["saveContinue"] = "Save and continue",
            ["email"] = "Email",
            ["password"] = "Password",
            ["login"] = "Sign in",
            ["demoAccount"] = "Demo account:",
            ["loginAsideTitle"] = "License-enabled modules",
            ["loginAsideText"] = "The client shows only modules allowed by the server, according to Starter, Professional or Enterprise packs.",
            ["loginAsideItem1"] = "23 ERP modules",
            ["loginAsideItem2"] = "Server-side license control",
            ["loginAsideItem3"] = "Demo mode without database",
            ["logout"] = "Sign out",
            ["dashboard"] = "DASHBOARD",
            ["welcome"] = "Welcome, {name}",
            ["packSummary"] = "{pack} pack — {enabled} active modules out of {total}",
            ["license"] = "License",
            ["licenseValid"] = "Valid",
            ["licenseInvalid"] = "Denied",
            ["licenseDemo"] = "Professional license — demo mode",
            ["activeModules"] = "Active modules",
            ["families"] = "Families",
            ["pack"] = "Pack",
            ["mode"] = "Mode",
            ["readonly"] = "Read-only",
            ["normal"] = "Normal",
            ["availableModules"] = "Available modules",
            ["modulesHint"] = "Only modules included in your license are enabled.",
            ["active"] = "Active",
            ["blocked"] = "Blocked",
            ["language"] = "Language",
            ["family.core"] = "Core",
            ["family.finance"] = "Finance",
            ["family.hr"] = "HR",
            ["family.operations"] = "Operations",
            ["family.specific"] = "Specific",
            ["family.system"] = "System",
        },
        [AppLocale.Ar] = new()
        {
            ["appName"] = "نظام رقمي",
            ["appTagline"] = "نظام ERP معياري متعدد العملاء",
            ["serverSettings"] = "إعدادات الخادم",
            ["serverSettingsHint"] = "رابط كامل أو IP محلي (المنفذ 3000 افتراضياً).",
            ["serverUrl"] = "عنوان الخادم",
            ["serverUrlExamples"] = "مثال: 192.168.1.10 · 192.168.1.10:3000 · http://localhost:3000",
            ["serverUnreachable"] = "تعذّر الوصول إلى الخادم. تحقق من العنوان وتأكد أن الخادم يعمل.",
            ["saveContinue"] = "حفظ ومتابعة",
            ["email"] = "البريد الإلكتروني",
            ["password"] = "كلمة المرور",
            ["login"] = "تسجيل الدخول",
            ["demoAccount"] = "حساب تجريبي:",
            ["loginAsideTitle"] = "الوحدات المفعّلة بالترخيص",
            ["loginAsideText"] = "يعرض العميل فقط الوحدات المسموح بها من الخادم حسب باقة Starter أو Professional أو Enterprise.",
            ["loginAsideItem1"] = "23 وحدة ERP",
            ["loginAsideItem2"] = "التحكم بالترخيص من الخادم",
            ["loginAsideItem3"] = "وضع تجريبي بدون قاعدة بيانات",
            ["logout"] = "تسجيل الخروج",
            ["dashboard"] = "لوحة التحكم",
            ["welcome"] = "مرحباً، {name}",
            ["packSummary"] = "باقة {pack} — {enabled} وحدة مفعّلة من {total}",
            ["license"] = "الترخيص",
            ["licenseValid"] = "صالح",
            ["licenseInvalid"] = "مرفوض",
            ["licenseDemo"] = "ترخيص Professional — وضع تجريبي",
            ["activeModules"] = "الوحدات النشطة",
            ["families"] = "العائلات",
            ["pack"] = "الباقة",
            ["mode"] = "الوضع",
            ["readonly"] = "قراءة فقط",
            ["normal"] = "عادي",
            ["availableModules"] = "الوحدات المتاحة",
            ["modulesHint"] = "يتم تفعيل الوحدات المدرجة في ترخيصك فقط.",
            ["active"] = "نشط",
            ["blocked"] = "محظور",
            ["language"] = "اللغة",
            ["family.core"] = "أساسي",
            ["family.finance"] = "مالية",
            ["family.hr"] = "موارد بشرية",
            ["family.operations"] = "عمليات",
            ["family.specific"] = "مخصص",
            ["family.system"] = "نظام",
        },
    };
}
