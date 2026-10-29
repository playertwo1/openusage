---
name: OpenUsage for Windows
description: A quiet tray panel with one grouped list of 48px account rows, double mini rings, thin purple and cyan meters and provider-reported limits only.
colors:
  background: "#FCFCFD"
  surface: "#F2F2F5"
  raised: "#FFFFFF"
  foreground: "#202127"
  muted: "#64656D"
  border: "#E0E1E6"
  track: "#DDE0E5"
  accent: "#9B30D0"
  on-accent: "#FFFFFF"
  accent-soft: "#F3E8FB"
  s5h: "#9B30D0"
  swk: "#0A8FC2"
  warning: "#A96500"
  danger: "#C73A33"
  success: "#22824B"
  background-dark: "#18191D"
  surface-dark: "#24262C"
  raised-dark: "#30333A"
  foreground-dark: "#F4F4F6"
  muted-dark: "#A9ABB5"
  border-dark: "#393C46"
  track-dark: "#42454E"
  accent-dark: "#BF5AF2"
  on-accent-dark: "#1B0A26"
  accent-soft-dark: "#3A2A4D"
  s5h-dark: "#BF5AF2"
  swk-dark: "#64D2FF"
  warning-dark: "#FFD60A"
  danger-dark: "#FF453A"
  success-dark: "#30D158"
  mini-surface: "#1C1C1E"
typography:
  heading:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "20px"
    fontWeight: 600
  title:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "15px"
    fontWeight: 600
  brand:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "17px"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "13px"
    fontWeight: 400
  row-name:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "13px"
    fontWeight: 600
  row-number:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "14px"
    fontWeight: 600
  metric-label:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "12.5px"
    fontWeight: 600
  account-name:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "12px"
    fontWeight: 600
  value:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "11.5px"
    fontWeight: 400
  caption:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "11px"
    fontWeight: 400
  reset:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "10.5px"
    fontWeight: 400
  footnote:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "10px"
    fontWeight: 400
  mini-label:
    fontFamily: "Segoe UI Variable, Segoe UI"
    fontSize: "9px"
    fontWeight: 600
rounded:
  mini-pill: "14px"
  mini-flyout: "8px"
  meter: "2px"
  control: "8px"
  field-menu: "6px"
  segment-group: "9px"
  segment-theme: "7px"
  switch: "8px"
  keycap: "4px"
  badge: "4px"
  list: "10px"
  card: "12px"
spacing:
  xs: "4px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "20px"
  page-gutter: "22px"
components:
  list-row:
    backgroundColor: "{colors.surface}"
    height: "48px"
    padding: "0 12px"
  list-row-expanded:
    backgroundColor: "{colors.raised}"
  card:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.card}"
    padding: "15px 16px 12px"
  account-list:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.list}"
  account-head:
    height: "40px"
    padding: "0 8px 0 12px"
  plan-badge:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
    rounded: "{rounded.badge}"
    padding: "1px 6px"
  metric-line:
    height: "34px"
    padding: "0 12px 0 8px"
  account-footer:
    height: "26px"
    padding: "0 12px"
  settings-row:
    height: "44px"
    padding: "0 12px"
  choice:
    backgroundColor: "{colors.raised}"
    rounded: "{rounded.field-menu}"
    height: "22px"
  segment-theme:
    backgroundColor: "{colors.track}"
    rounded: "{rounded.segment-theme}"
    padding: "2px"
  switch:
    backgroundColor: "{colors.track}"
    rounded: "{rounded.switch}"
    height: "16px"
  switch-on:
    backgroundColor: "{colors.accent}"
  keycap:
    backgroundColor: "{colors.track}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.keycap}"
    padding: "1px 6px"
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    rounded: "{rounded.control}"
    padding: "7px 11px"
    height: "32px"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.foreground}"
    rounded: "{rounded.control}"
    padding: "7px 11px"
    height: "32px"
  button-quiet-hover:
    backgroundColor: "{colors.surface}"
  button-quiet-pressed:
    backgroundColor: "{colors.border}"
  segment-selected:
    backgroundColor: "{colors.raised}"
    rounded: "{rounded.control}"
    padding: "6px 12px"
  segment-group:
    backgroundColor: "{colors.surface}"
    rounded: "{rounded.segment-group}"
    padding: "3px"
  input:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.foreground}"
    rounded: "{rounded.control}"
    padding: "9px 10px"
  mini-pill:
    backgroundColor: "{colors.mini-surface}"
    rounded: "{rounded.mini-pill}"
    padding: "7px 14px 7px 10px"
  mini-cell:
    width: "28px"
  mini-bar:
    height: "4px"
    rounded: "{rounded.meter}"
  mini-flyout-row:
    height: "34px"
    padding: "0 14px"
  meter-track:
    backgroundColor: "{colors.track}"
    rounded: "{rounded.meter}"
    height: "3px"
  meter-fill-5h:
    backgroundColor: "{colors.s5h}"
    rounded: "{rounded.meter}"
    height: "3px"
  meter-fill-week:
    backgroundColor: "{colors.swk}"
    rounded: "{rounded.meter}"
    height: "3px"
---

# Design System: OpenUsage for Windows

## Overview

**Creative North Star: "The Quiet Gauge"**

A 440-logical-pixel tray panel that reads like a household meter: one grouped list, one 48px row per account, two thin bars per row. It adapts the macOS edition (off-white and graphite surfaces, grouped corners, original OpenUsage and provider marks) using native WPF controls and Segoe UI Variable. The old utility-style Windows interface is the anti-reference.

Density is calm. The Overview row carries a mini double ring, the account, a pace verdict and two numbers with bars (5h and Week). Expanding a row reveals the weekly pace chart. Everything shown is provider-reported; there is no spend overview.

The window is opaque in light and dark: no title bar chrome from the default frame, Windows 11 rounded corners requested through DWM (Windows 10 ignores it). There is no blur, acrylic or transparency.

**Key Characteristics:**
- Tonal layering, no shadows: depth comes from Background, Surface and Raised steps.
- One accent, purple. It is also the 5h window colour; the Week window uses its own cyan data tone, not a second accent.
- Alerts live in the number only; bars never change colour with severity.
- Provider marks and the app mark are original SVG assets; interface icons are drawn strokes.
- Light, dark and system themes share one token set; text scales 100, 110 or 125 percent as a single transform.

## Colors

A cool off-white (light) or blue-tinted graphite (dark) neutral ladder with a single purple accent, one cyan window tone and three semantic hues. Light and dark tokens are separate frontmatter entries (`-dark` suffix); the app swaps them by role name.

### Primary
- **Signal Purple** (`accent`, `accent-dark`): focus ring, text-field focus border, filled stars, selected switch, primary button, today initial in the pace chart, pace-chart "now" dot. Identical to `s5h`.
- **Accent Wash** (`accent-soft`, `accent-soft-dark`): highlighted and selected dropdown items, text selection.

### Secondary
- **5h Purple** (`s5h`): Session meter, outer ring, 5h legend dot.
- **Week Cyan** (`swk`, `swk-dark`): Weekly meter, inner ring, pace-chart line and projection. A data tone for the weekly window only.
- **Amber Caution** (`warning`, `warning-dark`): weekly number when the projection passes 100 percent; "Runs out" verdict.
- **Signal Red** (`danger`, `danger-dark`): number at 85 percent used or more; account errors; run-out marker in the chart.
- **Confirm Green** (`success`, `success-dark`): "Lasts week" verdict.

### Neutral
- **Page** (`background`): panel and dialog canvas.
- **Card Surface** (`surface`): list group, provider cards, navigation track, settings groups, quiet-button hover.
- **Raised** (`raised`): selected segment, inputs, dropdown, switch thumb, expanded row.
- **Ink** (`foreground`): primary text and the pace tick.
- **Muted Ink** (`muted`): sub-lines, headers, resets, hints, unstarred stars, scrollbar thumb, chart even-pace diagonal.
- **Hairline** (`border`): row dividers, field borders, pressed-button fill.
- **Meter Track** (`track`): unfilled bars and rings, chart backdrop at 35 percent, switch track when off.

### Named Rules
**The Number Alerts Rule.** Severity is carried by the number alone: red from 85 percent used, amber when the weekly projection passes 100 percent, with a 5px dot before it. Bars and rings keep their window colour.

**The One Accent Rule.** There is a single accent hue (purple). Cyan marks the Week window only and never labels providers, categories or actions.

**The High Contrast Rule.** When Windows high contrast is on, every role collapses to system window, text and highlight brushes.

## Typography

**Display, Body and Label Font:** Segoe UI Variable (fallback Segoe UI), one family throughout.

**Character:** Native, compact and even. Hierarchy comes from size and a single SemiBold weight, never from a second face.

### Hierarchy
- **Heading** (600, 20px): empty-state title uses 18px.
- **Brand** (600, 17px): "OpenUsage" wordmark in the header.
- **Title** (600, 15px): the short "Settings" page title.
- **Body** (400, 13px): default text, dialogs and fields.
- **Row Name** (600, 13px): account name in the Overview row.
- **Row Number** (600, 14px): used or left percent in the 5h and Week cells.
- **Metric Label** (400, 12px): metric name on Accounts metric lines (Muted when hidden); values are 12px, SemiBold for percents.
- **Account Name** (600, 13px in Accounts list heads; 12px elsewhere): account label; pace verdict in the expanded row. Settings row labels are Medium 12px.
- **Value** (400, 11.5px): "23% used" or "100 credits".
- **Caption** (400, 11px): row sub-line, pace details, footer status, provider group header and settings group headings (SemiBold, Muted), settings hints, Theme segments, keycap labels (Medium).
- **Reset** (400, 10.5px): reset text on legacy cards.
- **Footnote** (400, 10px): column headers, compact reset next to the number, daily list, Accounts reset text, plan badge (SemiBold), account "Updated" footer.

### Named Rules
**The Sentence Weight Rule.** Only SemiBold and Medium appear. Hardcoded titles use title case.

**The Scale Once Rule.** Text size 110 or 125 percent is one layout scale on the shell or dialog root; never also resize individual fonts.

## Mini Widget

An optional floating pill, separate from the tray panel and always rendered with the dark token set (theme setting ignored). Fixed `mini-surface` fill (#1C1C1E, flyout #202020), 1px dark Hairline, 14px radius; opaque, no Acrylic, corners cut with a window Region. Each account is a 28px cell: a 9px SemiBold Muted short label (C, Cx, Cu, Cp, AG, Gk, Ol, OC, OR, Z; numbered when a provider has several accounts), then two 4px bars of 28px, 5h purple above and Week cyan below, with a 6px Warning or Danger dot at the top-right corner (Danger from 85 percent used, Warning when the week runs out). Cells sit 10px apart (padding 10/7/14/7); vertical mode stacks cells 8px apart (padding 8/6/8/10). A drag grip leads the pill.

Hover opens a 400px flyout (8px radius, 1px Hairline) on the side with room, always inside the work area: header "OpenUsage" with a 5h and Week legend and update time, one 34px row per account (22px double ring, 12px SemiBold name, 5h and Week percents with compact resets, pace verdict at right), then a 10px footer with status and "Open OpenUsage" in Accent. Right-click menu (Raised): Always on top, Lock position, Vertical layout, Open OpenUsage, Refresh now, Hide widget (Ctrl+Alt+W).

The window never takes focus and stays out of the taskbar. Drag from anywhere on the surface; within 12px of a work-area edge it settles 8px from it. Position is saved normalized (0 to 1) against its monitor's work area, so resolution changes keep it on screen.

## Layout

Single column, 440 logical px wide, minimum 400 (scaled by text size). The shell is four rows: header, segmented navigation, scrolling page, footer. The page scrolls vertically only.

Overview is a column header plus one grouped list. Header and rows share a grid: ring 28, name flexible, 5h cell 72, Week cell 84, chevron 10, with 12px side margins; header margin 12/12/12/4. Rows are 48px high, separated by 1px Hairline, inside one Surface block with a 10px radius and 8px bottom margin. The first column is the mini ring (28px, 3px stroke, outer radius 12.5 for 5h, inner 7.5 for Week); a lone window draws a single ring.

Each number cell stacks the percent (14px), an optional compact reset (10px Muted, "4h 30m") and a 3px bar. On the Week bar a 2 by 7px Ink tick marks expected use at this point of the week.

A row expands in place (Enter, Space or click) onto a Raised area inset 50px left and 12px right: verdict, "Up to N%/day left", "Projected N%", a daily-use list, update time and a More button on the left; a 168 by 32 pace chart on the right with seven day initials beneath. Empty states center three provider marks (30px), a title, a muted sentence and, on first run, one primary Add Account button.

Accounts is grouped by provider. Each group opens with a header (provider mark 12, Muted SemiBold 11px name, "N account(s)", a More button; margin 4/16/0/6), then one inset list per account: Surface block, 10px radius, 8px bottom margin, rows split by 1px Hairline. The list head is 40px (name 13px SemiBold, plan badge on Accent Wash, More menu). Each metric is a 34px line with margin 8/0/12/0 over columns 22 (star), flexible name, 64 (3px bar), 40 (value), 64 (reset), 28 (Show switch). Hidden metrics dim their name to Muted. A 26px "Updated HH:mm" footer (10px Muted, plus " - Out of date" after 5 minutes) closes the list; errors and waiting text sit in the list at 11px.

Settings is a page with 20/18/20/12 margins: a 15px "Settings" title, then three Surface groups (Appearance, Usage Display, General), each preceded by an 11px SemiBold Muted heading. Groups have 10px radius, 12px side padding and 16px bottom margin; rows are at least 44px, split by 1px Hairline, with a Medium 12px label and optional 11px Muted hint on the left and the control on the right. Spacing rhythm is 4, 8, 12, 16, 20 with a 22px gutter.

## Elevation & Depth

Flat, tonal layering. No shadows anywhere, and the window itself is opaque. Page, then Surface list or cards, then Raised for the expanded row, selected segment, fields and dropdown popups (1px Hairline and 3px padding). Disabled controls drop to 45 percent opacity. Scrollbars are an 8px transparent rail with a rounded Muted thumb at 50 percent opacity.

### Named Rules
**The Tonal Depth Rule.** Separate surfaces by background step and 1px Hairline, never by drop shadow or transparency.

## Shapes

Soft rounded rectangles. Provider cards 12px; the Overview list, account lists and settings groups 10px; buttons, fields, dropdown popup and focus ring 8px; Choice field 6px; navigation track 9px; Theme segment track 7px; plan badge and keycaps 4px; switch a full pill (28 by 16 track, 12px thumb inset 2px). Meters are 3px high with 2px radius and sit on a Track pill; rings use round caps. Chart backdrop is a 3px rounded rectangle. Icons are 24-unit stroked paths at 1.65 stroke with round caps, drawn at 14 to 17px. Provider marks are 12px in Overview rows and Accounts group headers.

## Components

### Overview Row
48px grid row (ring, name, 5h, Week, chevron). Name line: provider mark, optional filled star when a metric is starred, 13px SemiBold name. Sub-line (11px): error in Red; otherwise pace verdict (Amber if runs out, Green if lasts) plus a Muted trend ("+22%" or "on pace"); fallbacks "5h window", "Weekly pace needs a reset time", the first visible metric, or "Fetching". Missing windows show a Muted dash. Focusable, named for assistive technology, with a context menu (account actions plus Move Provider Up and Down).

### Mini Ring
Double ring: outer 5h in purple, inner Week in cyan, on Track rings. Fill follows the Used or Remaining mode.

### Pace Chart
168 by 32 canvas on a Track backdrop at 35 percent: cumulative weekly use as a 2px cyan polyline, a dashed Muted even-pace diagonal, a dashed 40 percent cyan projection to day 7 or to the 100 percent mark, a 6px purple dot for now and a red ring where the limit would be hit. Weekday initials sit below; today is purple and SemiBold.

### Buttons
- **Primary:** purple fill, `on-accent` SemiBold text, 8px radius, min height 32; hover 88 percent opacity, pressed 72. Used for Add Account in the empty state.
- **Quiet:** transparent with Ink text; hover fills Surface, pressed fills Hairline. Footer Add Account, dialogs, settings actions.
- **Icon button:** 32px square, quiet styling, every one named for assistive technology.
- **Focus:** 2px purple rounded rectangle outset 3px, on every control.

### Segmented Controls
Surface track, 3px padding, 9px radius; selected segment Raised with SemiBold text. Used for Overview, Accounts and Settings, Used and Remaining, All and Favorites, and theme.

### Account List (Accounts)
Surface fill, 10px radius, no border, one per account under its provider header. 40px head (name, plan badge in Accent Wash with Accent 10px SemiBold text, More menu); 34px metric lines (filled purple star or Muted outline star, name, 3px meter in the window colour, value, reset, Show switch); 26px Updated footer. Percent values take the alert colour; non-percent values (credits) span the bar and value columns.

### Switch
28 by 16 pill, Track when off, Accent when on, white 12px thumb inset 2px, hover 80 percent, disabled 45 percent. Used for Show on Accounts and every on/off setting.

### Choice (popup menu)
22px high, min width 76, 12px text, Raised fill, 1px Hairline, 6px radius, stacked up and down Muted chevrons at the right. Popup: Raised, 1px Hairline, 8px radius, 3px padding; highlighted and selected items use Accent Wash. Used for Text Size, Show Usage As and Reset Times.

### Theme Segments
Track-filled frame, 7px radius, 2px padding; System, Light and Dark as 11px buttons (padding 10 by 3, min height 24); the selected one is Raised and SemiBold.

### Keycaps
Ctrl, Alt and U as 11px Medium labels on Track, 4px radius, padding 6 by 1, 4px apart, 1px Hairline underline. Display only: the Global Shortcut is not editable.

### Inputs and Menus
Raised fill, 1px Hairline, 8px radius, padding 10 by 9, 13px text; focus turns the border and caret purple. Dropdowns use the 22px Choice style above. Context menus use Surface and Ink.

## Do's and Don'ts

### Do:
- **Do** pull colors from the token roles and let the theme swap light and dark values.
- **Do** put alerts in the number: red from 85 percent used, amber when the projection passes 100.
- **Do** keep bars 3px with window colours (5h purple, Week cyan, other metrics purple) regardless of severity.
- **Do** name every interactive control for assistive technology and keep the focus ring visible.
- **Do** use Windows-native input: keyboard focus, Escape closes dialogs, real controls.
- **Do** show only provider-supplied values, and keep provider and account order as saved.
- **Do** keep both themes, compact layouts, empty and error states, long account names and 125 percent text free of overlap at 400 to 500 px.

### Don't:
- **Don't** add a spend chart, spend overview, cost estimate or token tally; the product shows reported limits, balances, resets and the weekly pace of reported percent only.
- **Don't** add tooltips (`hoverTooltip` or WPF ToolTip).
- **Don't** use drop shadows, gradient fills, blur or window transparency.
- **Don't** introduce a second accent hue; cyan is reserved for the Week window.
- **Don't** colour bars or rings by severity.
- **Don't** revert to the old utility-style Windows interface.
- **Don't** use hardcoded hex values in views; reference resource keys.
- **Don't** show Density or Layout controls in Settings; they had no effect and were removed.
- **Don't** make the keycaps look editable or add a shortcut recorder without wiring it.
- **Don't** add shadows, translucency, Acrylic or tooltips to the Mini Widget or its flyout.
- **Don't** put real account labels in shared review captures.
