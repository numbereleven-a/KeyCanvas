using System.Globalization;

namespace KeyCanvas;

internal enum AppLanguage { English, Russian }

internal static class UiText
{
    internal static AppLanguage WindowsLanguage => FromCulture(CultureInfo.CurrentUICulture);
    internal static AppLanguage FromCulture(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "ru" ? AppLanguage.Russian : AppLanguage.English;
    internal static string Pick(AppLanguage language, string english, string russian) =>
        language == AppLanguage.Russian ? russian : english;
    internal static string SettingsTitle(AppLanguage language) =>
        Pick(language, "KeyCanvas — Settings", "KeyCanvas — Настройки");
}
