# Changelog

Each release's section below is also its GitHub release notes.

## 0.4.4

Everything since 0.2.1 (0.3.0 and the 0.4.x steps were not released separately).

### Where next
- A guide to the next objective, after the Fextralife wiki's Game Progress Route: the next
  main-route boss or brand gets a pulsing gold ring on the map, and an arrow at the edge of
  the minimap or map page when it is out of view.
- Optional objectives once they open up: Ronin Cran, Carsejaw the Cruel, The Forgotten King;
  the Masterless Knight's, Despondent Thief's and Black Sands Sorcerer's stories (pointing at
  where the NPC stands now); the Pitchfork, Stone Sellsword, Kureimoa, Haymaker, Vile Vines
  Ring and Bag of Earth; the House of Splendor and Order of the Betrayer sanctuaries. They
  get pale blue rings.
- Progress is read from your save, so the guide starts where you are. The prologue (the boat
  and the Unspeakable Deep) is left out.
- G (keyboard) or Y (controller) on the map page cycles: main and optional, main only, off.

### Map page
- A panel under the menu with completion counts (items, bosses, claimed sanctuaries), the
  next objective and the open optional ones.
- Area names on the map.
- D-pad left/right (or F, Shift+F) picks what to show: all, items, bosses, NPCs,
  sanctuaries, enemies, or just the map. The game's own prompts are replaced by the map's
  controls, so there is only one "B" prompt.
- Hover an enemy for its name, health and whether it has noticed you; hover a ring for its
  objective.

### Icons
- NPCs show only where they stand now in your save (they move as the story goes on).
- Sanctuaries are named after their area and the creed that holds them in your save,
  instead of "Empty Sanctuary", with a creed icon once claimed.
- Brands disappear once you own them; items NPCs hand over are tracked too.
- Enemies near you are red dots, bosses ringed; zoomed far out (the three widest levels)
  only the objectives' icons stay.
- Your marker is a magenta diamond; a co-op partner shows in blue.
- SSMap's boat markers, which sat off the edge of the map, are gone.

### Diagnostics
- Opening the map page writes `Mods\marker-status.tsv`, `Mods\save-flags.txt` and
  `Mods\guide-status.txt`, for tracing an icon or objective that looks wrong.

## 0.2.1

- While the map page is open, the bar under the Escape menu shows the map's controls
  instead of the game's own prompts, so there is only one "B" prompt.
- Installer: deleting the temporary map download can no longer fail the install (it
  ended with a yellow warning before); download and extraction failures are reported
  separately; extraction shows progress; folders with `[ ]` in their names work.

## 0.2.0

First release.

- Minimap in the top-right corner, inside the HUD's corner ornament, from the SSMap web
  map's colour tiles.
- A Map page in the Escape menu, with pan and zoom.
- Item, NPC, boss and sanctuary icons; item and boss icons disappear once collected or
  beaten.
- Enemy dots.
- Keyboard, mouse and controller (Back/Select opens the map page).
- One-step installer that downloads the map data from the SSMap project.
