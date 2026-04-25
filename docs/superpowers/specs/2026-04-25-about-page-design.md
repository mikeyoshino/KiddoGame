# About Page Design

## Goal

Add a Thai-language About page at `/about` that introduces KiddoGame to Thai parents and kids, adds meaningful keyword-rich Thai content for SEO, and links from both the desktop and mobile nav.

## Target Audience & SEO Intent

Same audiences as the home page — Thai parents (safety-conscious) and kids (8–15). The About page targets informational queries like `KiddoGame คืออะไร`, `เกมเด็กออนไลน์ปลอดภัย`, `เกมออนไลน์ฟรีไม่ต้องดาวน์โหลด`. With ~400 words of Thai content across multiple H2 sections, the page gives Google enough signal to index it as a genuine, useful document.

## Architecture

Five files:

```
webapp/Components/Pages/About.razor        ← new page at /about
webapp/Components/Layout/TopNav.razor      ← add About link (desktop)
webapp/Components/Layout/MobileNav.razor   ← add About tab (mobile bottom bar)
webapp/Services/Strings.cs                 ← add nav_about entry
```

No new services, no database calls. Follows the same patterns as `Favorites.razor` and `GamePage.razor`.

SEO title, description, and JSON-LD for the About page are defined as `private const string` fields directly in `About.razor` — they are page-specific and won't be reused elsewhere, so they don't belong in `SeoMeta.cs`.

## About.razor

**Route:** `@page "/about"`  
**Render mode:** `@rendermode InteractiveServer`  
**Injects:** `LanguageService LangSvc`

### SEO Head

```
Title:       "เกี่ยวกับ KiddoGame - เกมออนไลน์สำหรับเด็กฟรี ปลอดภัย ไม่ต้องดาวน์โหลด"
Description: "KiddoGame รวมเกมออนไลน์ฟรีสำหรับเด็กกว่า 2,000 เกม ปลอดภัย ไม่ต้องดาวน์โหลด เล่นได้ทันทีบนเบราว์เซอร์ เหมาะสำหรับเด็กทุกวัย"
Canonical:   https://kiddogame.net/about
OgType:      website
JsonLd:      Organization schema (see below)
```

### JSON-LD — Organization

```json
{
  "@context": "https://schema.org",
  "@type": "Organization",
  "name": "KiddoGame",
  "url": "https://kiddogame.net",
  "description": "รวมเกมออนไลน์ฟรีสำหรับเด็กกว่า 2,000 เกม ปลอดภัย ไม่ต้องดาวน์โหลด"
}
```

### Page Sections (in order)

#### 1. Page heading (H1)
- Thai: `เกี่ยวกับ KiddoGame`
- English: `About KiddoGame`

#### 2. Mission paragraphs (2 short paragraphs)
- **Thai:**
  - Para 1: `KiddoGame คือแหล่งรวมเกมออนไลน์สำหรับเด็กฟรีที่ใหญ่ที่สุด เราเชื่อว่าเด็กทุกคนควรมีสิทธิ์เข้าถึงเกมที่สนุก ปลอดภัย และเสริมพัฒนาการ โดยไม่มีค่าใช้จ่ายใดๆ`
  - Para 2: `เราก่อตั้ง KiddoGame ขึ้นเพราะเชื่อว่าการเล่นเกมที่ดีช่วยพัฒนาความคิดสร้างสรรค์ ทักษะการแก้ปัญหา และความสนุกสนานของเด็กๆ ทุกเกมบน KiddoGame ผ่านการคัดสรรให้เหมาะสมกับเด็กทุกวัย`
- **English:**
  - Para 1: `KiddoGame is your go-to destination for free online kids games. We believe every child deserves access to fun, safe, and enriching games — completely free.`
  - Para 2: `We built KiddoGame because great games help kids develop creativity, problem-solving, and joy. Every game on KiddoGame is carefully chosen to be age-appropriate and safe.`

#### 3. Feature cards (2×2 grid, rounded-2xl bg-indigo-50)

| Icon | Thai heading | Thai sub | English heading | English sub |
|------|-------------|----------|-----------------|-------------|
| 🎮 | 2,000+ เกม | รวมเกมหลากหลายทุกประเภท | 2,000+ Games | Every genre covered |
| 🆓 | ฟรีทั้งหมด | ไม่มีค่าใช้จ่ายใดๆ | 100% Free | No cost, ever |
| 🛡️ | ปลอดภัยสำหรับเด็ก | คัดสรรเนื้อหาเหมาะสม | Safe for Kids | Carefully curated content |
| 📱 | ไม่ต้องดาวน์โหลด | เล่นได้เลยบนเบราว์เซอร์ | No Download | Play right in your browser |

#### 4. Genre highlights section (H2 + paragraph)
- **H2 Thai:** `เกมครอบคลุมทุกประเภท`
- **H2 English:** `Games for Every Interest`
- **Thai paragraph:** `เรามีเกมครอบคลุมทุกประเภท ไม่ว่าจะเป็นเกมปริศนาฝึกสมอง เกมแข่งรถมันส์ๆ เกมการศึกษาสอนภาษาอังกฤษ เกมแต่งตัว เกมทำอาหาร เกมแคชวล เกมไอโอ และอีกมากมาย รับรองว่ามีเกมที่ถูกใจเด็กทุกคนอย่างแน่นอน`
- **English paragraph:** `From brain-teasing puzzles and racing games to educational titles, dress-up, cooking, casual, and IO games — there's something for every kid on KiddoGame.`

#### 5. How to play section (H2 + 3 numbered steps)
- **H2 Thai:** `วิธีเริ่มเล่น`
- **H2 English:** `How to Start Playing`
- **Steps (Thai):**
  1. เลือกเกมที่ชอบจากหน้าแรก
  2. กดที่เกมเพื่อเข้าสู่หน้าเกม
  3. กดเล่นได้เลย ไม่ต้องสมัครสมาชิก ไม่ต้องดาวน์โหลด
- **Steps (English):**
  1. Browse and pick a game from the home page
  2. Click the game to open its page
  3. Hit play — no sign-up, no download needed

### Styling notes
- Page wrapper: `max-w-2xl mx-auto` (narrower than game grid — reads better as prose)
- Feature cards: `grid grid-cols-2 gap-4` on mobile, `sm:grid-cols-2` consistent
- Each card: `bg-indigo-50 rounded-2xl p-6 flex flex-col gap-2`
- H1: `text-3xl font-bold mb-6`
- H2 sections: `text-xl font-bold mt-10 mb-3`
- Steps: `ol` with `list-decimal list-inside space-y-2 text-slate-600`

## Nav Changes

### Strings.cs
Add one entry:
```csharp
["nav_about"] = ("เกี่ยวกับเรา", "About"),
```

### TopNav.razor
Add after the Favorites `<a>` tag:
```razor
<a href="/about"
   class="px-4 py-2 rounded-xl text-sm font-medium transition-colors
          @(IsAbout ? "bg-indigo-100 text-indigo-700" : "text-slate-500 hover:bg-slate-100 hover:text-slate-700")">
    @Strings.Get(LangSvc.Current, "nav_about")
</a>
```
Add `IsAbout` computed property alongside `IsHome` and `IsFavorites`.

### MobileNav.razor
Replace the dead Search placeholder tab with an About tab using an info icon (SVG) and `nav_about` label. Add `IsAbout` computed property.

## Sitemap Update

In `Program.cs`, add the About URL to the `/sitemap.xml` endpoint after the home page entry:

```xml
<url>
  <loc>https://kiddogame.net/about</loc>
  <changefreq>monthly</changefreq>
  <priority>0.9</priority>
</url>
```

## Testing

- Build passes with 0 errors
- `/about` renders correctly in Thai and English mode
- `<title>` tag contains Thai keywords (curl verify)
- Nav link highlights correctly when on `/about`
- Mobile nav tab highlights correctly when on `/about`
- `/sitemap.xml` includes `https://kiddogame.net/about`
