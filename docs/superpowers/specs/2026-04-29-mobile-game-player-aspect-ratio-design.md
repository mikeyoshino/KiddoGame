# Mobile Game Player Aspect Ratio — Design Spec
_Date: 2026-04-29_

## Problem

The game iframe uses `aspect-video` (16:9) at all screen sizes. On phones the game content is designed for portrait orientation, so a 16:9 landscape frame wastes vertical space and makes the game feel cramped.

## Solution

Use Tailwind responsive aspect ratio classes to switch the iframe container from 9:16 portrait on phones to 16:9 landscape on tablets and above.

## Breakpoints

| Screen | Breakpoint | Aspect ratio |
|---|---|---|
| Phone | < 768px (below `md`) | 9:16 (portrait) |
| Tablet + Desktop | ≥ 768px (`md` and above) | 16:9 (landscape) |

## Change

**File:** `webapp/Components/Pages/GamePage.razor`

**What changes:** The non-expanded iframe wrapper div class changes from:
```
w-full aspect-video rounded-2xl mb-6 relative
```
to:
```
w-full aspect-[9/16] md:aspect-video rounded-2xl mb-6 relative
```

## Out of scope
- No change to the fullscreen/expand behaviour
- No change to desktop layout
- No change to similar games thumbnails
- No other files touched
