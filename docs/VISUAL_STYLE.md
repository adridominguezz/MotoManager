# VISUAL STYLE — MotoManager (Android, landscape)

## Resumen (para el usuario)

- **Qué se ha hecho:** una pasada **solo de estilo** (colores, tipografía, botones, bordes, radios, sombras, degradados, cabeceras, etiquetas). **No se ha movido, añadido ni quitado** ninguna sección, baldosa, lista, botón, página ni flujo, y no hay iconos nuevos.
- **Estética MotoGP (web oficial), traducida a nuestro juego oscuro:** superficies casi negras con texto claro; **un rojo de carrera** (#D80F0A) para el botón principal y para los estados "en directo / próximo"; títulos y números grandes en una letra **muy gruesa, ancha y recta**; texto normal en una sans limpia y en **minúscula normal** (no todo en mayúsculas); botones en forma de **cápsula** (principal rojo relleno, secundarios con borde fino); la **barra roja inclinada "/"** antes de cada título; **bloques de dato** con leyenda pequeña + número enorme y una barra roja fina a la izquierda; **línea roja fina en la base** de baldosas, tarjetas y hojas; **brillo rojo oscuro** muy suave detrás de la cabecera; filas de clasificación con número de posición grande y sin "placa", líneas finas entre filas.
- **El color del equipo se mantiene como identidad:** tu fila en las tablas (degradado del color del equipo + borde), barra lateral del menú, línea bajo la barra superior, subrayado de pestañas, bordes de los botones "secundario de acento", bandas de clasificación, chapas de dorsal. El **botón principal pasa a rojo de carrera siempre** (antes era el color del equipo): así es igual de reconocible con cualquier equipo y nunca choca con equipos de color claro.
- **Fuentes (licencia OFL, incluidas con su licencia):** **Russo One** para títulos y números (parecida en espíritu a la de MotoGP, que es propietaria y **no** se usa) y **Titillium Web** para el resto. Si faltan, el juego vuelve a la fuente integrada de Unity.
- **Correcciones de accesibilidad:** el rojo de "negativo" en textos no llegaba al contraste mínimo (3,8:1) → ahora 5,9:1; el texto en color del equipo se aclara automáticamente hasta 4,5:1 (antes, con equipos azul oscuro, quedaba ~3:1); el texto sobre rellenos de color se elige midiendo el contraste real; el estado **pulsado** ahora se ve en todos los botones y filas (antes era casi invisible en los grises oscuros) y no se queda "resaltado" tras tocar en el móvil; "aviso" (naranja) ya no se confunde con el amarillo de cronometraje.
- **Menos ruido:** fuera sombras en los textos, degradados en todas las superficies, cursivas en todo y la mezcla de esquinas cortadas/inclinadas/redondeadas: ahora **cápsula = control**, **radio pequeño = superficie**, **inclinado = etiqueta/marca** (barra "/", etiquetas tipo "FINISHED", dorsales), **recto = datos** (barras, indicadores).

---

## 1. Audit (by skill category)

Severity: **P1** = WCAG AA failure or significant difficulty, **P2** = consistency/clarity problem, **P3** = polish.
Mode: *Operate* (app UI) — scanability and consistent states beat expression; brand lives in the details.

### 1.1 Colour & contrast (gstack §3, impeccable colorize / craft-floor)

| # | Sev | Finding (before) | After |
|---|---|---|---|
| C1 | P1 | `BadHex` #DC3737 used as **text** (negative money, deltas, weaknesses) = **3.8:1** on cards. | Text variant `BadHex` #F26B6B = 5.9:1; the fill `Theme.Bad` keeps the saturated red. |
| C2 | P1 | Accent text (`SecondaryAccent` label, `Theme.AccentText`) = accent lerped 25% to white: with dark team colours (navy, deep purple) ≈ 3:1. | `Theme.Readable(c, surface, ratio)` lightens until **≥ 4.5:1** (`Theme.AccentInk`); outline rings ≥ 3:1. |
| C3 | P1 | `Theme.OnColor` picked ink/white by a gamma-space luminance threshold (0.55): wrong for mid-tones (oranges, teals). | WCAG luminance + contrast: light text while ≥ 3.6:1 (all on-fill labels are bold ≥ 18 units = large text), otherwise the higher-contrast of text/ink. |
| C4 | P2 | Warning amber #EBB42D vs timing yellow #FFD200: hue 43° vs 49° — "attention" and "key number" looked the same. | Warning → amber-orange #F29F45 (8.2:1 as text), clearly apart from timing yellow. |
| C5 | P2 | Primary CTA wore the team colour: identical to own-row/header-tag accents, and illegible-prone on light team colours. One accent did five jobs. | **Race red** (`Theme.Action`/`Theme.Race` #D80F0A, 5.3:1 with light text) owns *action + live/next status + section slash + card closing rule*; the **team colour** owns *identity/selection* (own row, rail bar, tab underline, top-bar line, accent outlines, bands). |
| C6 | P3 | Pure white text (`Color.white`) and pure black overlays everywhere; neutrals slightly mixed. | Off-white `Text` #F4F5F7, ink-tinted overlays (`HeaderFill` = ink 30%), neutrals on one faint cool cast. |
| C7 | P3 | Hard-coded hex in race screens (#8FB4D4 pit, #B266FF fastest lap) and a light plate colour duplicated in 4 files. | Tokens `Theme.InfoHex`, `Theme.FastestHex`, `Theme.Fastest`, `Theme.Plate`. |
| — | ✓ | Dim text #969BA5 was already ≥ 5.8:1 on every surface; red/green deltas carry a sign (+/−), so colour is not the only cue. | Kept (now #A3A8B1, 6.9–7.6:1). |

### 1.2 Typography (impeccable typeset, frontend-design, gstack §2)

| # | Sev | Finding (before) | After |
|---|---|---|---|
| T1 | P2 | Unity built-in font (Arial/LegacyRuntime) as the only face — the "gave up on typography" tell. | Two OFL families: **Russo One** (display: titles, header strips, big numbers, positions, rider numbers) + **Titillium Web** (text: body, labels, buttons, captions). |
| T2 | P2 | Bold-italic CAPS on headers, tabs, selected states, CTAs, captions, empty states, status lines — italic and caps carried no information any more. | **CAPS only** for titles, header strips, buttons, tabs and tags. **Sentence case** for captions, status lines, column captions, KPI captions, stepper labels, empty states. **No italics** in the display role (upright, like the championship's graphics). Selection is shown by fill/underline, not by switching to italic. |
| T3 | P2 | Role drift: `FontStyle.BoldAndItalic` was the de-facto "title/number" role in 80+ call sites but nothing made it a real type role. | `UiFactory.Style(text, style)` is the single type-role switch: `BoldAndItalic` ⇒ display face (upright); everything else ⇒ text face with that style. All `fontStyle` assignments in the UI route through it. |
| T4 | P3 | 1.5–2 unit drop shadows under text on flat dark surfaces (muddy edges, doubled meshes). | Removed on management/race-panel surfaces; kept only where text sits on 3D/imagery (race HUD, boot, loading). |
| — | ✓ | Type scale 56/44/32/24/24/20 with an 18 floor is coherent (≈1.25–1.33 steps). | Kept unchanged; 18 floor untouched. |

### 1.3 Components, states & interaction (gstack §5, impeccable polish / component review / android)

| # | Sev | Finding (before) | After |
|---|---|---|---|
| S1 | P1 | Pressed = ×0.8 tint: invisible on graphite (#2C2F37 → #23252C) and on translucent rows/tiles. Hover tint ×1.1 **sticks after a tap** on Android. | `UiFactory.PressFeedback`: dark/translucent fills light up ×1.7 (alpha ×2.6), bright fills darken ×0.74; no hover/selected tint; 60 ms fade. Used by every button, row, tile, icon button. Ghost links dim their text. |
| S2 | P2 | Disabled primary = graphite slanted block, close to an enabled secondary. | Disabled: translucent flat capsule, dim text, no outline colour — visibly "off" for every look. |
| S3 | P2 | Danger idle = Bad at 45% (looked like an enabled red primary). | Idle: red outline capsule + red text; armed: solid red with readable text. Two states are unmistakable. |
| S4 | P2 | Shape soup: chamfers (buttons, KPI tiles, toasts), slants (CTA, pills, tabs, segments, pips, contract segments, picker, top-bar pills), rounded (inputs, badges), square (tiles, cards). | **Capsule = control** (all buttons, toggles, picker, top-bar pills, icon plates, count badges); **radius ≈ 6 = surface** (tiles, cards, sheets, KPI tiles, toasts, rating squares, inputs, alerts); **slant = tag/mark** (title slash, corner-tag pills, rider-number badges, tower position blocks); **square = data** (gauges, pips, segments). |
| S5 | P2 | Segmented control: slanted graphite blocks + accent underline, unselected nearly as heavy as selected. | Capsule toggles on a faint capsule track; selected = light capsule with ink text (16:1), others transparent. |
| S6 | P3 | Gradients on every surface and button; outlines of 5/6/7/10/14% white. | Flat surfaces; one edge token (`Edge` 8%), one divider (`Divider` 7%), one outline-button stroke (`Stroke` 38%). Gradients remain only where they carry meaning (own-row fade, hero bands, top-bar accent line). |
| S7 | P3 | Coloured left stripes as decoration (Alert, `Ui.Item` steel stripe) — "border-left accent" tell. | Removed from Alert/Item (Alert keeps its "!" tag + tint). Kept where they encode identity: own row (team colour), standings team bars, rail selection. |
| — | ✓ | Touch targets: 56 hit / 64 buttons / 72 CTA; one primary per screen. | Unchanged. |

### 1.4 Hierarchy & visual noise (impeccable critique / quieter, gstack §1 & §9)

| # | Sev | Finding (before) | After |
|---|---|---|---|
| H1 | P2 | Header strips were heavy black slabs (black 38%) with italic caps + text shadow: every tile shouted equally. | Faint ink band (30%), display caps without shadow, a small red slash. Big numbers (display, timing yellow) are now the loudest thing in a tile. |
| H2 | P2 | Standings/list rows: alternating stripes + white slanted position plates for P1–P3 + plates for the rest — busy. | Faint alternate shade + 1-unit hairline, position as a big plain display number; own row = rich team-colour gradient + 4-unit edge (the one highlighted row). |
| H3 | P3 | Kpis/StatTile numbers centred with a caps caption strip — template "big number, small label" without identity. | Stat-block treatment from the reference: dim sentence-case caption + huge display number, set left after a thin red bar. |

---

## 2. Final token set

All tokens live in `Theme` (Assets/MotoManager/Scripts/Unity/UI/UiFactory.cs); primitives in `UiFactory`, components in `Ui` (Components.cs).

### 2.1 Palette

| Token | Hex | Role | Contrast notes |
|---|---|---|---|
| `Chrome` | #030304 | top bar, rail, race-flow top bar | |
| `Background` | #08090B | page floor (+ faint carbon weave, red glow, team livery lines) | |
| `Panel` | #111317 | toasts, lists | |
| `Card` | #17191E | cards, sheets | Text 17.0:1, TextDim 7.4:1 |
| `Tile` | #1C1F24 | tiles, KPI tiles | Text 15.9:1, TextDim 6.9:1 |
| `PanelLight` | #2A2D33 | raised/selected rail cell, disabled fills | TextDim 5.8:1 |
| `Text` | #F4F5F7 | body/titles (off-white) | |
| `TextDim` | #A3A8B1 (`DimHex`) | captions, secondary values | ≥ 5.8:1 everywhere |
| `Ink` | #0A0B0D | text on light fills; tinted overlays | |
| `Race` / `Action` / `Brand` | #D80F0A | **primary CTA fill**, live/next status, title slash, card bottom rules, stat bars, glow | light text 4.8:1 (bold large) |
| `Accent` / `CurrentAccent` | team colour (clamped S .55–.9, V .62–.9) | identity: own row, rail bar, tab underline, top-bar line, accent outlines, bands | as text via `AccentInk` ≥ 4.5:1 |
| `Timing` | #FFD200 | key numbers, leader | 10.6:1 on tiles |
| `Good` | #46B973 (`GoodHex`) | covered / positive | 7.1:1 as text |
| `Warning` | #F29F45 (`WarnHex`) | attention / expiring | 8.2:1 as text |
| `Bad` (fill) / `BadHex` (text) | #DC3737 / #F26B6B | missing / negative / destructive | text 5.9:1 |
| `Info` / `InfoHex` | #96AAC3 / #8FB4D4 | neutral info, pit lane | 8.1:1 |
| `Fastest` / `FastestHex` | #B266FF | fastest lap / personal best | 5.2:1 |
| `Perf` / `Rel` | #46C86E / #C45CE2 | bike performance / reliability gauges | |
| `Plate` | #ECEEF1 | light plates behind ink numbers (race tower, grid blocks) | ink 16:1 |
| `Steel` | #7A808B | non-text details, inactive tags | |
| `HeaderFill` | ink @ 30% | header strips | |
| `Divider` | white @ 7% | row/section hairlines | |
| `Edge` | white @ 8% | 1-unit edge of tiles, cards, sheets, inputs | |
| `Stroke` | white @ 38% | outline-button rings (≈ 3:1 vs surfaces) | |

Helpers: `Theme.Contrast(a, b)`, `Theme.Luminance(c)`, `Theme.OnColor(fill)`, `Theme.Readable(c, surface, ratio)`, `Theme.AccentInk`, `Theme.OnAction`.

### 2.2 Type

| Role | Face | Size (units) | Case | Where |
|---|---|---|---|---|
| Display | Russo One (upright) | 56 `TextDisplay` | CAPS for words | hero numbers |
| Key | Russo One | 44 `TextKey` (timing yellow) | — | one key number per tile, KPI values |
| Title | Russo One | 32 `TextTitle` | CAPS | page/hero names, top bars, list positions |
| Header | Russo One | 24 `TextHeader` | CAPS | tile/card header strips, section headings, bands |
| Button | Titillium Web Bold | 28 CTA / 22 others | CAPS | all buttons, tabs, toggles, tags |
| Body | Titillium Web Regular/Bold | 24 `TextBody` | sentence | list cells, lines |
| Caption | Titillium Web Regular (Bold in column captions) | 20 `TextCaption` | **sentence** | captions, status lines, KPI captions, column captions |
| Floor | — | 18 `TextMin` | | nothing smaller, `resizeTextMinSize` ≥ 18 |

- Role switch: `UiFactory.Style(text, FontStyle)`; `FontStyle.BoldAndItalic` = display role. `UiFactory.Display(text)` is shorthand.
- Files: `Assets/MotoManager/Resources/Fonts/` — `RussoOne-Regular.ttf` (+ `RussoOne-OFL.txt`), `TitilliumWeb-{Regular,Bold,Italic,BoldItalic}.ttf` (+ `TitilliumWeb-OFL.txt`). Both SIL OFL 1.1. Loaded with `Resources.Load<Font>`; missing files fall back to Unity's built-in font (display role then falls back to bold text). Unity uses the Bold/Italic files of the same family for those styles (and synthesises a style if a file is missing).
- Glyphs: both cover Spanish accents, ñ, ¿¡, €, ·, …, –, —. Russo One has **no U+2212 minus**: Unicode-minus deltas (`Theme.Signed`) are body-text helpers; if one is ever set in the display role Unity falls back to an OS font for that glyph.
- MotoGP's own typeface is proprietary and is not used; no MotoGP logos or marks are used anywhere.

### 2.3 Shape, depth and decoration

| Token / primitive | Value | Use |
|---|---|---|
| `UiFactory.Capsule(img)` | fully rounded ends (radius > half height) | buttons, toggles, picker, top-bar pills, icon plates, count badges |
| `UiFactory.Surface(img)` / `SurfaceRadius` | ≈ 6 units | tiles, cards, sheets, KPI tiles, toasts, rating squares, inputs, alerts, list items |
| `UiFactory.Slant` | forward lean | **tags & marks only**: title slash, corner-tag pills (`Ui.Pill`), rider-number badges, race-tower/grid position blocks |
| square | — | gauges (slim, centred), pips, contract segments, hairlines |
| `UiFactory.Ring(parent, color)` | ~2-unit hollow capsule stroke | outline buttons (Secondary, SecondaryAccent, Danger idle, Picker, top-bar pills) |
| `UiFactory.Slash(parent, color)` | thin slanted red bar | before every header-strip / section / top-bar title |
| `UiFactory.BottomRule(parent)` | 2-unit race-red line | bottom of tiles, cards, sheets, career path cards (dim tiles: 35%) |
| `UiFactory.Glow(parent, x, y, size, a)` | radial dark-red glow (generated sprite) | shell background, top centre-left, 16% |
| `DropShadow` | 6-unit offset @ 60% | **only elevated layers**: sheets, toast, assistant card; text over 3D/imagery |
| Gradients | — | only meaningful: own-row team fade, path-card hero bands, accent lines |

### 2.4 Buttons and states

| Look | Fill | Border | Label | Pressed | Disabled |
|---|---|---|---|---|---|
| **Primary CTA** (`Ui.PrimaryCTA`, `UiAct` primary) | `Race` #D80F0A | — | Titillium Bold CAPS 28, `OnAction` (light) | ×0.74 darker | translucent graphite, `TextDim`, not interactable |
| **Secondary** | white 5% | `Stroke` ring | Bold CAPS 22, `Text` | fill ×1.7, alpha ×2.6 | white 2.5%, ring 12%, `TextDim` |
| **Secondary accent** | team colour 10% | team colour ring (≥ 3:1) | `AccentInk` (≥ 4.5:1) | as Secondary | as Secondary |
| **Ghost / link** | none | — | dim CAPS + chevron | text dims ×0.6 | — |
| **Danger** | red 10% → armed solid `Bad` | red ring → none when armed | `BadHex` → `OnColor(Bad)` | as Secondary / darker | — |
| **Segmented / toggles** | track white 6%; selected `Text` | — | selected `Ink`, others dim-light | light-up | — |
| **Tabs (shell)** | white 2% / selected 7% | — | Bold CAPS, selected `Text` | light-up | — |
| **Rows & tiles** (tappable) | surface | — | — | light-up (visible on translucent rows) | — |

No hover tint and no "selected" tint (touch-first; Unity otherwise keeps the last-tapped button highlighted).

### 2.5 Reference translation (motogp.com → our tokens)

| Reference trait | Our implementation |
|---|---|
| Black / charcoal surfaces, light text | `Chrome`/`Background`/`Card`/`Tile`, `Text` off-white; app stays dark (no light cards: they would break contrast with the HUD and dim captions) |
| Brand red CTAs ("SUSCRIBIRSE"), "UP NEXT" red tags | `Race` primary capsule; `Ui.Pill` slanted tags for status (callers' fills) |
| Heavy wide display caps for titles/numbers | Russo One display role |
| Clean light sans, sentence case labels | Titillium Web, sentence-case captions |
| Outline pill secondary buttons, pill toggles | `Ring` outline capsules; capsule segmented control |
| Team-colour left edges on standings rows; leader/selected row gradient | `RaceUi.TeamBar` (existing) ; own row team-colour gradient + edge |
| Thin red/grey comparison bars | slim flat `Ui.Gauge` |
| Red slash before eyebrows, red bar on stat blocks | `Slash` before titles; `StatBar` on KPI/stat tiles |
| Red slanted corner tags, thin red bottom line on cards | `Ui.Pill` slant; `BottomRule` on tiles/cards/sheets |
| Dark-red radial glow on hero areas | `UiFactory.Glow` on the shell background |
| White rounded rider-number badge | race tower `Plate` + display numbers |

---

## 3. Verification

- `cd Tests/UnityCheck && dotnet build` → 0 errors.
- `dotnet build --no-incremental -p:DefineConstants="UNITY_ANDROID%3BUNITY_2021_3_OR_NEWER"` → 0 errors.
- Contrast pairs computed with the WCAG formula (values above). No device screenshots were available in this environment: the first in-editor run should check the display face's width in the narrowest columns (best-fit shrinks to the 18 floor) and the font-style pickup of the Titillium files.
