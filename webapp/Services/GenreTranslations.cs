using Kiddo.Web.Models;

namespace Kiddo.Web.Services;

public static class GenreTranslations
{
    private static readonly Dictionary<string, string> _thaiMap = new()
    {
        ["Casual"]            = "แคชวล",
        ["Puzzle"]            = "ปริศนา",
        ["Adventure"]         = "การผจญภัย",
        ["Racing & Driving"]  = "แข่งรถ",
        ["Simulation"]        = "จำลองสถานการณ์",
        ["Dress-up"]          = "แต่งตัว",
        ["Agility"]           = "ความคล่องแคล่ว",
        ["Shooter"]           = "ยิงปืน",
        ["Battle"]            = "ต่อสู้",
        ["Match-3"]           = "จับคู่สาม",
        ["Strategy"]          = "กลยุทธ์",
        ["Mahjong & Connect"] = "มาจองและต่อเชื่อม",
        [".IO"]               = "ไอโอ",
        ["Art"]               = "ศิลปะ",
        ["Merge"]             = "รวมไอเทม",
        ["Sports"]            = "กีฬา",
        ["Cards"]             = "เกมไพ่",
        ["Educational"]       = "การศึกษา",
        ["Bubble Shooter"]    = "ยิงฟองสบู่",
        ["Football"]          = "ฟุตบอล",
        ["Cooking"]           = "ทำอาหาร",
        ["Care"]              = "ดูแลเอาใจใส่",
        ["Boardgames"]        = "เกมกระดาน",
        ["Basketball"]        = "บาสเกตบอล",
        ["Quiz"]              = "ทายปัญหา",
        ["Jigsaw"]            = "จิ๊กซอว์",
    };

    public static string Translate(string englishKey, Lang lang) =>
        lang == Lang.Thai && _thaiMap.TryGetValue(englishKey, out var thai)
            ? thai : englishKey;
}
