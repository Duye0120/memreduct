using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace MemReduct.Fluent;

internal static class UiText
{
    private static ResourceLoader? _resources;
    private static readonly string SystemLanguage = CultureInfo.CurrentUICulture.Name;
    public static string LanguageTag { get; private set; } = "en-US";

    internal static string ResolveLanguage(string selected, string systemLanguage) => selected == "System"
        ? (systemLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US")
        : selected;

    public static void Initialize(string selected)
    {
        LanguageTag = ResolveSelectedLanguage(selected);
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = LanguageTag;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(LanguageTag);
        _resources = new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), "Resources");
    }

    internal static string ResolveSelectedLanguage(string selected) => ResolveLanguage(selected, SystemLanguage);

    public static string Get(string key, params object[] values)
    {
        _resources ??= new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), "Resources");
        var text = _resources.GetString(key.Replace('.', '/'));
        if (string.IsNullOrEmpty(text)) throw new InvalidOperationException("Missing translation: " + key);
        return values.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, values);
    }
}
