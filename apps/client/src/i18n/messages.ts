export type Locale = 'fr' | 'en' | 'ar';

export const LOCALE_LABELS: Record<Locale, string> = {
  fr: 'Français',
  en: 'English',
  ar: 'العربية',
};

export const LOCALE_DATE: Record<Locale, string> = {
  fr: 'fr-FR',
  en: 'en-GB',
  ar: 'ar-DZ',
};

export type Messages = {
  appName: string;
  appTagline: string;
  init: string;
  loading: string;
  sessionExpired: string;
  email: string;
  password: string;
  login: string;
  loggingIn: string;
  loginFailed: string;
  demoAccount: string;
  loginAsideTitle: string;
  loginAsideText: string;
  loginAsideItem1: string;
  loginAsideItem2: string;
  loginAsideItem3: string;
  serverSettings: string;
  serverSettingsHint: string;
  serverUrl: string;
  serverUrlPlaceholder: string;
  serverUrlExamples: string;
  serverUrlResolved: string;
  serverInvalid: string;
  serverUnreachable: string;
  testing: string;
  saveContinue: string;
  logout: string;
  dashboard: string;
  welcome: string;
  packSummary: string;
  license: string;
  licenseValid: string;
  licenseInvalid: string;
  expiresOn: string;
  activeModules: string;
  families: string;
  pack: string;
  mode: string;
  readonly: string;
  normal: string;
  availableModules: string;
  modulesHint: string;
  active: string;
  blocked: string;
  language: string;
  family: Record<'core' | 'finance' | 'hr' | 'operations' | 'specific' | 'system', string>;
};

const messages: Record<Locale, Messages> = {
  fr: {
    appName: 'Raqmi System',
    appTagline: 'ERP modulaire multi-client',
    init: 'Initialisation…',
    loading: 'Chargement de Raqmi System…',
    sessionExpired: 'Session expirée',
    email: 'Email',
    password: 'Mot de passe',
    login: 'Se connecter',
    loggingIn: 'Connexion…',
    loginFailed: 'Connexion impossible',
    demoAccount: 'Compte demo :',
    loginAsideTitle: 'Modules activés par licence',
    loginAsideText:
      'Le client affiche uniquement les modules autorisés par le serveur, selon le pack Starter, Professional ou Enterprise.',
    loginAsideItem1: '23 modules ERP',
    loginAsideItem2: 'Contrôle licence côté serveur',
    loginAsideItem3: 'Mode demo sans base de données',
    serverSettings: 'Configuration serveur',
    serverSettingsHint:
      'Indiquez l’adresse du Raqmi System Server : URL complète ou IP locale sur votre réseau.',
    serverUrl: 'Adresse du serveur',
    serverUrlPlaceholder: '192.168.1.10 ou http://localhost:3000',
    serverUrlExamples: 'Exemples : 192.168.1.10 · 192.168.1.10:3000 · http://serveur.local:3000',
    serverUrlResolved: 'Connexion via {url}',
    serverInvalid: 'Adresse serveur invalide.',
    serverUnreachable:
      'Serveur inaccessible. Vérifiez l’adresse et que Raqmi Server est démarré.',
    testing: 'Test en cours…',
    saveContinue: 'Enregistrer et continuer',
    logout: 'Déconnexion',
    dashboard: 'Tableau de bord',
    welcome: 'Bienvenue, {name}',
    packSummary: 'Pack {pack} — {enabled} modules actifs sur {total}',
    license: 'Licence',
    licenseValid: 'Valide',
    licenseInvalid: 'Refusée',
    expiresOn: 'Expire le {date}',
    activeModules: 'Modules actifs',
    families: 'Familles',
    pack: 'Pack',
    mode: 'Mode',
    readonly: 'Lecture seule',
    normal: 'Normal',
    availableModules: 'Modules disponibles',
    modulesHint: 'Seuls les modules inclus dans votre licence sont activés.',
    active: 'Actif',
    blocked: 'Bloqué',
    language: 'Langue',
    family: {
      core: 'Core',
      finance: 'Finance',
      hr: 'RH',
      operations: 'Opérations',
      specific: 'Spécifique',
      system: 'Système',
    },
  },
  en: {
    appName: 'Raqmi System',
    appTagline: 'Modular multi-tenant ERP',
    init: 'Starting…',
    loading: 'Loading Raqmi System…',
    sessionExpired: 'Session expired',
    email: 'Email',
    password: 'Password',
    login: 'Sign in',
    loggingIn: 'Signing in…',
    loginFailed: 'Unable to sign in',
    demoAccount: 'Demo account:',
    loginAsideTitle: 'License-enabled modules',
    loginAsideText:
      'The client shows only modules allowed by the server, according to Starter, Professional or Enterprise packs.',
    loginAsideItem1: '23 ERP modules',
    loginAsideItem2: 'Server-side license control',
    loginAsideItem3: 'Demo mode without database',
    serverSettings: 'Server configuration',
    serverSettingsHint:
      'Enter the Raqmi System Server address: full URL or local IP on your network.',
    serverUrl: 'Server address',
    serverUrlPlaceholder: '192.168.1.10 or http://localhost:3000',
    serverUrlExamples: 'Examples: 192.168.1.10 · 192.168.1.10:3000 · http://server.local:3000',
    serverUrlResolved: 'Connecting via {url}',
    serverInvalid: 'Invalid server address.',
    serverUnreachable:
      'Server unreachable. Check the address and ensure Raqmi Server is running.',
    testing: 'Testing…',
    saveContinue: 'Save and continue',
    logout: 'Sign out',
    dashboard: 'Dashboard',
    welcome: 'Welcome, {name}',
    packSummary: '{pack} pack — {enabled} active modules out of {total}',
    license: 'License',
    licenseValid: 'Valid',
    licenseInvalid: 'Denied',
    expiresOn: 'Expires on {date}',
    activeModules: 'Active modules',
    families: 'Families',
    pack: 'Pack',
    mode: 'Mode',
    readonly: 'Read-only',
    normal: 'Normal',
    availableModules: 'Available modules',
    modulesHint: 'Only modules included in your license are enabled.',
    active: 'Active',
    blocked: 'Blocked',
    language: 'Language',
    family: {
      core: 'Core',
      finance: 'Finance',
      hr: 'HR',
      operations: 'Operations',
      specific: 'Specific',
      system: 'System',
    },
  },
  ar: {
    appName: 'نظام رقمي',
    appTagline: 'نظام ERP معياري متعدد العملاء',
    init: 'جاري التهيئة…',
    loading: 'جاري تحميل نظام رقمي…',
    sessionExpired: 'انتهت الجلسة',
    email: 'البريد الإلكتروني',
    password: 'كلمة المرور',
    login: 'تسجيل الدخول',
    loggingIn: 'جاري الدخول…',
    loginFailed: 'تعذّر تسجيل الدخول',
    demoAccount: 'حساب تجريبي:',
    loginAsideTitle: 'الوحدات المفعّلة بالترخيص',
    loginAsideText:
      'يعرض العميل فقط الوحدات المسموح بها من الخادم حسب باقة Starter أو Professional أو Enterprise.',
    loginAsideItem1: '23 وحدة ERP',
    loginAsideItem2: 'التحكم بالترخيص من الخادم',
    loginAsideItem3: 'وضع تجريبي بدون قاعدة بيانات',
    serverSettings: 'إعدادات الخادم',
    serverSettingsHint: 'أدخل عنوان خادم Raqmi System: رابط كامل أو IP محلي على شبكتك.',
    serverUrl: 'عنوان الخادم',
    serverUrlPlaceholder: '192.168.1.10 أو http://localhost:3000',
    serverUrlExamples: 'أمثلة: 192.168.1.10 · 192.168.1.10:3000 · http://serveur.local:3000',
    serverUrlResolved: 'الاتصال عبر {url}',
    serverInvalid: 'عنوان الخادم غير صالح.',
    serverUnreachable: 'تعذّر الوصول إلى الخادم. تحقق من العنوان وتأكد أن الخادم يعمل.',
    testing: 'جاري الاختبار…',
    saveContinue: 'حفظ ومتابعة',
    logout: 'تسجيل الخروج',
    dashboard: 'لوحة التحكم',
    welcome: 'مرحباً، {name}',
    packSummary: 'باقة {pack} — {enabled} وحدة مفعّلة من {total}',
    license: 'الترخيص',
    licenseValid: 'صالح',
    licenseInvalid: 'مرفوض',
    expiresOn: 'ينتهي في {date}',
    activeModules: 'الوحدات النشطة',
    families: 'العائلات',
    pack: 'الباقة',
    mode: 'الوضع',
    readonly: 'قراءة فقط',
    normal: 'عادي',
    availableModules: 'الوحدات المتاحة',
    modulesHint: 'يتم تفعيل الوحدات المدرجة في ترخيصك فقط.',
    active: 'نشط',
    blocked: 'محظور',
    language: 'اللغة',
    family: {
      core: 'أساسي',
      finance: 'مالية',
      hr: 'موارد بشرية',
      operations: 'عمليات',
      specific: 'مخصص',
      system: 'نظام',
    },
  },
};

export function getMessages(locale: Locale): Messages {
  return messages[locale];
}

export function formatMessage(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, key: string) => String(vars[key] ?? ''));
}

export function detectLocale(): Locale {
  const lang = navigator.language.toLowerCase();
  if (lang.startsWith('ar')) return 'ar';
  if (lang.startsWith('en')) return 'en';
  return 'fr';
}
