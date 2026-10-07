# How the game draws a frame

How Salt and Sanctuary draws one frame, and where a mod can draw on top of it.

Sources, and how far to trust them:

- **Read from the code**: the decompiled game in `salt-src/salt/`. That export
  was made from a hand-patched `salt.exe` (its `Game1.Update` still holds the
  old UDP block). The drawing methods below were compared with the installed,
  unpatched build (`salt.exe.orig`, dated 2026-10-01) with a Mono.Cecil dump of
  their IL. Every `SetRenderTarget`, `Clear`, `SpriteTools.Begin*`/`End` and
  `BloomComponent.Draw` call is in the same order in both.
- **Verified in the running game**: only the lines marked *(verified)*. They come
  from `Mods\SaltMap.log` and from a person looking at the screen.

MonoGame is 3.7.1, the WindowsDX build (SharpDX Direct3D 11 DLLs ship with the game).

## From MonoGame into the game

MonoGame's `Game.Tick` calls `BeginDraw`, then `Game1.Draw`, then `EndDraw`,
which presents the back buffer.

`Game1.Draw` (`ProjectTower/Game1.cs`) is only:

```csharp
GameDraw.Draw(gameTime, base.GraphicsDevice);
base.Draw(gameTime);            // draws GameComponents; the game registers none
```

Nothing in the game calls `Components.Add`, so once `GameDraw.Draw` returns the
frame is finished. A Harmony postfix on `Game1.Draw` runs after the last game
draw call and before `Present`.

## Render targets

Most are created in `Game1.CreateTargs` (`Game1.cs`), which runs at load time and on every
resolution change. "Screen" means `ScrollManager.screenSize`, which equals the
back buffer size unless split-screen co-op is on (and it never is, see below).

| Target | Size | Used for |
|---|---|---|
| `GameDraw.backTarg` | screen | background map layers, background particles |
| `GameDraw.auxTarg` | screen | `backTarg` passed through refraction, then characters and foreground particles. Also the logo (menu) and dying bosses |
| `GameDraw.mainTarg` | screen | `auxTarg` + water + foreground map layers |
| `GameDraw.sceneTarg` | screen | `mainTarg` through the layer tint effect (`GameDraw.mainEffect`), plus glow and parchment |
| `GameDraw.lightTarg` | screen | light map, built by `GlowMgr.Prepare` |
| `GameDraw.splitScreenTarg[2]` | half or full width | split-screen halves (dead code) |
| `BossToast.toastTarg` | screen | a bloomed copy of the frame for `BossToast` (a boss banner; purpose not checked) |
| `BloomComponent.renderTarget1/2` | half screen | bloom extract and blur |
| others | | `Water.waterTarg`, `RefractDraw.refractTarg`, `MapBlood` layers, `SaltStatue.rTarg`, `MenuSkillTree.rTarg`, `CreateCharacter.charTarg`, `CharSequence` |

All are created without a depth buffer. The game has exactly one `SpriteBatch`,
`SpriteTools.sprite`, always begun with `SpriteSortMode.Deferred`;
`SpriteTools.BeginAlpha` is `BlendState.NonPremultiplied`, `BeginPMAlpha` is
`AlphaBlend`, `BeginOpaque` is `Opaque`. The exceptions are the cape, cloth and
robe meshes (`CharCape/Cloth/RobeVertexBufferDraw`), which call
`DrawUserIndexedPrimitives` with their own effect.

## GameDraw.Draw

`ProjectTower/game/GameDraw.cs`, `GameDraw.Draw(GameTime, GraphicsDevice)`:

1. Builds `GameDraw.MatrixTransform` from the current viewport; `DebugMgr.Draw()`.
2. Draws the current state, chosen by `GameStateManager.gameState`:

   | State | Meaning | Call |
   |---|---|---|
   | 3 | splash and loading screens (set in `Game1.Initialize`) | `Loader.Draw()` |
   | 2 | title screen (`Loader.Update` sets it after the third splash screen) | `DrawMenu(p, logo: true)` |
   | 0 | menu without the logo (set by `MenuMgr`) | `DrawMenu(p, logo: false)` |
   | 1 | in game (set by `NewGameManager`) | `DrawGame(p, null, false)` |
   | 4 | credits | `Credits.Draw(dev)` |

3. Always, on whatever is bound (the back buffer, see below):
   `HUD.DrawModalDialog()`; then, once `Loader.loaded`, `MouseMgr.Draw()` (the
   mouse cursor), `HUD.DrawSaving` and `HUD.DrawStorage` (the save and storage
   icons near the top right, at `(screenSize.X - 100, 100)`). Last comes
   `MouseMgr.EndDraw()`, which only resets the click flags.

`Loader.Draw` and `Credits.Draw` never set a render target. They clear and draw
onto whatever the previous frame left bound, which is the back buffer.

### DrawGame (state 1)

`GameDraw.DrawGame(Player p, RenderTarget2D goalTarg, bool coopMode, GraphicsDevice)`,
single screen, so `goalTarg` is `null`:

1. Offscreen prep, each into its own target: `MapBlood.Prepare`,
   `SaltStatueMgr.ProcessSaltStatues`, `MenuSkillTree.Prepare` (when the skill
   tree is open), `CharSequenceMgr.Prepare`.
2. **backTarg**: `Clear(Black)`. Then, each in its own Begin/End: `MapMgr.DrawBack`
   (or `DrawIndoorBack`), `MapBlood.Draw(.., 1)`, `MapMgr.DrawBackAdditives`, sanctuary
   back and `dotsMgr.DrawBack`, `MapBlood.Draw(.., 0)`, then `DrawForeEntities`
   (map entities, sanctuary, particles in alpha, subtractive and additive passes).
3. `RefractDraw.Draw(auxTarg, backTarg)`: refraction particles go into
   `refractTarg`, then **auxTarg** is bound and `backTarg` is drawn into it through
   `refractEffect`. Still on auxTarg: `SaltStatueMgr.Draw`, then one BeginAlpha
   with `SmashMgr`, bottle messages, `CharMgr.DrawSanctuaryChars`, sanctuary fore and
   `CharMgr.Draw` (all characters), then particles in alpha, subtractive and
   additive passes, then `MapMgr.DrawPortals`.
4. `Water.Prepare(auxTarg)` renders into `Water.waterTarg` (when this map has water).
5. **mainTarg**: `Clear`, BeginOpaque draws `auxTarg`, `Water.Draw()`, BeginPMAlpha
   `MapMgr.DrawFore` (or `DrawIndoorFore`), BeginAlpha `dotsMgr.DrawFore` and
   collision debug.
6. `MapMgr.glowMgr.Prepare` renders the light map into **lightTarg** (when glow alpha > 0).
7. **sceneTarg**: `Clear`, BeginAlpha with `mainEffect` draws `mainTarg` (rotated in
   the deep-ocean and below-Y-49900 cases), then `glowMgr.Draw(lightTarg)` and
   `LayerTintCatalog.Finalize` (parchment overlay).
8. When a boss banner needs its frame, `BloomComponent.Draw(BossToast.toastTarg, ..)`.
9. **Bloom to the back buffer**: `BloomComponent.Draw(goalTarg = null, sceneTarg, lightTarg)`
   (`ProjectTower/director/bloom/BloomComponent.cs`): extract into `renderTarget1`, blur
   into `renderTarget2` and back, then `dev.SetRenderTarget(goalTarg)`, which binds the
   **back buffer**, and one BeginOpaque with `bloomCombineEffect` fills the viewport.
   While a boss is dying, `CharEffects.DrawBlastTarg` first goes through `auxTarg` and
   `backTarg`, and bloom then reads `backTarg`; it still ends on the back buffer.
10. `DrawFinalOverlay(p)`, on the back buffer: one BeginAlpha with `HUD.Draw(p)` (and the
    co-op partner's HUD), debug text, End; then `BossToast.Draw()`.

### DrawMenu (states 0 and 2)

Same shape, fewer layers: `LogoRender.Prepare` into `auxTarg` (title screen only),
**backTarg** (map back, menu, particles), **mainTarg** (backTarg + water + map fore),
**sceneTarg** (mainTarg through `mainEffect`, `LayerTintCatalog.Finalize`, the logo
from `auxTarg`), then `BloomComponent.Draw(null, sceneTarg, lightTarg)` binds the
**back buffer**, and a last BeginAlpha draws `menuMgr.Draw`, the version text and
the quit fade on top.

### Split-screen co-op is dead code

`GameStateManager.splitScreenCoopMode` is read in several places but assigned
nowhere, so it is always false. Ordinary co-op takes the `else` branch: one
`DrawGame` to the back buffer, then BeginAlpha `HUD.DrawPostHUD()`. (The
split-screen branch also looks broken: after drawing the second half it never
re-binds the back buffer before compositing both halves.)

## What is bound when Game1.Draw returns

- **Read from the code**: the back buffer (no render target), in every state.
  States 0, 1 and 2 end with `BloomComponent.Draw(null, ..)`, and nothing after it
  switches targets: `DrawFinalOverlay`, `HUD.*`, `BossToast.Draw` and
  `MouseMgr.Draw` call no `SetRenderTarget`. States 3 and 4 never leave it. The
  viewport is the full back buffer, because MonoGame resets it when the target
  changes and the game never sets one by hand. The shared `SpriteTools.sprite`
  is not inside a Begin; every Begin in the frame is closed.
- *(verified)* At the splash screens (state 3), the title screen (state 2) and in a
  save (state 1), the overlay's first draw in each state logged: no render target
  bound (back buffer), viewport 3000x1920 at (0, 0), equal to the back buffer, and
  the draw completed without an exception. A person saw the square on screen in
  all three, above the HUD, with the game's picture intact.

## Where an overlay has to draw

To land on screen above the HUD it must draw onto the **back buffer after
`DrawFinalOverlay`**. Drawing into any of the targets above puts it under bloom,
tint and the HUD, or gets it cleared.

Two places qualify:

- A **postfix on `Game1.Draw`** runs after everything, including modal dialogs, the
  save icon and the mouse cursor, so it covers all of them. The magenta test square
  was drawn there *(verified: back buffer bound in states 3, 2 and 1, and seen on
  screen)*.
- A **prefix on `HUD.DrawModalDialog`** runs just after the state's drawing (HUD
  included) and before the modal dialog, cursor and save icon. This is the mod's
  current hook (`Mod/Hooks.cs`), because the minimap belongs under those.
  *(verified: it runs in states 3, 2 and 1, so the JIT has not inlined this small
  method into `GameDraw.Draw`, and the back buffer is bound with the full viewport.)*

`Mod/Overlay.cs` uses its own `SpriteBatch` rather than `SpriteTools.sprite` and
puts back the blend, depth, rasterizer, sampler-0 states and scissor rectangle
afterwards. That is defensive: the game's own Begin calls set these anyway.
