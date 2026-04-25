using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public static class Strings
{
    private static readonly Dictionary<string, (string Thai, string English)> _table = new()
    {
        ["search_placeholder"] = ("ค้นหาเกมที่อยากเล่น...",                "Search for a game..."),
        ["all_categories"]     = ("🌟 ทั้งหมด",                             "🌟 All"),
        ["all_games"]          = ("เกมทั้งหมด",                             "All Games"),
        ["items_count"]        = ("{0} รายการ",                             "{0} items"),
        ["loading"]            = ("กำลังโหลด...",                           "Loading..."),
        ["no_results"]         = ("ไม่พบเกมที่คุณค้นหา ลองเปลี่ยนคำดูนะ", "No games found. Try a different search."),
        ["prev_page"]          = ("← ก่อนหน้า",                            "← Prev"),
        ["next_page"]          = ("ถัดไป →",                                "Next →"),
        ["page_of"]            = ("หน้า {0} / {1}",                         "Page {0} / {1}"),
        ["by_company"]         = ("โดย {0}",                                "by {0}"),
        ["how_to_play"]        = ("วิธีเล่น",                               "How to play"),
        ["similar_games"]      = ("เกมที่คล้ายกัน",                         "Similar games"),
        ["game_not_found"]     = ("ไม่พบเกมนี้",                            "Game not found"),
        ["back_home"]          = ("← กลับหน้าแรก",                         "← Back to home"),
        ["tagline"]            = ("เกมสนุกๆ สำหรับเด็กทุกวัย 🎮",           "Fun games for kids of all ages 🎮"),
        ["nav_home"]           = ("หน้าแรก",                                "Home"),
        ["goto_page"]          = ("ไปหน้า",                                "Go to page"),
        ["favorites"]          = ("รายการโปรด",                           "Favorites"),
        ["no_favorites"]       = ("ยังไม่มีเกมโปรด เพิ่มได้เลย!",        "No favorites yet. Start adding some!"),
        ["filters"]            = ("ตัวกรอง",                              "Filters"),
        ["genre"]              = ("ประเภท",                               "Genre"),
        ["nav_about"]          = ("เกี่ยวกับเรา",                         "About"),
    };

    public static string Get(Lang lang, string key, params object[] args)
    {
        if (!_table.TryGetValue(key, out var pair)) return key;
        var template = lang == Lang.Thai ? pair.Thai : pair.English;
        return args.Length > 0 ? string.Format(template, args) : template;
    }
}
