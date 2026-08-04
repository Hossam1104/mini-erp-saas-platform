import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type Language = 'en' | 'ar';
export type TranslationKey = keyof typeof translations.en;

const translations = {
  en: {
    appName: 'Mini ERP control plane',
    appKicker: 'Foundation workspace',
    signInTitle: 'Sign in to your workspace',
    signInLead: 'Use your approved account. Access is confirmed by the server.',
    login: 'Email or username',
    password: 'Password',
    signIn: 'Sign in',
    signingIn: 'Signing in…',
    signInError: 'We could not sign you in. Check your details and try again.',
    sessionExpired: 'Your session has expired or was revoked. Sign in again.',
    networkError: 'The service is unavailable right now. Try again shortly.',
    requestError: 'The request could not be completed safely.',
    accessDenied: 'This action is not available for your current access.',
    staleContext: 'The available contexts changed. Reload the list and try again.',
    validationError: 'Check the highlighted fields and try again.',
    language: 'Language',
    switchToArabic: 'العربية',
    switchToEnglish: 'English',
    overview: 'Overview',
    switchContext: 'Switch workspace',
    signOut: 'Sign out',
    signingOut: 'Signing out…',
    signOutFailed: 'Sign-out could not be confirmed. Your session may still be active. Please try again.',
    signedInAs: 'Signed in as',
    platformGovernance: 'Platform governance',
    tenantWorkspace: 'Tenant workspace',
    ordinaryMembership: 'Tenant membership',
    supportGrant: 'Support grant',
    contextTitle: 'Choose a workspace',
    contextLead: 'Only contexts returned by the server can be selected.',
    contextEmpty: 'No authorized contexts are available for this session.',
    contextLoading: 'Loading authorized contexts…',
    contextSwitching: 'Confirming workspace…',
    currentWorkspace: 'Current workspace',
    chooseWorkspace: 'Select a workspace',
    shellWelcome: 'A calm starting point for approved ERP work.',
    shellSafeBoundary: 'Business records appear only after server-confirmed context.',
    shellNoBusinessData: 'No business data is loaded in this foundation slice.',
    sessionState: 'Session state',
    active: 'Active',
    restricted: 'Restricted',
    empty: 'Empty',
    loading: 'Loading',
    error: 'Error',
    skipToContent: 'Skip to content',
    menu: 'Navigation',
    helpText: 'Foundation shell · Release 1 B2B ERP',
  },
  ar: {
    appName: 'منصة ميني ERP',
    appKicker: 'مساحة الأساس',
    signInTitle: 'سجّل الدخول إلى مساحة العمل',
    signInLead: 'استخدم الحساب المعتمد. الخادم هو من يؤكد الصلاحية.',
    login: 'البريد الإلكتروني أو اسم المستخدم',
    password: 'كلمة المرور',
    signIn: 'تسجيل الدخول',
    signingIn: 'جارٍ تسجيل الدخول…',
    signInError: 'تعذّر تسجيل الدخول. تحقّق من البيانات وحاول مجددًا.',
    sessionExpired: 'انتهت الجلسة أو أُلغيت. سجّل الدخول مجددًا.',
    networkError: 'الخدمة غير متاحة حاليًا. حاول بعد قليل.',
    requestError: 'تعذّر إكمال الطلب بأمان.',
    accessDenied: 'هذا الإجراء غير متاح لصلاحياتك الحالية.',
    staleContext: 'تغيّرت مساحات العمل المتاحة. أعد تحميل القائمة وحاول مجددًا.',
    validationError: 'تحقّق من الحقول المعلّمة وحاول مجددًا.',
    language: 'اللغة',
    switchToArabic: 'العربية',
    switchToEnglish: 'English',
    overview: 'نظرة عامة',
    switchContext: 'تبديل مساحة العمل',
    signOut: 'تسجيل الخروج',
    signingOut: 'جارٍ تسجيل الخروج…',
    signOutFailed: 'تعذّر تأكيد تسجيل الخروج. قد تظل جلستك نشطة. يُرجى المحاولة مرة أخرى.',
    signedInAs: 'تم تسجيل الدخول باسم',
    platformGovernance: 'حوكمة المنصة',
    tenantWorkspace: 'مساحة العميل',
    ordinaryMembership: 'عضوية العميل',
    supportGrant: 'منحة دعم',
    contextTitle: 'اختر مساحة عمل',
    contextLead: 'يمكن اختيار المساحات التي يعيدها الخادم فقط.',
    contextEmpty: 'لا توجد مساحات عمل معتمدة لهذه الجلسة.',
    contextLoading: 'جارٍ تحميل المساحات المعتمدة…',
    contextSwitching: 'جارٍ تأكيد مساحة العمل…',
    currentWorkspace: 'مساحة العمل الحالية',
    chooseWorkspace: 'اختر مساحة عمل',
    shellWelcome: 'بداية هادئة للعمل المعتمد على نظام ERP.',
    shellSafeBoundary: 'تظهر السجلات بعد تأكيد السياق من الخادم فقط.',
    shellNoBusinessData: 'لا يتم تحميل بيانات أعمال في شريحة الأساس هذه.',
    sessionState: 'حالة الجلسة',
    active: 'نشطة',
    restricted: 'مقيّدة',
    empty: 'فارغة',
    loading: 'جارٍ التحميل',
    error: 'خطأ',
    skipToContent: 'تخطى إلى المحتوى',
    menu: 'التنقل',
    helpText: 'واجهة الأساس · ERP للأعمال فقط في الإصدار الأول',
  },
} as const;

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly document = inject(DOCUMENT);
  readonly language = signal<Language>('en');

  constructor() {
    this.applyDocumentSettings('en');
  }

  text(key: TranslationKey): string {
    return translations[this.language()][key];
  }

  setLanguage(language: Language): void {
    this.language.set(language);
    this.applyDocumentSettings(language);
  }

  toggle(): void {
    this.setLanguage(this.language() === 'en' ? 'ar' : 'en');
  }

  private applyDocumentSettings(language: Language): void {
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
  }
}
