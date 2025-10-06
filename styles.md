# ReMux2 UI Style Guide

## 1. Design Goals
ReMux2’s interface should feel clean, modern, and focused.  
The design draws inspiration from **Windows 11 Fluent Design** and **VS Code** — functional first, minimal distraction.

**Core goals**
- Prioritize clarity and speed of use.  
- Keep contrast high, visuals restrained.  
- Emphasize main actions (drag-drop, encode controls).  

---

## 2. Colour Palette

| Element | Colour | Notes |
|----------|---------|-------|
| Background (Main) | `#1E1E1E` | Matte dark grey, eye-friendly. |
| Panel / Card | `#252526` | Slightly lighter section background. |
| Text (Primary) | `#FFFFFF` | Main text. |
| Text (Secondary) | `#B0B0B0` | Labels, placeholders. |
| Accent | `#0078D7` | Windows blue, used for buttons and highlights. |
| Border / Divider | `#3C3C3C` | Subtle separation lines. |
| Error / Warning | `#D83B01` | Windows orange. |
| Success | `#107C10` | Calm green. |

Optional **Light Theme**: background `#F3F3F3`, text `#202020`, accent `#0078D7`.

---

## 3. Typography

| Element | Font | Size | Weight |
|----------|------|------|--------|
| Titles / Headers | Segoe UI Variable | 18px | Semi-Bold |
| Section Headings | Segoe UI Variable | 14–16px | Bold |
| Labels / Dropdowns | Segoe UI Variable | 12–13px | Normal |
| Console / Log Output | Consolas or JetBrains Mono | 11px | Regular |

Use **consistent casing**: uppercase for section headers, sentence case for buttons.

---

## 4. Layout & Spacing

- Align all UI elements to a **4 px grid**.  
- Use **12–16 px padding** between grouped elements.  
- Apply **8 px corner radius** for panels and buttons.  
- Maintain consistent button height (≈ 36 px).  

**General Layout Example**
