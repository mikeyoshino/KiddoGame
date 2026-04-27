using Kiddo.Web.Models;
using Microsoft.AspNetCore.Http;

namespace Kiddo.Web.Services;

public class LanguageService
{
    public Lang Current { get; private set; }
    public event Action? OnChanged;

    public LanguageService(IHttpContextAccessor httpContextAccessor)
    {
        var cookie = httpContextAccessor.HttpContext?.Request.Cookies["lang"];
        Current = cookie == "en" ? Lang.English : Lang.Thai;
    }

    public void SetLanguage(Lang lang)
    {
        Current = lang;
        OnChanged?.Invoke();
    }
}
