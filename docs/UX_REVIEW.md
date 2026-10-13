# UX/UI REVIEW — MotoManager (Android, landscape)

## Resumen (para el usuario)

- **Problema principal:** casi todas las pantallas enseñan *todo a la vez*: notas largas plegadas bajo "Más información", varias filas de chips apiladas, botones verdes/amarillos repetidos en cada tarjeta (10 "MEJORAR" en Instalaciones, 4 "CONTRATAR" + 3 "RENOVAR" en Personal), tablas de 6–9 columnas y textos a 12–14 px. Además hay **tres familias de componentes** distintas (`Ui.*`, `TeamCards`, `BikeCards` y el `Tile` privado de Inicio), por eso cada sección se ve un poco distinta.
- **Qué copiamos del juego de referencia:** baldosas grandes con cabecera en mayúsculas, **un número clave por baldosa**, como mucho una línea de estado, **un solo botón principal por pantalla** (abajo a la derecha, o arriba a la derecha en el flujo de carrera), la explicación detrás de un botón **"i" que abre una hoja** (no texto plegado), listas de 4 columnas como máximo con la fila entera pulsable, y selección de neumáticos/acciones secundarias en ventanas emergentes.
- **Qué mantenemos nuestro:** el tema oscuro "carbono", el amarillo de cronometraje para los números clave y el **color del equipo como acento** (botón principal, pestaña activa, fila propia). No copiamos el azul de la referencia.
- **No se pierde información:** lo que sale de la vista pasa a una hoja "i", al detalle (tocar la baldosa/fila) o a un menú "⋯". Cada acción actual sigue accesible (lista por pantalla abajo).
- **Plan:** 5 paquetes de trabajo independientes. El paquete 1 (sistema de diseño + shell) va primero; los otros 4 trabajan en paralelo sobre ficheros distintos.

---

## 0. Scope and ground rules for implementers

- Canvas: `UiScale` gives ~1689×760 units on a 20:9 phone; after `SafeArea` the usable rect is **≈1600×730**. All numbers below are canvas units in that rect.
- Every screen must work **without scrolling** for its primary state (overview pages). Scroll lists are allowed only for genuinely long data (standings, market, calendar, tables in detail views, sheets).
- **Preserve every action.** Each screen section lists "Actions that must remain reachable". If an action moves, it moves to a sheet, a "⋯" menu, or a detail view — never removed.
- Unicode symbol glyphs (`ⓘ ☂ ☁ ☀ ▼ ► ◄ ●`) must be replaced by `UiIcons` sprites (the device font may lack them).
- Code style: match the existing code (C# 7.3-safe, no new packages, static helpers, anchor-based layouts).

---

## 1. Design system (strict rules)

### 1.1 Layout grid (1600×730 safe rect)

| Token | Value | Notes |
|---|---|---|
| `Rail` | **128** wide (fixed units, not 9.5%) | icon + 18-unit caption |
| `TopBar` | **72** high | was 84 |
| `Tabs` | **56** high, 8 gap above/below | only when section has >1 visible page |
| `Gutter` | **16** | page side/bottom margin |
| `Gap` | **12** | between tiles/cards |
| Content rect | x 144→1584 (**1440 wide**); height **570** with tabs, **634** without | |
| Columns | **12 cols**, col = 109, gap 12 → span(n) = 121·n − 12 | 3=351, 4=472, 6=714, 8=956, 9=1077 |
| Spacing scale | 4 / 8 / 12 / 16 / 24 only | no other paddings |
| Card padding | 16 (all sides) | was 12/8 |
| Tile header strip | **48** high | `TeamCards.Head` is 50 → 48 |
| List row | **64** high (min 56) | |

### 1.2 Type scale (new `Theme` constants in UiFactory.cs)

| Constant | Size | Style | Use |
|---|---|---|---|
| `Theme.TextDisplay` | 56 | BoldItalic | hero number (rating, P1, cash in a hero tile) |
| `Theme.TextKey` | 44 | BoldItalic, Timing yellow by default | the one key number of a tile |
| `Theme.TextTitle` | 32 | BoldItalic CAPS | page/hero names (rider surname, GP name) |
| `Theme.TextHeader` | 24 | BoldItalic CAPS | tile/card header strips, CTA labels (CTA uses 28) |
| `Theme.TextBody` | 24 | Regular/Bold | list cells, lines |
| `Theme.TextCaption` | 20 | Bold CAPS, dim | captions under numbers, column captions, status lines |
| `Theme.TextMin` | **18** | — | absolute floor; `resizeTextMinSize` must be ≥ 18 everywhere |

Keep `FontSmall/Normal/Large/Title` as aliases (22/28/40/56) for compatibility, but **ban `FontSmall - 6/-8/-10`** (12–16 units). Any `resizeTextMinSize` below 18 is a bug.

### 1.3 Density limits

- **Tile**: header (≤ 2 words) + **1 key number** + **≤ 1 status line** + optional badge/chevron. No paragraphs, no buttons except when the tile *is* the button.
- **Card**: header + ≤ 1 KPI row (≤ 4 KPIs) + ≤ 5 label/value lines + ≤ 2 buttons. More → split, or move to sheet.
- **Screen**: ≤ 6 tiles/cards visible, **exactly 0 or 1 filled-accent (primary) button**, ≤ 1 row of segmented control (a second filter becomes a `Ui.Picker` pill).
- **List**: ≤ 4 data columns + position + chevron. Extra columns → row tap → detail/sheet.
- **Text**: no visible sentence longer than ~60 characters on an overview. Explanations → `Ui.InfoButton` sheet.

### 1.4 Colour roles (keep our palette)

| Role | Value | Rule |
|---|---|---|
| Background | `Theme.Background` #0A0B0E + carbon weave | unchanged |
| Card surface | `Theme.Card` #191B20 | lists, sheets |
| Tile surface | `Lerp(Card, PanelLight, .28)` → **new `Theme.Tile`** | all tiles |
| Header strip | black α .38 over tile → **new `Theme.HeaderFill`**; highlighted strip = Accent fill | |
| **Accent = team colour** | `Theme.Accent` | primary CTA fill, active tab underline, active rail bar, own row in lists, header tag. Never for body text. |
| **On-accent text** | **new `Theme.OnAccent`** = Ink (#0C0D10) if accent luminance > 0.55 else white | fixes white-on-yellow team colours |
| Timing yellow | `Theme.Timing` #FFD200 | key numbers (levels, grid slots, positions), "better" edge in duels. Never on buttons. |
| Good / Warning / Bad | #46B973 / #EBB42D / #DC3737 | status only: covered/positive delta; attention/expiring; missing/negative/destructive |
| Perf / Reliability | `BikeCards.PerfColor` green, `RelColor` purple | bike gauges only (move to `Theme.Perf`, `Theme.Rel`) |
| Dim text | `Theme.TextDim` | captions, secondary values |

Remove decorative noise that competes with data: diagonal **hatch patterns** in card titles/bands (`Ui.Card`, `Ui.Band`, `TeamCards.Strip`) and the gradient "Divider" under card titles. Keep: chamfered corners, header strip, accent tag.

### 1.5 Button hierarchy

| Level | Look | Height | Where |
|---|---|---|---|
| **Primary CTA** (`Ui.PrimaryCTA`) | Accent fill, slanted, `OnAccent` BoldItalic CAPS 28 + optional 20-unit sub-line (cost · days) | 72 | **one per screen**, bottom-right of content (management) / top-right of top bar (race flow) |
| Secondary (`Ui.Secondary`) | `PanelLight` fill, white Bold CAPS 22 | 64 | beside the primary (to its left) or inside cards |
| Ghost / link (`Ui.Ghost`) | no fill, dim CAPS + chevron icon | 56 | "Historial ›", "Ver estadísticas ›" |
| Danger (`Ui.Danger`) | Bad at 45% → full Bad when armed; two-tap confirm built in | 64 | release, fire, cancel project, sell, close |
| Icon button (`Ui.IconButton`) | 40 visual, **56 hit area** | 56 | info, more (⋯), close, back, camera |

- The shell top-bar race button becomes a **secondary-accent** style (dark fill, accent outline 2, accent text) on sub-pages, and is **hidden on the Hub** (the hub's race tile is the primary there). The page's own primary stays the only filled-accent element.
- Per-row buttons in lists are **never** filled accent; rows are tappable instead (chevron at right).

### 1.6 Secondary information

- **`Ui.InfoButton`** ("i" in a circle, 36 visual / 56 hit) at the right end of a header strip opens **`Ui.Sheet`** with the title and paragraphs. Replaces: `Ui.Note` folding ("Más información"), `Ui.Expander` for explanations, and `Shell.Toast(...)` used as an explanation (attribute info, staff ceiling).
- `Ui.Note` stays only for **visible alerts** (warning/bad colour) → rename use to `Ui.Alert`. Empty lists → `Ui.Empty`.
- Data that is secondary but still data (all 20 attributes, track performance bars, history tables) → **sheet opened from an InfoButton or a Ghost link**.
- Toast stays for confirmations of actions only.

### 1.7 Tabs and segmented controls

- Shell tabs: equal width, max **4 visible** per section (pages may be hidden with `ScreenBase.HiddenTab`), CAPS 22 Bold; active = `PanelLight` + 4-unit accent underline. Height 56.
- In-page segmented (`Ui.Segmented`, today `Ui.Chips`): max 5 segments, one row per screen, height 56. More options (categories, championships) → `Ui.Picker` pill ("MOTO2 ▾") opening a sheet list.

### 1.8 List rows

`Ui.ListRow`: 64 high, alternate shade, slanted position block (if ranking), name cell Bold 24 left, other cells centred 24, own row = accent α .32 + 6-unit accent edge, whole row tappable when `onTap` given (chevron icon right, 24). Max 4 data columns. Pinned own row at bottom when scrolled out (existing standings behaviour, generalise).

### 1.9 Empty states

`Ui.Empty`: dim icon 64, one line CAPS 22 dim, optional secondary button. Centered in the card/area. Never a dim paragraph.

### 1.10 Number formatting

- Money: `MathX.FormatMoney` / `Theme.Money` ("€640k", "€6.15M"), max 1 decimal, sign coloured only when it is a delta.
- Ratings/levels: integers. Percentages: integer + "%". Positions: "P3" (tables use "3"). Deltas: "+4" Good / "−0,5" Bad (Unicode minus), new `Theme.Signed(double v, int decimals = 0)`.
- Lap times `m:ss.fff`, gaps `+1.234`. Dates "29 MAR" (CAPS in headers), seasons "2026".
- Units go into the caption ("€/TEMP."), not repeated after every value.

### 1.11 Touch targets

Min **56×56** hit area for any tappable; buttons 64; primary 72; list rows 64; tabs 56; rail cells ≥ 80 high. `UiScale.Touch()` stays (×1.2) but callers pass the base values above.

### 1.12 Icons (add to `UiIcons`)

Add: `Info`, `Close`, `Chevron`, `More` (⋯), `Lock`, `Inbox`, `News`, `Settings`, `Camera`, `Pause`, `Play`, `Skip`, `Sun`, `Cloud`, `Rain`, `Tyre`, `Gauge` (performance), `Wrench` (reliability), `Plus`, `Minus`, `Check`, `Warning`. Same drawing approach as the existing pictograms (`Inside(name,u,v)`).

---

## 2. Shared components (Components.cs / UiFactory.cs) — exact API

All in `public static class Ui` (Components.cs) unless noted. "area" = a RectTransform the component **fills** (anchor layouts); "list" = a parent with a layout group (row added).

```csharp
// ---- Theme additions (UiFactory.cs, class Theme)
public const int TextDisplay = 56, TextKey = 44, TextTitle = 32, TextHeader = 24, TextBody = 24, TextCaption = 20, TextMin = 18;
public static readonly Color Tile;          // Lerp(Card, PanelLight, .28)
public static readonly Color HeaderFill;    // new Color(0,0,0,.38f)
public static readonly Color Perf, Rel;     // moved from BikeCards
public static Color OnAccent { get; }       // recomputed in SetTeamColors/SetAccent
public static string Signed(double v, int decimals = 0);   // "+4" green / "−0,5" red rich text
public static string Pos(int p);                            // "P3" or "-"

// ---- Layout (Components.cs)
public static class Grid { public const float Gutter = 16, Gap = 12, Header = 48, Row = 64, Button = 64, Cta = 72, Hit = 56; }
public static RectTransform Area(RectTransform parent, float x0, float y0, float x1, float y1);   // anchors + Gap/2 inset
public static RectTransform[] Columns12(RectTransform parent, params int[] spans);              // horizontal split by 12-col spans, Gap between
public static RectTransform[] Rows(RectTransform parent, params float[] shares);                // vertical split, Gap between

// ---- Tiles & headers
public static RectTransform HeaderStrip(Transform parent, string title, string right = null, Action onInfo = null,
    Action onBack = null, Color? fill = null, int badge = 0, Color? badgeColor = null, bool chevron = false);   // 48 high, top-anchored, returns strip
public static RectTransform Tile(RectTransform area, string title, Action onTap = null, string right = null,
    int badge = 0, Color? badgeColor = null, Action onInfo = null, bool dim = false, Color? edge = null, Color? headerFill = null); // returns body
public static RectTransform StatTile(RectTransform area, string title, string value, string status = null,
    Action onTap = null, Color? valueColor = null, string icon = null, int badge = 0, Action onInfo = null, Color? edge = null);   // 1 number + 1 status
public static RectTransform Card(Transform list, string title = null, string right = null, Action onInfo = null, Action onBack = null); // restyled (no hatch), padding 16
public static RectTransform Section(Transform list, string title, string right = null, Action onInfo = null);  // header strip alone in a list

// ---- Buttons
public static Button PrimaryCTA(RectTransform area, string label, Action onTap, string sub = null, bool enabled = true);   // disabled: sub shows the blocker
public static Button PrimaryCTA(Transform list, string label, Action onTap, string sub = null, bool enabled = true);      // row of Grid.Cta
public static Button Secondary(RectTransform area, string label, Action onTap, bool enabled = true);
public static Button Ghost(RectTransform area, string label, Action onTap);                 // text + chevron
public static Button Danger(RectTransform area, string label, string confirmLabel, string armKey, Action onConfirm, Action refresh); // two-tap, static armed key
public static Button IconButton(Transform parent, string icon, Action onTap, Color? tint = null);   // 56 hit; caller positions

// ---- Info & sheets
public static Button InfoButton(Transform headerStrip, string title, params string[] paragraphs);   // places itself at the strip's right end
public static RectTransform Sheet(string title, Action<Transform> build, UiAct primary = null, UiAct secondary = null, float width = 0.62f);
public static void CloseSheet();
public static Transform SheetHost;   // set by ManagementShell.Build and by each race-flow panel root; fallback = top-most Canvas
// Sheet: shade α.6 (tap closes), centred panel width*1600, height ≤ 85% (content-sized, scrolls), HeaderStrip with Close icon,
// body = vertical scroll list (padding 16), optional buttons bottom-right (primary right). Only one sheet at a time.

// ---- Data atoms
public static Image Pill(Transform parent, string text, Color fill, Color? textColor = null);       // 32 high, CAPS 20 Bold, auto width (ContentSizeFitter)
public static Image RatingBadge(Transform parent, double value, string tag, float side);             // replaces TeamCards.RatingSquare & BikeCards.Square
public static void Segmented(Transform list, IList<string> labels, int selected, Action<int> onSelect, float height = 56); // = restyled Chips, max 5
public static Button Picker(RectTransform area, string caption, IList<string> options, int selected, Action<int> onSelect); // "MOTO2 ▾" → sheet list
public static void Stepper(Transform list, string label, string value, Action minus, Action plus);   // [−] VALUE [+], 64 high (replaces MobileParts.Stepper with 4 buttons)
public static RectTransform ListRow(Transform list, float[] widths, string[] cells, int index, bool mine, Action onTap = null, float height = 64);
public static RectTransform PersonRow(Transform list, double level, string name, string detail, string figure, Action onTap = null, UiAct action = null); // level badge | name/detail | figure | 1 secondary button
public static void Empty(Transform list, string icon, string text, UiAct action = null);
public static void Alert(Transform list, string text, bool bad = false);   // visible single-line warning with Warning/Bad edge
public static void Gauge(RectTransform area, double fraction, Color color);  // moved from BikeCards.Gauge
public static void Pips(RectTransform area, int level, int max);            // moved from BikeCards.Pips
public static RectTransform Segments(Transform parent, int total, int filled, Color on); // moved from TeamCards
```

Existing APIs kept: `Kpis/Kpi` (restyled as StatTile-look, **assert ≤ 4**), `Bar`, `Line`, `Item` (min height 64, actions rendered Secondary), `Band` (no hatch), `ListCaptions` (captions ≥ 20), `Stack`, `Columns`, `Expander` (allowed only for user data lists, not explanations). `Ui.Note` keeps current behaviour but is marked `[Obsolete("Use InfoButton/Alert/Empty")]`; packages 2–5 must replace every call.

`TeamCards`, `BikeCards`, `MobileParts`: keep their signatures but make them **thin wrappers** over the new `Ui.*` (same tokens, same look). New code must call `Ui.*` directly.

Localisation: `Loc.Load` additionally merges every TextAsset under `Resources/Localization/ux_<lang>/` (via `Resources.LoadAll<TextAsset>`). Each package adds its new keys in **its own file** `ux_es/wpN.json` + `ux_en/wpN.json` → no merge conflicts on es.json/en.json. Key prefix `ux.<area>.*`.

---

## 3. Per-screen audit and target layouts

Notation: wireframes are the content rect (1440×570 with tabs / 1440×634 without). `[i]` = InfoButton, `›` = chevron/tappable, `⋯` = more menu, `▾` = Picker, `═CTA═` = the single primary.

### 3.1 ManagementShell (top bar, rail, tabs, menu, toast)

**Problems**
- Top bar holds 8 things: back, team name, sub line (page · date · next GP), busy text, 3 pills (cash, rep, championship), race CTA → crowded; the next GP repeats the hub race tile; REP pill is low-value.
- Race CTA in the top bar duplicates the hub's big race button (two primaries on the hub) and competes with every page's own primary.
- Rail = 9.5% (152 units): wide for icons; captions at 10–16 units are unreadable.
- 5 tabs in Career (TeamSetup and Found are wizard steps, not browse pages).
- Menu (☰) packs save, 3 slots, debug, audio, text size as rows of 2–4 buttons; glyph ☰ drawn by icon (ok).

**Target**
```
┌Rail 128┬──────────────────────────── TopBar 72 ───────────────────────────────────────────┐
│ ☰      │[‹] PAGE TITLE (TextTitle)         [€6.15M] [P12 · 9 PTS] [inbox n][news] [CARRERA ›]│
│ HOME   │    29 MAR 2026 (caption)            pill      pill            56   56   secondary-accent│
│ TEAM   ├──────────────────────────── Tabs 56 (≤4) ───────────────────────────────────────────┤
│ BIKE   │ PILOTOS  │ PERSONAL │ MÁNAGER │                                                     │
│ €      ├──────────────────────────── content 1440×570 ───────────────────────────────────────┤
│ ⇄      │                                                                                     │
│ 🏆     │                                                                                     │
│ CAREER │                                              toast: bottom-centre, 640 wide, 64 high │
└────────┴─────────────────────────────────────────────────────────────────────────────────────┘
```
- Title: team name on Hub; page title (CAPS) on sub-pages. Sub line: date only (busy label replaces it while busy — keep).
- Pills: **cash** (→ Finance) and **championship** (→ Standings). REP moves to Manager page.
- New icon buttons: **Inbox** (badge = todo count; opens Sheet listing every hub todo as tappable rows → their existing `Go` actions) and **News** (opens the news Sheet; replaces the hub news strip/overlay).
- Race button: hidden on Hub; secondary-accent style elsewhere; label stays `ContinueShort()`.
- Rail: fixed 128; icon 48; caption 18 CAPS Bold; active cell = `PanelLight` + 6-unit accent bar (keep).
- `ScreenBase` gets `public virtual bool HiddenTab => false;` — hidden pages are excluded from the tab strip but `Show<T>()` still works. Career: tabs = RESUMEN | OFERTAS | ORGANIZACIÓN; `TeamSetupPage` and `CareerFoundPage` hidden (reached by buttons, see 3.20).
- Menu: becomes a `Ui.Sheet` "MENÚ": rows: GUARDAR (primary), Slots 1–3 (Secondary, filled dot icon when used), MÚSICA / SONIDO toggles, TEXTO A− A A+ segmented, SALIR AL MENÚ (Danger). Debug row unchanged under `#if`.
- Set `Ui.SheetHost` = safe root in `Build`.

**Must remain reachable:** back/history, all 7 sections, all tabs (incl. hidden via buttons), save/slots/menu exit/audio/text size, Continue (race / simulate / finish / close season), busy indicator, toasts.

### 3.2 Hub (HomeScreen.cs)

**Problems**
- Top strip has two tickers (first todo + "+n", latest headline) competing with the tiles; the todo text duplicates tile badges.
- Four race actions: top-bar CARRERA, RaceCall button, "Simular evento", "Simular temporada".
- Riders tile: per rider 7 elements (number, name, P, points, injury, morale bar, rating square).
- Finance tile shows cash = same as the top-bar cash pill.
- `HubScreen.Tile/Figure` duplicate `TeamCards.Tile`.

**Target (no tabs, 1440×634)**
```
┌──────── PILOTOS (6 cols) ──────────┐┌─ PERSONAL (3) ─┐┌─ MOTO (3) ─────┐
│ [#93] MÁRQUEZ             [ 78 ]   ││   [icon]       ││   [icon]       │
│       P3 · 54 PTS                  ││     11         ││     71         │  h = 0.58
│ ────────────────────────────────── ││ 1 VACANTE  (warn)││ I+D 1/2       │
│ [#21] GÓMEZ               [ 64 ]   ││                ││                │
│       LESIONADO 2 GP (bad pill)    │└────────────────┘└────────────────┘
└────────────────────────────────────┘
┌ PATROCINIO (3) ┐┌ FINANZAS (3) ──┐┌──────────── PRÓXIMA CARRERA (6) ─────────────┐
│     3/4        ││    €2.1M       ││ RONDA 5/20 · 29 MAR                    [⋯]   │ h = 0.42
│ 2 OFERTAS      ││ PRESUP. RESTANTE││ GRAN PREMIO DE CATAR                         │
└────────────────┘└────────────────┘│ ════════ CARRERA › (PrimaryCTA) ═════════    │
                                    └──────────────────────────────────────────────┘
```
- Remove the strip; todos → tile badges (existing `Count`) + top-bar Inbox sheet. Headline → News icon.
- Riders tile per rider (≤ 3 rows): number plate, SURNAME (TextTitle), rating badge; one line: `P3 · 54 PTS` or an injury pill (Bad) instead. **Drop morale bar** (shown in rider detail). Free seat row: "PLAZA LIBRE ›" warning → Market.
- Staff tile key = headcount, status = missing count (Warning) or "COMPLETO".
- Bike tile key = average component level (rating colour), status = `I+D n/m`.
- Sponsors tile key = `deals/slots`, status = offers (Timing) or "SIN OFERTAS".
- Finance tile key = **remaining budget** (Bad if < 0), status "PRESUPUESTO RESTANTE" (cash already in top bar).
- Race tile: round + date caption, GP name TextTitle, PrimaryCTA "CARRERA ›" (or "SIMULAR ›" when the next event is not the player's category, or season over → `ContinueShort`). `⋯` IconButton → Sheet with Secondary "SIMULAR EVENTO" and "SIMULAR TEMPORADA".
- No-team hub (`BuildWithoutTeam`): left card "SIN EQUIPO" with PrimaryCTA "VER OFERTAS" + Secondary "FUNDAR EQUIPO"; next-event card (GP name + Secondary SIMULAR / SIMULAR TEMPORADA); news = News icon; last result → 1 line under the next-event title.
- Replace `Tile/Figure` with `Ui.Tile/Ui.StatTile`.

**Must remain reachable:** every tile destination, all todos (Inbox), news, Continue, simulate event, simulate season, last result line.

### 3.3 Riders overview (TeamScreens.cs `RidersPage.Overview`)

Already close to the reference (two rider cards + attribute duel). **Problems:** each rider card carries hero + key line (age, morale, potential) + trait + contract caption/seasons/segmented bar + under-line (until, salary, next/expiring) ≈ 11 items; middle column stacks pager + bids + "Fichar" + average (4 controls).

**Target**
```
┌──── DETALLES DEL PILOTO › (4 cols) ──┐┌──── ATRIBUTOS (4 cols) ────┐┌──── DETALLES DEL PILOTO › ──┐
│ #93  Marc                    [78]    ││ MAR             GÓM        ││  (mirror)                   │
│      MÁRQUEZ                 VAL     ││ 74 │ CARRERA      │ 70     ││                             │
│ ──────────────────────────────────── ││ 81 │ CLASIFICACIÓN│ 66     ││                             │
│ [★] GENIO SOBRE MOJADO               ││ .. 6 rows (Timing edge on better side)                    │
│     Carreras en mojado: −1 s/vuelta  ││ [pill: 2 OFERTAS ›] [pill: 3–4 / 5 ›]                    │
│ ──────────────────────────────────── ││ ═══════ FICHAR PILOTOS › ═══════ (Secondary-accent)      │
│ CONTRATO            2 TEMPORADAS     ││ MEDIA CATEGORÍA          [62]                            │
│ ▬▬▬ ▬▬▬ ░░░ (expiring = Warning)     │└────────────────────────────┘                            │
└──────────────────────────────────────┘
```
- Drop the key line (age/morale/potential) and the under-line (until/salary/next) from the overview → rider detail.
- Bids and pager become **pills** above the market button (only when present).
- "FICHAR PILOTOS" is Secondary (accent outline): this page has no filled primary (browse page).
- Free seat card: icon + "PLAZA LIBRE" + PrimaryCTA "IR AL MERCADO ›" (the only primary when a seat is free).

**Must remain reachable:** open rider detail, cycle pairs (>2 seats), bids view (accept/reject), market.

### 3.4 Rider detail

**Problems:** in-page back is a 66-high full-width button; attribute info via toast; right column scrolls through attribute tiles, stats button, "all attributes" expander, traits, contract card with up to 9 lines + long terms note.

**Target (non-scrolling)**
```
┌────── left 5 cols ─────────────────┐┌──────────── right 7 cols ─────────────────────────────┐
│[‹] PERFIL                          ││ ATRIBUTOS DEL PILOTO                         [i]       │
│ #93  Marc                  [78]    ││ ┌CARRERA ┐┌CLASIFIC.┐┌REGULAR.┐                        │
│      MÁRQUEZ               VAL     ││ │  74    ││  81     ││  69    │  StatTiles 2×3, h 0.55 │
│ 31 AÑOS · MORAL 72 · POT. 80 (caption)│└────────┘└─────────┘└────────┘                       │
│ CONTRATO  ›          2 TEMPORADAS  ││ ┌MOJADO  ┐┌ADELANT.┐┌NEUMÁT. ┐                         │
│ ▬▬▬ ▬▬▬ ░░░                         ││ └────────┘└────────┘└────────┘                         │
│ VER ESTADÍSTICAS ›  (Ghost)        ││ RASGOS                                      [i]        │
│[OPCIÓN] [LIBERAR (Danger)] [═RENOVAR═]││ [★] GENIO SOBRE MOJADO — Carreras en mojado −1 s/v.  │
└────────────────────────────────────┘│ [+] LESIONADO 2 GP (Bad) when injured                   │
                                      └────────────────────────────────────────────────────────┘
```
- Back = `onBack` chevron on the left card HeaderStrip.
- One caption line keeps age · morale · potential (moved from overview).
- Contract block tappable → **Sheet "CONTRATO"** with all `Ui.Line`s (salary, until, win/point bonus, release clause, option, funding, termination cost, next contract) + `NegotiationPanel.Terms` text.
- ATRIBUTOS `[i]` → Sheet with `MobileParts.AttributeTable` (all attributes) + one paragraph per key attribute (`attrinfo.*`) — replaces the toasts and the Expander.
- Stats → Ghost link (existing `_stats` view).
- Actions bottom of left card: RENOVAR = PrimaryCTA (bottom-right of the card), team option = Secondary (only when available), LIBERAR = `Ui.Danger` (label shows cost; confirm label `team.releaseConfirm`).

**Must remain reachable:** negotiate/renew, exercise option, release (with confirm), stats, all attributes, all contract terms, back.

### 3.5 Rider stats view

Keep. Changes: KPI row (P, PTS, V, POD) full width; below `Columns12(7,5)`: chart card (left) | "TEMPORADA" 3 lines (last race, average, DNFs) + "CARRERA DEPORTIVA" table (5 cols → 4: season, team, pos, pts; wins/podiums merged as "3V · 7P" in pos cell). `r.RealHistory` → `[i]` on the career card. Back = header chevron (remove `MobileParts.BackRow`).

### 3.6 Negotiation (NegotiationPanel.cs) and Bids

**Problems:** a full-width "◄ Cerrar" row; left card = 3 KPIs + 4-button salary stepper + seasons chips + start chips + status note + clauses expander with 6 buttons; right card = cost lines + fee stepper + message + propose + counter. ~25 controls.

**Target**
```
┌──── [‹] OFERTA A M. MÁRQUEZ (6 cols) ──────┐┌──── COSTE 2026–2027 (6 cols) ───────────────┐
│ SALARIO     [−]   €450k / TEMP.   [+]      ││ [PIDE €500k] [GARANTIZADO €900k] [CAJA TRAS €1.2M] │
│ TEMPORADAS  [ 1 | 2 | 3 | 4 ]              ││ BONUS ESPERADOS     ≈ €40k / temp.          │
│ INICIO      [ AHORA | PRÓX. TEMP. ]        ││ PRIMA FICHAJE       €45k                    │
│ CLÁUSULAS (3) ›   (Secondary → sheet)      ││ TRASPASO            €200k  [−][+] (if needed)│
│ status line (Warning/Good, 1 line)         ││ "Quiere más dinero" (message, Bold, 1–2 lines)│
└────────────────────────────────────────────┘│ [USAR CONTRAOFERTA €480k]   [═ PROPONER ═]   │
                                              └──────────────────────────────────────────────┘
```
- Salary stepper: ± 5% per tap (`Ui.Stepper`); −10/+10 removed (reachable by two taps).
- Clauses Sheet: signing bonus, bonuses, release clause, team option, performance extension, exit clause (same toggles as now).
- Fee stepper only when `needAgreement`; owner agreement warning as `Ui.Alert`.
- Close = header back chevron.
- Bids view: `Ui.Card("OFERTAS RECIBIDAS", onBack)` + `PersonRow` per bid (name → buyer, "caduca en N"), action = Secondary "ACEPTAR" and a `⋯`→"RECHAZAR". `bids.note` → `[i]`.

### 3.7 Staff overview

Good structure (12 role tiles). **Problems:** top strip text "8 personas · nóminas €430k por temporada" + ceiling with "ⓘ" glyph that toasts; empty-role tiles show red "CONTRATAR" text — ok.

**Target**
```
┌ PERSONAL 8 · NÓMINAS €430K/TEMP. (8 cols, caption-strip 48) ┐┌ TECHO FICHAJES  61 [i] (4 cols) ┐
┌DIRECTOR TÉCNICO ›┐┌JEFE INGENIEROS ›┐┌ING. DE PISTA ›┐┌ING. ELECTRÓNICA ›┐   4×3 tiles, h = (570-60)/3
│ CONTRATAR (Bad)  ││ WIJAYA      57  ││ GIL/GRECO  54 ││ (dim, optional)  │   left edge = coverage colour
└──────────────────┘└─────────────────┘└───────────────┘└──────────────────┘   badge = contracts ending
```
- Ceiling: `[i]` → Sheet with `staff.ceiling` text (no toast). Level number TextKey Timing; name TextHeader. Use `Ui.Tile(edge: coverage)`.

### 3.8 Staff role detail

**Problems (screenshot 7):** full-width "◄ Volver" row; per holder 3 big buttons (CAMBIAR PILOTO / RENOVAR / DESPEDIR) → 6 buttons for two holders, 3 yellow; candidates each with a yellow CONTRATAR → 7 filled yellow buttons on one screen; warning sentence; "Más información" folds.

**Target**
```
┌[‹] MECÁNICO JEFE   CUBIERTO (Good pill)  [i] (5 cols)┐┌ CANDIDATOS          TECHO 61 (7 cols)        ┐
│ [49] GALANG SETIAWAN                         [⋯]     ││ ⚠ Sustituye a Setiawan (indemn. €23k)       │
│      con Lombardi · hasta 2027 · €23k/temp.          ││ [54] TAIGA SUZUKI  JP · Exp.10   €30k [CONTRATAR]│
│ [54] TAIGA SUZUKI                            [⋯]     ││ [50] KAITO MATSUMOTO              €25k [CONTRATAR]│
│      con Gómez · hasta 2027 · €30k/temp.             ││ ... (scroll)                                 │
└──────────────────────────────────────────────────────┘└──────────────────────────────────────────────┘
```
- Holder `⋯` → Sheet with: Secondary "CAMBIAR PILOTO" (when applicable), Primary "RENOVAR", Danger "DESPEDIR (€x)".
- Candidate CONTRATAR buttons = **Secondary** (dark, accent text); confirm-replace stays two-tap via `Ui.Danger`-style arming when it replaces someone.
- "No mejora al actual" sub-line → dim caption only when true; role info (`roleinfo`, `roleimpact`, `staff.advice`) → `[i]`.
- Candidate list `PersonRow(level, name, "JP · Exp. 10", "€30k", action)`.

### 3.9 Manager page

**Problems:** inline "key: value" strings, role sentence note, skills section, objective card, structure card with 5 lines, all in a scroll.

**Target (non-scrolling)**
```
┌ MÁNAGER (6 cols) ──────────────────────────┐┌ OBJETIVO (6 cols) ──────────────────────────┐
│ ANA PÉREZ                       [REP 54]   ││ TOP 5                    OBJETIVO ACTUAL P5  │
│ [PATRIMONIO €1.2M][SALARIO €300k][HASTA 2027]││ ÚLTIMO: TOP 8 · P6  ✓ CUMPLIDO               │
│ HABILIDADES                         [i]    │├ ESTRUCTURA ─────────────────────────────────┤
│ Negociación  ▬▬▬▬▬░ 62  (5 bars)          ││ EQUIPO / ORGANIZACIÓN / PROPIETARIO / CATEGORÍA│
│ ...                                       ││ REPUTACIÓN EQUIPO  18                        │
└───────────────────────────────────────────┘└──────────────────────────────────────────────┘
```
- KPIs: wealth, salary (or "PROPIETARIO"), contract until. Role sentence (`manager.owner/employed`) → `[i]`. Team reputation lives here (removed from top bar).

### 3.10 Bike overview (BikeScreens.cs `BikeScreen`)

**Problems (screenshot 9):** rider rating squares + number plates side by side ("55 50") read as two unexplained numbers; maker/class text overflows the header; every component tile repeats "TOCA PARA DESARROLLAR"; 11 performance bars in an expander below the fold → page scrolls.

**Target (non-scrolling, tabs on: 1440×570)**
```
┌──── DESARROLLO DE LA MOTO   HONDA NSF250RW  [i] (9 cols, h .52) ─────┐┌ PROYECTOS I+D › ┐
│ [#55] LOMBARDI   MOTO 01     │   [bike icon]   │ [#21] GÓMEZ  MOTO 02 ││  1/2            │
│ RENDIMIENTO ▬▬▬▬▬░░  70      │  HONDA NSF250RW │ RENDIM.  ▬▬▬▬░ 70    ││ 1 HUECO LIBRE   │
│ FIABILIDAD  ▬▬▬▬░░░  74%     │      2026       │ FIABIL.  ▬▬▬░ 74%    │├ INSTALACIONES ›┤
└───────────────────────────────────────────────────────────────────────┘│  NV 1,0         │
┌ MOTOR ┐┌ CHASIS › ┐┌ SUSPENSIÓN › ┐┌ AERODINÁMICA › ┐┌ FIABILIDAD › ┐ │ MEDIA DE 10    │
│  69   ││   71     ││    70        ││     65         ││    77        │ └────────────────┘
│CONGEL.││          ││ EN DESARR. 40%││                ││              │   h .48, one row
└───────┘└──────────┘└──────────────┘└────────────────┘└──────────────┘
```
- Drop rider rating squares (they belong to Riders); keep number plate + surname.
- Header right = model only; maker/class → `[i]` sheet together with **"RENDIMIENTO EN PISTA"** (all 11 `PerfAttr` bars) — removes the expander.
- Component tile: key = level (Timing; dim when frozen); status only for frozen / in development; chevron on tappable ones. ≤ 6 components in one row (7+ → two rows of `ceil(n/2)`).

**Must remain reachable:** each component → R&D focus, R&D page, facilities page, free seat → market, performance attributes.

### 3.11 R&D projects (`ProjectsPage`)

**Problems (screenshot 8):** chips row + tier chips + two competing CTAs (green "MEJORAR PIEZA" and blue "INICIAR" in the old build; now one `BigButton` whose label packs "+4 · €95k · 56 días"); trade-off + risk + cap notes in one tiny line; three explanatory notes + history expander in the right column.

**Target**
```
[ MOTOR● | CHASIS | SUSPENSIÓN | AERODINÁMICA | FIABILIDAD ]   (Segmented, 56)
┌──── CHASIS  [71 NV]                         [i] (8 cols) ───────┐┌ EN CURSO 1/2   [i] (4 cols) ┐
│ [ INCREMENTAL | NUEVA ESPEC. | EXPERIMENTAL ]  (Segmented)      ││ CHASIS: NUEVA ESPEC.   [×]  │
│   71 → [75]          ┌ MEJORA ┐  ┌ ÉXITO ┐                       ││ ▬▬▬▬▬▬░░░░ 60%   12 DÍAS    │
│        NUEVO NIVEL   │  +4    │  │  90%  │                       ││ ┌ - - HUECO LIBRE - - ┐    │
│ [SUSPENSIÓN −0,5] (Warning pill, only if trade-off/cap)         ││ HISTORIAL (3) ›  (Ghost)    │
│ COSTE €95k │ INGENIERÍA 47 │ TIEMPO ≈56 DÍAS │ ═ INICIAR ═       ││                             │
└─────────────────────────────────────────────────────────────────┘└─────────────────────────────┘
```
- One primary: INICIAR (bottom-right of left card); disabled with blocker as sub-line ("SIN HUECOS", "SIN DINERO", "CONGELADO").
- Component `[i]` → `compinfo.*`, risk %, season gain cap left, `rnd.successInfo`, `rnd.availableInfo`. Running `[i]` → `rnd.cancelInfo`.
- Cancel = `[×]` IconButton with two-tap arming (`rnd.cancelConfirm` shown as pill when armed).
- Expected effects of a running project → tap the row → sheet. History → Ghost → Sheet with the table.
- Frozen component: left card shows `Ui.Empty(Lock, "CONGELADO POR REGLAMENTO")`.

### 3.12 Facilities

**Problems (screenshot 5):** 10 filled green "MEJORAR €xxxK" buttons; description text in each tile; a long "Para qué sirve" fold under the grid.

**Target (2×5 StatTiles, non-scrolling)**
```
┌ FÁBRICA › ┐┌ BANCO POTENCIA › ┐┌ TÚNEL VIENTO › ┐┌ LAB. ELECTRÓNICA › ┐┌ SIMULADOR › ┐
│  NV 1     ││  NV 1            ││                ││                    ││             │
│ ■□□□□     ││ ■□□□□            ││                ││                    ││             │
│ [€640K]   ││ [€400K] (pill: Good if affordable, Steel if not; "MÁX" Timing at 5)          │
└───────────┘└──────────────────┘ ... second row of 5
```
- Tap tile → Sheet "FÁBRICA": level + pips, `facilitytip.*` + the long "what it does" text, current → next effect, PrimaryCTA "MEJORAR €640K" (disabled "SIN DINERO").
- Remove the bottom fold entirely (its text lives in each sheet).

### 3.13 Finance summary

**Problems:** KPIs + bar + warnings + stepper (3 buttons) or board buttons + org teams note in one card; right card = chips + up to 9 lines + note.

**Target**
```
┌ CAJA ┐┌ PRESUP. RESTANTE ┐┌ FIN DE TEMPORADA ┐┌ MÍNIMO PREVISTO ┐   StatTiles 3 cols each, h 150
│€6.15M││ €2.1M  ▬▬▬▬░ 78% ││ €5.4M            ││ −€120k (Bad)    │
└──────┘└──────────────────┘└──────────────────┘└─────────────────┘
┌ PRESUPUESTO   [i] (5 cols) ───────────────┐┌ [PREVISIÓN | REAL | PLAN]  [i] (7 cols) ──────┐
│ GASTADO €4.0M / €6.1M  ▬▬▬▬▬░             ││ up to 9 Ui.Line rows (scroll inside card)      │
│ [−]  €6.1M  [+]     [CUBRIR] (if player sets)││ balance line Bold                             │
│ or [PEDIR A LA JUNTA] [RECORTAR GASTOS ›] ││                                                │
│ ⚠ over-budget alert (1 line)              ││                                                │
└───────────────────────────────────────────┘└────────────────────────────────────────────────┘
```
- Stepper ±10% via `Ui.Stepper`. Org name/owner, multi-team list, `forecast.note`, `finance.seasonPlan` → `[i]`s.

### 3.14 Sponsors (+ offer detail)

**Problems:** list items with 2-line dim details (industry · status · until · mood + objectives); offers each with a filled "Abrir" primary.

**Target**
```
[ CONTRATOS 3/4 | OFERTAS 2 ]   (Segmented)
┌ RED BULL › ┐┌ MOTUL › ┐┌ ALPINESTARS › ┐┌ + HUECO LIBRE ┐     tiles 3 cols, h 200, wrap rows
│ €400k/TEMP.││         ││               ││ (dim)          │
│ [ACTIVO]   ││[COND.]  ││[DESDE 2027]   ││                │   status pill + mood dot (rating colour)
└────────────┘└─────────┘└───────────────┘└────────────────┘
```
- Deal tile tap → Sheet: industry, status, until, mood, objectives list.
- Offer tile: name, €/season, pills "2 TEMP." + "CADUCA EN 3"; tap → offer detail.
- Offer detail: left card KPIs (€/temp, temporadas, prima) + objectives card right; bottom-right: RECHAZAR (Ghost) · CONTRAOFERTA (Secondary) · ═ACEPTAR═. Penalty/exclusive as `Ui.Alert`. `sponsor.offersInfo` → `[i]`. Back = header chevron.
- Empty: `Ui.Empty(Sponsor, "SIN OFERTAS")`.

### 3.15 Shop (ShopScreen.cs)

**Problems:** 6-column tables with a "Gestionar"/"Añadir" button in every row; 2 notes per view; detail mixes KPIs, 4-button stepper, lines, paragraphs, chips and tables.

**Target**
- Segmented: RESUMEN | PRODUCTOS n | CATÁLOGO n | CAMPAÑAS n.
- Resumen: StatTiles (AFICIÓN, INGRESOS, NETO) + account card (≤ 8 lines). Units/lost sales, stock spend, inventory → into account card; hype/marketing → 1 Alert/Good pill; notes → `[i]`.
- Productos: `ListRow` cols PRODUCTO | PRECIO | STOCK | BENEFICIO + chevron (drop "Vendidos" → detail). Retired → dim + pill "RETIRADO".
- Catálogo: cols PRODUCTO | PRECIO REF. | DEMANDA + Secondary "AÑADIR"; cost & terms → row tap sheet. Locked products → one Ghost row "BLOQUEADOS (n) ›" → sheet list with unlock reason.
- Campañas: cols CAMPAÑA | TOTAL | IMPULSO | RESULTADO + action Secondary; notes → `[i]`.
- Detail: header back + header-right Secondary RETIRAR/REACTIVAR; left KPIs (stock, margin, demand) + `Ui.Stepper` price ±5% + cost line; by-event/terms/shared → `[i]`; right Segmented PRECIO | PEDIDO tables (keep, ≤ 4 cols each) ; LIQUIDAR → `Ui.Danger`.

### 3.16 Market (MarketScreen.cs)

**Problems:** category chips (6+) + filter chips + pager + 6-col table with an accent "Abrir" in every row + 2 long notes.

**Target**
```
[MOTO2 ▾] [ FIN CONTRATO | TODOS | OJEADOS ]           PLAZAS: 1 AHORA · 2 EN 2027 [i]   [‹] 1/5 [›]
┌ # ┬ PILOTO ─────────────────┬ EDAD ┬ EQUIPO ──────────┬ NIVEL ┬ POTENC. ┬ › ┐   ListRow 64, scroll
│[72]│ ● MÁRQUEZ  ESP [€ APORTA]│ 31  │ GRESINI  2026    │  72   │  ★★★    │ › │
```
- Category → `Ui.Picker` (incl. "LIBRES"). Rating badge as position block. Max 1 tag pill per name (priority: signed > brings money). Notes → `[i]`.
- Profile: left hero card (name, nation · age, rating badge) + KPIs (NIVEL, POTENCIAL, CONOCIDO %) + 6 key bars; right situation lines (team, until, style, brings) + `Ui.Alert` if already signed; bottom-right OJEAR (Secondary toggle) · ═HACER OFERTA═; all attributes → `[i]`. Back = header chevron.

### 3.17 Standings (InfoScreens.cs)

**Problems:** a head card with **two chip rows** (categories + table type); 7 columns.

**Target**
```
[ PILOTOS | EQUIPOS | FABRICANTES | EVOLUCIÓN ]                                 (Segmented)
┌ MOTO2 · PILOTOS                       RONDA 5/20     [MOTO2 ▾] ┐   Band + Picker at right
│ #  PILOTO                 EQUIPO            V      PTS        │   ListCaptions
│[1] MÁRQUEZ                GRESINI           3      120      › │   ListRow 64, scroll
│ ... pinned own row at bottom ...                              │
```
- Riders: # | PILOTO | EQUIPO | V | PTS; row tap → Sheet with points, wins, podiums, poles. Teams/makers: # | NOMBRE | V | PTS.
- Evolution chart unchanged (axis labels ≥ 18).

### 3.18 Calendar

Championship chips → Picker when > 4. Rows (64): R# block | "29 MAR" | GP name Bold + circuit dim on a second line | winner. Next round row = accent highlight + pill "PRÓXIMA". Drop separate circuit column.

### 3.19 History

View segmented (CAMPEONES | TEMPORADAS | RÉCORDS | SALÓN) + category → Picker (one row total). Seasons: 3-column grid of compact cards (header "2025 · MOTO2", 1º/2º/3º lines; team/maker/most wins/rookie/retired → card tap sheet). Hall table 6 → 4 cols (#, PILOTO, TÍTULOS, VICTORIAS; "top-category" splits in row sheet). Empty → `Ui.Empty(Trophy, ...)`.

### 3.20 Career (CareerScreens.cs, TeamSetupPage.cs, FoundingPanel.cs)

- Tabs: RESUMEN | OFERTAS | ORGANIZACIÓN. `TeamSetupPage`, `CareerFoundPage` = `HiddenTab`.
- **Resumen:** left: profile card (name, background caption) + KPIs (patrimonio, salario, fama) + 5 skill bars always visible (no expander); right: EMPLEO card ≤ 5 lines (team, role, until, autonomy, relation) + confidence bar; confidence advice → `[i]`; renewal card only when an offer exists (PrimaryCTA ACEPTAR); DIMITIR → header `⋯` → Danger; history → Ghost "HISTORIAL ›" sheet. No job: `Ui.Empty` + PrimaryCTA VER OFERTAS + Secondary FUNDAR (→ `Show<CareerFoundPage>`).
- **Ofertas:** tiles per offer (team, category pill, salary key, objective status); tap → Sheet with details + PrimaryCTA ACEPTAR (two-tap confirm when breaking a contract). Notes → `[i]`.
- **Organización:** left org card KPIs (cash, debt, wealth) + team Picker (if > 1) + crisis Alert + crisis actions (Secondary loan / inject, two-tap); right Segmented views (DIRECCIÓN | CONTRATAR | MOVER | VENDER | CERRAR → ≤ 5 ok). Add Secondary buttons "CONFIGURAR EQUIPO ›" (→ TeamSetupPage) and "FUNDAR OTRO EQUIPO ›" (→ CareerFoundPage) in the org card header `⋯`.
- **TeamSetupPage (wizard):** step Segmented stays; bottom bar: Ghost "‹ ANTERIOR" left, PrimaryCTA "SIGUIENTE ›"/"LISTO" right. Each step ≤ 2 cards; per-row "NEGOCIAR"/"CONTRATAR"/"ACEPTAR" = Secondary. Notes → `[i]`.
- **Found page / FoundingPanel:** left: CAPITAL card (invest Segmented 25/50/75/100, loan Segmented 0/½/MAX, one line TOTAL) + categories ListRows (CATEGORÍA | VEREDICTO pill | ARRANQUE) tap = select (reasons/lowest → row sheet); right: IDENTIDAD (supplier Segmented, name input, 2 swatch rows) + PrimaryCTA FUNDAR bottom-right. Loan terms/notes → `[i]`.

### 3.21 News

The hub overlay → `Ui.Sheet("NOTICIAS")`, rows "29 MAR  headline" (important = Bold + accent edge), up to `MaxNews`; empty → `Ui.Empty(News, ...)`. Opened from top-bar News icon.

### 3.22 Pre-race (PreRaceDialog.cs)

Close to the reference. **Problems:** weather uses glyphs ☂☁☀; engineer advice sentence at 16 units; tyre slots cycle on tap (hidden options); track traits as a dim run-on line.

**Target**
```
┌ GP DE CATAR   [22 VUELTAS]                                          [═ SALIR A PISTA › ═] ┐ bar 72
│ LOSAIL  [RÁPIDO] [ABRASIVO]  (≤2 pills, rest [i])        ┌ [sun] 20% LLUVIA  AIRE 24° PISTA 31° ┐│
├──────── rider card ──────────────────────┬──────── rider card ───────────────────────────┤
│ [#93] Marc MÁRQUEZ               [P3]    │ ...                                            │
│ ┌ DELANTERO ┐ ┌ TRASERO ┐                │                                                │
│ │ (tyre) M  │ │ (tyre) S│  ING: M/S pill │                                                │
```
- Tyre slot tap → **Sheet tyre picker** like the reference: one card per allowed compound (colour ring, name, wear hint, wet range for inter/wet), "SELECCIONAR" Secondary / "ACTIVO" accent. Engineer advice = pill "ING: M/S".
- Weather icons via `UiIcons.Sun/Cloud/Rain`. `Ui.SheetHost` = this panel's root.

### 3.23 Qualifying

Keep big player cards (grid slot in Timing block). Grid table → `ListRow` cols P | PILOTO (team colour bar, own row accent) | MEJOR | DIF.; drop TEAM column. CONTINUAR = top-right of a 72 top bar (race-flow placement), title "CLASIFICACIÓN · GP name" left.

### 3.24 Race HUD (RaceHud.cs) + RiderCommandPanel.cs

**Problems (screenshots 2–4):** top bar has title, lap, two-line conditions, flag, 5 speed buttons, skip; tower rows 34 high with 12–14-unit text (unreadable, not touch-safe); 3 camera buttons + tower title + gap toggle in a second strip; rider cards: 2 neighbour rows, F/R wear text, fuel, last/best lap, 3 buttons.

**Target**
```
┌ VUELTA 12/26 │[VERDE]│[cloud] PISTA 31° · 12% MOJ.          [1×|2×|4×|8×] [❚❚] [⏭] [cam] ┐ 64
├ TOWER 22% ─────────┐                                                ┌ #93 MÁRQUEZ  P5  1:42.3 ┐
│ 1 ▌GUE   LÍDER     │                (track view)                    │ ▲ P4 ROB   −0.4         │
│ 2 ▌ROB   +0.050    │                                                │ ▼ P6 TAN   +0.3         │
│ ... rows 40 high,  │                                                │ DEL M ▬▬▬▬░ 82%          │
│ 3-letter code 22,  │                                                │ TRA S ▬▬▬░░ 64%          │
│ gap 20; own rows   │                                                │ [AHORRO|NORMAL|ATAQUE]   │
│ accent             │                                                │ [DEFENSA|NORMAL|ATAQUE]  │
│ [INTERVALO ▾]      │      feed: 2 lines, bottom-centre, fades       │ [AUTO]                   │
└────────────────────┘                                                └──────────────────────────┘
```
- Session title moves to the start banner only. Conditions single line. Speeds = Segmented; pause/skip = IconButtons; 3 camera buttons → one `Camera` IconButton cycling Overview → Follow → Sector (label toast shows mode).
- Tower: rows 40, team bar, code, gap; tyre letters only on own riders; gap/interval toggle as a pill at the bottom of the tower.
- Rider card: pace and attitude as 3-segment controls (replace cycling buttons — state always visible); fuel row only with a tank limit; last lap in header right; best lap dropped from card (it is in the post-race and feed). Status (issue, long lap) = Warning pill in header.
- Replay bar: ‹ VUELTA / VUELTA › Secondary + ═RESULTADOS═ primary.

### 3.25 Post-race (PostRacePanel.cs)

**Problems:** tabs as small buttons; 8-column classification; rider page 2 KPI rows (8 numbers) + sector table + why list.

**Target**
- Optional first tab **PODIO** (reference): three cards P2 | P1 | P3 (number, surname, team, time/gap), own riders accent edge.
- Tabs (Segmented at top, max 5): PODIO | CLASIFICACIÓN | DIFERENCIAS | <own rider 1> | <own rider 2>.
- Classification ListRow: P (+ gained arrow ▲3 Good / ▼2 Bad small) | PILOTO (team bar) | TIEMPO/DIF. | MEJOR VUELTA | PTS | notes as icons (fastest lap purple dot, penalty, DNF pill). Team column dropped.
- Rider page: KPIs (SALIDA, LLEGADA, PTS, MEJOR VUELTA); 4 secondary numbers (top speed, passes, errors, gained) as `Ui.Line`s; sector table keep; "¿POR QUÉ?" max 3 strengths + 3 weaknesses visible, rest in `[i]`.
- Bottom-right: REPETICIÓN (Secondary) · ═CONTINUAR═.

### 3.26 Event summary (EventSummaryPanel.cs)

Per rider card: KPIs ≤ 4 (CLASIF., CARRERA 1[/CARRERA 2 merged "P5 / P3"], PTS); championship move as pill "P8 → P6 ▲2"; best lap / passes / gained as 3 lines; analysis 1 strength + 1 weakness visible, rest `[i]`. Team card: KPIs (PTS, CAMPEONATO, DINERO) + move pill; both notes → `[i]`. Session tabs Segmented (≤ 5; more sessions → Picker). Session tables → ListRow 5 cols (team col dropped, own row Timing tint). CONTINUAR primary bottom-right.

### 3.27 Season review (SeasonReviewPanel.cs)

```
┌ TU TEMPORADA (4 cols) ────┐┌ LA JUNTA (4 cols) ─────────┐┌ CAMPEONES (4 cols) ─────────┐
│ [P4/12][✓ TOP 5][+€1.2M]  ││ SATISFECHA (TextTitle, mood)││ MOTOGP   MÁRQUEZ            │
│ MÁRQUEZ   P3   ›          ││ CONFIANZA ▬▬▬▬▬░ 72%        ││ MOTO2    ...                │
│ GÓMEZ     P9   ›          ││ + Objetivo cumplido         ││ OFERTAS DE MÁNAGER (2) ›    │
│ PRESUPUESTO €5.8M / €6.1M ││ − Presupuesto superado [i]  ││                             │
└───────────────────────────┘└─────────────────────────────┘└───────────────────[═CONTINUAR═]
```
Reasons: top 3 visible, rest `[i]`. Offers → Sheet list (+ `review.offersNote`). Rider row tap → line sheet (points, wins, podiums).

### 3.28 Boot screen (BootScreen.cs)

Left 5 cols: game title/logo, PrimaryCTA CONTINUAR (when autosave), Secondary CARGAR, Secondary NUEVA CARRERA, status caption (load errors as Alert). Right 7 cols: intro card only when no save exists (else hidden). Language / text size / audio → `Settings` IconButton top-right → Sheet. Save slots → Sheet with `PersonRow`-style rows (slot, team, meta, CARGAR Secondary).

### 3.29 Career start flow (CareerStartFlow.cs)

**Problems:** tables of 7–9 columns (backgrounds 8, jobs 7, categories 9), long paragraphs, duplicate founding UI.

**Target:** step dots header (PERFIL › CAMINO › EMPLEO/FUNDAR). Profile: name inputs + nationality Picker; backgrounds = grid of tiles (name, 2 best-skill pills, chosen = accent edge); description + all 7 numbers → `[i]` per tile. Path: two big tiles "BUSCAR EMPLEO" / "FUNDAR EQUIPO" (each = whole-tile button, 1-line caption). Jobs: tiles (team, category pill, salary key, objective) → Sheet with budget, autonomy, owner, seasons + PrimaryCTA ACEPTAR. Founding: **reuse `FoundingPanel.Build`** (delete the duplicated founding UI). Back = Ghost bottom-left, primary bottom-right.

---

## 4. Implementation plan (work packages)

Order: **WP1 first** (it defines the API). WP2–WP5 may start immediately against the signatures in §2 but merge after WP1. Files are owned by exactly one package; nobody edits another package's files. New loc keys go to `Resources/Localization/ux_es/wpN.json` and `ux_en/wpN.json` (each package its own file).

### WP1 — Design system, shell, shared components (blocking)

**Owns:** `UI/UiFactory.cs`, `UI/Components.cs`, `UI/UiScale.cs`, `UI/Charts.cs`, `UI/Loc.cs`, `UI/LoadingOverlay.cs`, `Screens/ManagementShell.cs`, `Screens/TeamCards.cs`, `Screens/BikeCards.cs`, `Screens/MobileParts.cs`, `Localization/ux_es|ux_en/wp1.json`.

- [ ] Theme tokens (§1.2, §1.4): `TextDisplay..TextMin`, `Tile`, `HeaderFill`, `Perf`, `Rel`, `OnAccent` (luminance rule), `Signed`, `Pos`.
- [ ] Raise every `resizeTextMinSize` in owned files to ≥ 18; remove hatch patterns from `Ui.Card`, `Ui.Band`, `TeamCards.Strip`; card padding 16.
- [ ] `UiIcons`: add the 22 icons of §1.12.
- [ ] All components of §2 with the exact signatures; `Ui.Kpis` asserts ≤ 4 and gets StatTile styling; `Ui.Item` min height 64 + Secondary action style; `ListRow(onTap)` overload with chevron; `Chips` → forwards to `Segmented`.
- [ ] `Ui.Sheet` + `SheetHost` + `CloseSheet`; `InfoButton`; `Picker`; `Danger` two-tap with static armed key cleared on sheet close/page change.
- [ ] `Ui.Note` → `[Obsolete]`, behaviour unchanged.
- [ ] `TeamCards`/`BikeCards`/`MobileParts`: delegate to `Ui.*` (`Tile`, `Strip`→`HeaderStrip`, `RatingSquare`/`Square`→`RatingBadge`, `Gauge`, `Pips`, `Segments`, `Stepper` → `Ui.Stepper` with ±5% semantics kept by callers). Keep signatures compiling.
- [ ] `Loc.Load` merges `Resources.LoadAll<TextAsset>("Localization/ux_" + lang)`; fallback chain unchanged.
- [ ] Shell (§3.1): rail 128 fixed, top bar 72, tabs 56 (≤ 4, `ScreenBase.HiddenTab`), pills cash + championship only, Inbox + News icon buttons. Inbox: add `public System.Func<List<KeyValuePair<string, System.Action>>> InboxSource;` on the shell (WP2's HubScreen assigns it; badge = count; sheet rows call the action after `Ui.CloseSheet()`). News: move the category filter from HubScreen into the shell as `public IEnumerable<NewsItem> OwnNews()` and build the news sheet there (§3.21), race button secondary-accent + hidden on Hub, Menu → Sheet, `Ui.SheetHost = root`.
- [ ] Charts: axis/legend labels ≥ 18.
- [ ] Deliver a short API note at the top of Components.cs (doc comments per method).

### WP2 — Hub, Team, Market, Negotiation

**Owns:** `Screens/HomeScreen.cs`, `Screens/TeamScreens.cs`, `Screens/MarketScreen.cs`, `Screens/NegotiationPanel.cs`, `UI/Assistant.cs`, `ux_*/wp2.json`.

- [ ] Hub per §3.2: remove strip & news overlay; set `Shell.InboxSource` from `Todos(...)`; tiles via `Ui.Tile/StatTile`; finance tile = remaining budget; race tile with single PrimaryCTA + `⋯` simulate sheet; no-team variant.
- [ ] Riders overview §3.3 (drop key line/under-line; pills for bids/pager; free-seat primary).
- [ ] Rider detail §3.4 (header back, contract sheet, attributes `[i]` sheet replacing toasts + expander, actions row with one primary + Danger release).
- [ ] Stats view §3.5; Staff overview §3.7 (ceiling `[i]`); Staff role detail §3.8 (holder `⋯` sheet, Secondary hire buttons, `[i]` for role info); Manager page §3.9 (+ team reputation).
- [ ] Market list + profile §3.16 (Picker, one segmented row, no per-row buttons, notes → `[i]`).
- [ ] Negotiation + Bids §3.6 (Stepper ±5%, clauses sheet, single PROPONER primary).
- [ ] Replace every `Ui.Note` in owned files with `InfoButton`/`Alert`/`Empty`; no `FontSmall - n` below 18.
- [ ] Verify Assistant tutorial keys still point at existing elements (`tut.home.*` text only).

### WP3 — Bike, Finance, Shop

**Owns:** `Screens/BikeScreens.cs`, `Screens/FinanceScreens.cs`, `Screens/ShopScreen.cs`, `ux_*/wp3.json`.

- [ ] Bike overview §3.10 (non-scrolling; perf bars + maker in `[i]` sheet; no rating squares; statuses only when frozen/in dev).
- [ ] R&D §3.11 (one INICIAR primary with blocker sub-line; trade-off pill; `[×]` cancel with arming; history sheet; notes → `[i]`).
- [ ] Facilities §3.12 (2×5 StatTiles, cost pill, sheet with description + MEJORAR primary; delete bottom fold).
- [ ] Finance §3.13 (4 StatTiles, budget card with Stepper/board actions, view segmented, notes → `[i]`).
- [ ] Sponsors §3.14 (tiles for deals/offers, deal sheet, offer detail with one primary).
- [ ] Shop §3.15 (4-col lists, row-tap sheets, locked list sheet, detail with Stepper, LIQUIDAR Danger).
- [ ] Replace every `Ui.Note`; no fonts < 18.

### WP4 — Competition, Career, Boot/start, Season review

**Owns:** `Screens/InfoScreens.cs`, `Screens/CareerScreens.cs`, `Screens/TeamSetupPage.cs`, `UI/FoundingPanel.cs`, `UI/CareerStartFlow.cs`, `UI/BootScreen.cs`, `UI/SeasonReviewPanel.cs`, `ux_*/wp4.json`.

- [ ] Standings §3.17 (one segmented row + category Picker in Band; 5 cols; row sheet with podiums/poles).
- [ ] Calendar §3.18, History §3.19.
- [ ] Career tabs §3.20: mark `TeamSetupPage` and `CareerFoundPage` `HiddenTab => true`; add entry buttons in Org (`⋯`) and Summary (no job); Resumen/Ofertas/Organización layouts.
- [ ] TeamSetupPage wizard nav (Ghost back left, primary right; Secondary per-row actions).
- [ ] FoundingPanel §3.20; CareerStartFlow §3.29 reusing `FoundingPanel.Build`; Boot §3.28 (settings sheet, slots sheet); Season review §3.27 (3 columns, CONTINUAR primary bottom-right).
- [ ] Set `Ui.SheetHost` in BootScreen/CareerStartFlow/SeasonReviewPanel roots.
- [ ] Replace every `Ui.Note`; no fonts < 18.

### WP5 — Race flow

**Owns:** `UI/PreRaceDialog.cs`, `UI/QualifyingPanel.cs`, `UI/RaceHud.cs`, `UI/RiderCommandPanel.cs`, `UI/PostRacePanel.cs`, `UI/EventSummaryPanel.cs`, `UI/RaceFlow.cs`, `ux_*/wp5.json`.

- [ ] Every race-flow panel: 72 top bar, title left, CTA top-right (pre-race, qualifying) or bottom-right (post-race, summary); `Ui.SheetHost` = panel root.
- [ ] Pre-race §3.22 (icon weather, traits pills ≤ 2, tyre-picker sheet, engineer pill).
- [ ] Qualifying §3.23 (ListRow 4 cols).
- [ ] Race HUD §3.24 (single-line top bar, speed segmented + icon buttons, camera cycle button, tower rows 40, feed 2 lines, replay bar).
- [ ] RiderCommandPanel §3.24 (segmented pace/attitude, fuel only with limit, status pill).
- [ ] Post-race §3.25 (podium tab, 6-col classification with icons, rider page reduction).
- [ ] Event summary §3.26.
- [ ] Replace every `Ui.Note`; no fonts < 18; replace glyphs (☂☁☀◄►) with icons.

### Acceptance checklist (all packages)

- [ ] No screen shows more than one filled-accent button; no list row has a filled-accent button.
- [ ] No visible paragraph > ~60 chars on overview pages; all explanations reachable via `[i]`.
- [ ] No `resizeTextMinSize` or font < 18; all tappables ≥ 56 hit.
- [ ] Overview pages (Hub, Riders, Staff, Manager, Bike, Facilities, Finance, Sponsors) do not scroll at 1600×730.
- [ ] Every action listed in §3 is still reachable (walk each list).
- [ ] Spanish and English texts fit (Spanish is ~25% longer: test with es).
