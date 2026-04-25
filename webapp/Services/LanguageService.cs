using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public class LanguageService
{
    public Lang Current { get; private set; } = Lang.Thai;
    public event Action? OnChanged;

    public void SetLanguage(Lang lang)
    {
        Current = lang;
        OnChanged?.Invoke();
    }
}
