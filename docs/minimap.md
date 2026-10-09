# Map: tiles, markers, calibration, progress, controls

## Map pictures

The map art is the **SSMap web map** (https://github.com/Kaszub09/SSMap by Kaszub09 and
contributors, built from u/magicofgames' screenshot map of the world). The mod reads a local
copy at run time from the folder set by `ssmap=` in `Mods\SaltMap.ini` (default
`Mods\SSMap`); nothing from it is copied into the mod or its releases. SSMap has no licence
file, so the tiles and markers are for personal use.

- `tiles\{z}\{x}\{y}.jpeg`: 256 px JPEGs, zoom 0-7 (XYZ). The whole map is 32768 px square
  at zoom 7; 21,820 files, 116 MB. Zoom 7 lacks 25 tiles in column 0 (empty sea).
- `markers.js`: two JS functions each returning JSON in a template string, backslashes
  doubled. 314 markers `{id, icoPath, group, category, lat, lng, popup, tooltip}`
  (Items 202, NPC 36, Sanctuaries 31, Enemies 23, Spells & Prayers 22) and 22 region labels.
- `images\markerIcons\*.png`: 17 icons, 28x28.

"Map pixels" (`Mod/MapSpace.cs`) are SSMap's pixels at zoom 7. Markers are stored as Leaflet
lat/lng in the default Web Mercator CRS: `px = 32768 * (lng/360 + 0.5)`,
`py = 32768 * (0.5 - ln((1+sin lat)/(1-sin lat)) / (4 pi))`.

The game's own unused `gfx/worldmap` (parchment) and `gfx/worldmapns` (colour render) were
tried first; see "Earlier calibration" below.

## Calibration (world units to map pixels)

`map = (133.5987, 8735.7470) + world * (0.31975016, 0.31987863)`, one origin and scale per axis.

Method (offline Python scripts, not part of the repository):
1. The level file `map\data\<mode>\fortress.zax` (`Map.Read`) ends with the collision grid:
   `xUnits` x `yUnits` cells (1587 x 1766) of 64 x 32 world units (`CharCols.GetCol`),
   run-length columns ending in -1, then a same-shaped layer grid. Found by scanning for an
   offset where all 1587 columns parse and each sums to 1766.
2. The zoom-5 tiles stitched into one 8192x4096 image (rows 8-23).
3. The collision mask placed on it by a candidate transform, edges cross-correlated in
   512 px regions, per-region shifts fitted per axis with outlier rejection, five rounds.
   About 280 regions agree to within 1 zoom-5 pixel (4 map pixels, about 12 world units).

SSMap's own `calibration.json` (3 hand-placed points close together) agrees within 5-12 map
pixels where it was taken but is not usable far from there.

*(verified)* Three logged feet positions stand on solid collision cells, so `Character.loc`
uses the collision grid's coordinates. *(verified by eye)* The marker sits where the
character stands on the minimap.

### Earlier calibration (parchment, no longer used)

`gfx/worldmap` (DXT1 4096x2048) is traced from the collision: `(122.7, 95.0) + world *
(0.038541, 0.038539)`, residual 1-2 px. `gfx/worldmapns` (DXT5, never loaded by the game)
fits worse: `(119.7, 80.5) + world * (0.038476, 0.038944)`, residual 3 / 8 px.
`PlayerMap.Draw`'s constants `(115, 80) + world * (0.03875, 0.03895)` are 10-15 px off.

## Hiding collected items and beaten bosses (`Mod/Progress.cs`)

SSMap's item and boss markers are linked to level entities (`MapMgr.map.layer[19].seg`)
whenever the entity array changes (at load, and again when a save loads, which re-reads the
level). The game's loot catalog, loaded at startup, turns item ids into English titles.

- Candidates: chests and loot bags (`MonsterDef.type` 2 with `flag X` and `chest ITEM`),
  enemies with `flag X` and `drop ITEM`, and bosses (`boss X`, their `drop` lines).
- Bosses: nearest boss within 1200 units, else the only boss whose title matches.
- Items: the closest candidate holding an item whose title matches the marker's name (the
  part before " - "), if within 800 units or if that item exists only once; else the nearest
  candidate within 250 units holding the right kind of item (group to loot category:
  Weapons 0, Shields 1, Armors 2, Rings 3, Charms 3/4, Key Items 6, Spells/Prayers/
  Incantations 5, Pearls 4/6/7); for "Other", the nearest within 150.
- Brands are linked by name to their loot (category 3, type 2; `LootDef.flags` is the rune
  index) and are done when `Player.runes[index]` is set, the game's own ownership test.
- Items NPCs hand over in dialog (`DialogMgr.dialogList`, each node's `giveScript`, with the
  flag its `postSetFlagStr` sets) count as sources at every spot that NPC stands, by name only.
- Unlinked markers never hide. Sanctuaries never hide; NPCs follow the next section.
- Title screen, 2026-10-08: 235 of 247 linked (the five brands added). A save with 417 flags
  (2026-10-08): no shown item marker had an opened chest or drop next to it.
- `Mods\marker-links.tsv` lists every link (written at each link).
- Title screen, 2026-10-06: 230 of 247 item and boss markers linked (164 by name, 41 by kind).

"Done" (read from the code: CharCols, CharScript, CharUpdateDeath, Pickup, TriggerMgr):
opening a chest or killing a flagged enemy sets `X` and drops a pickup carrying `X`; picking
it up adds `X_pkp`; a loot bag sets both at once; a boss sets only `X` and its items go
straight to the inventory. Pickups are not saved. So a marker is done when `X_pkp` is set, or
`X` is set and no pickup carrying `X` exists (`MapMgr.pickupMgr.pickup`).

## NPCs and sanctuaries (`Mod/Places.cs`)

- NPCs stand at one of several spots, each gated in its script (read from
  `MapMgr.CreateMonster` / `ReserveChar.InitFromSeg`): every `enabledflag X` must be set; any
  `disabledflag X` that is set removes the NPC, and `disabledflag A&B` only when all parts are
  set; an NPC whose own `flag`/`boss`/`talkphase` tag is set is gone. NPCs cannot be killed.
  Each SSMap NPC marker ("Mad Jester - #1") is tied to the nearest NPC spot (or by name, for
  the Ship Captain) and shown only while that gate holds, named without the "#n".
- Sanctuaries: each marker is tied to the nearest of `MapMgr.sanctuaryMgr.sanctuaries` and
  named as the fast-travel list names it (`AreaCatalog.areaStr`, sanctuary index mapped to
  area as in `FastTravel.SanctuaryDestination`) plus the creed holding it in this save
  (`Sanctuary.creed`, names `PlayerStats.strs[34 + creed]`), or "empty sanctuary". Claimed
  ones use SSMap's creed icon. The sanctuary list is filled after the level is read, so the
  title-screen link finds none; a save load links all 31 (verified 2026-10-08).
- *(verified in a save, 2026-10-08)* 36 of 36 NPC markers tied, 16 shown and 20 hidden, one
  spot per NPC; 31 of 31 sanctuary markers tied to 31 different sanctuaries.

## Diagnostics

Opening the map page writes `Mods\marker-status.tsv` (every marker, its link, flag states,
whether hidden, and the name shown) and `Mods\save-flags.txt` (the save's flags).

## Where next (`Mod/Guide.cs`)

The route follows the Fextralife wiki's Game Progress Route
(https://saltandsanctuary.wiki.fextralife.com/Game_Progress_Route); only its facts (which
boss or brand, in which area, in what order) are used. The main route has 25 steps: 19
bosses, 5 brands, and choosing a creed. The prologue is left out: the Unspeakable Deep on
the boat is a scripted fight that never sets its flag, and the boat is not on SSMap's map
(SSMap's three boat markers sit beyond its left edge and are dropped; enemy dots in the
level's top-left corner, where the boat is, are skipped). Optional steps open once the main step before them
is done: Ronin Cran, Carsejaw the Cruel and The Forgotten King; the Masterless Knight's,
Despondent Thief's and Black Sands Sorcerer's stories; the Pitchfork, Stone Sellsword,
Kureimoa, Haymaker, Vile Vines Ring and Bag of Earth; the House of Splendor and Order of
the Betrayer sanctuaries. Each has a test the save answers:

| Kind | Done when | Points at |
|---|---|---|
| Boss | its `boss X` flag is set | the boss's spot in the level |
| Brand | `Player.runes[i]` | the NPC's `brandfire` spot |
| Creed | `chose_creed` | the old man's first spot |
| NPC story | its final reward's flag (`masterless_6_talk_2`, `4_desp_talk_3`, `3_choppy_talk_4`) | the NPC's current spot (same rules as the NPC icons) |
| Item | its tracked SSMap icon is done | the icon |
| Sanctuary | `Player.sanctuaryVisitCount[i] > 0` | the sanctuary |

Wiki steps with no such test (passing through an area, meeting a merchant, the endings)
are left out; the next objective leads through those places. The next main objective gets
a pulsing gold ring, and an arrow at the edge of the minimap or map page when it is out of
view; open optional ones get pale blue rings where they are in view. A panel under the
menu's prompt bar lists the counts, the next objective and the first open optional ones.
`guide=all|main|off` in `SaltMap.ini`; Y / G on the map page cycles. Title screen,
2026-10-08: all 28 boss, brand and flag targets found in the level. Opening the map page
also writes `Mods\guide-status.txt` (every step: done, open or later, and its target).

## Enemy dots (`Mod/Enemies.cs`)

Live characters in `CharMgr.character` that exist, are not players, are `MonsterDef.type`
1, are not peaceful NPCs and are not dying; plus unspawned ones in
`MapMgr.reserveEntities.reserveChar`. Only those within 4000 world units of the player are
drawn (all of them clustered into an unpleasant heat map when zoomed out), as red dots with
a dark edge; bosses are larger and ringed in gold. Killed enemies leave both lists until the
world respawns them (rest, death, fast travel). The prologue boat's corner of the level
(x < 9000, y < 6000) is skipped. On the map page, hovering a dot shows the monster's name
(`MonsterDef.title`), its health while spawned, and "(alert)" once it has noticed you.

A more dynamic version (pulsing when alert, dimming with health, facing notches, sized by
toughness, a trail behind the player) was tried in 0.4.0-0.4.3 and dropped as too busy.

## Player icons

A magenta diamond whose bottom tip is at the feet (no other icon uses magenta). The co-op
partner (when `GameStateManager.coopPlayerIdx` is set) is a blue diamond (not tested).

## Far zoom

The three most zoomed-out levels of both views show only the icons under the guide's rings
(the boss, brand, item, sanctuary, or the NPC at their current spot for each current
objective) and no enemy dots; the map page keeps its area names.

## Map page extras

SSMap's 22 region names are drawn at their places; a panel shows items, bosses and claimed
sanctuaries done out of those the mod can track, and the next objective.

## Minimap (`Mod/Minimap.cs`)

- Inside the top-right corner trim drawn by `PlayerDraw.DrawBackgroundTrim`: the trim's corner
  is 60 HUD units in from the top and right; the map is 200 units square, 9 units inside it,
  and the trim is drawn again over the map's corner. HUD scale is `PlayerDraw.scale` (2.0 at
  3000x1920). *(verified)* frame (2462, 137) 400x400.
- Zoom levels (screen px per world unit at HUD scale 1): 0.012 ... 0.14, default 0.05.
- Hidden outside play and while the inventory or a conversation is open.

## Map page in the Escape menu (`Mod/MenuMap.cs`)

The bag, skull and gear are slots 26-28 of the equipment grid (`PlayerInvEquip`); the map is
slot 29 at grid point (3, -1), with its own icon (`Mod/Resources/mapicon.png`, drawn for this
mod). Hooks: `PlayerInv.Update` prefix (keys), `PlayerInv.Draw` prefix/postfix (shows slot 28
to the prompt logic), `DrawEquipCategory` prefix (map open) / postfix (slot), `GetEquipPoint`
and `FindEquipNearestToPoint` postfixes (navigation), `Player.UpdateGamepad` postfix (Back),
and a `Text.DrawText` prefix (the 8-argument overload the menu prompts use) that skips the
game's own prompts (`InvPicker.strs` 79-82, 155, 160) while the map is open, so the bar
under the menu shows the map's controls instead of a second "B Close".
The map draws with the mod's own batch between `SpriteTools.End` and `BeginAlpha`, as
`InterfaceRender` does around its glow passes.

## Controls

| | Keyboard / mouse | Controller |
|---|---|---|
| Minimap show/hide | M | D-pad Up steps near, middle, far, hidden |
| Minimap zoom | = and - (numpad + -) | (D-pad Up) |
| Open map page | Esc, then the map icon | Back/Select (from play or the Esc menu) |
| Pan | arrows / WASD / drag | left stick |
| Zoom | Q E, wheel, = - | right stick (glides), LB RB, LT RT (steps) |
| What the map page shows | F (Shift+F back) | D-pad left/right |
| Guide: all, main only, off | G | Y |
| Centre on player | Space | A |
| Back to menu | Esc | B |
| Close menu | | Back |

Chosen from the game's bindings (`InputProfile`): Back/Select is bound to nothing; D-pad Up
is unused in the field (Left/Right switch items, Down is the torch). On the map page the
bumpers (inventory paging) and triggers (stats panel pages) are taken over, and the stats
panel is kept from flipping. The controller is `Player.gamepadIdx`; prompts switch with
`InputMgr.IsMouseKeyboardActive` and use the game's glyphs (`Text.CHAR_*`).
Minimap visibility and zoom are saved in `SaltMap.ini`.
