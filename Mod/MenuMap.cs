using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectTower;
using ProjectTower.character;
using ProjectTower.gamestate;
using ProjectTower.hud;
using ProjectTower.player;

namespace SaltMap
{
    /// <summary>
    /// A "Map" entry in the game's Escape menu, after the bag, skull and gear. Those
    /// three are slots 26-28 of the equipment grid (PlayerInvEquip); the map is slot 29
    /// at grid point (3, -1). Choosing it shows a large map where the equipment grid
    /// was. The game's own state is left alone: selCategory stays 0 and the mod keeps
    /// track of whether the map is open.
    /// </summary>
    internal static class MenuMap
    {
        public const int Slot = 29;
        const int GearSlot = 28;
        static readonly Point SlotPoint = new Point(3, -1);
        static readonly int[] GameIcons = { 7, 21, 11 };      // InterfaceRender icons for slots 26, 27, 28

        // Screen pixels per world unit at HUD scale 1, from far to near.
        static readonly float[] Zooms = { 0.004f, 0.006f, 0.009f, 0.013f, 0.019f, 0.027f, 0.038f, 0.054f, 0.076f, 0.11f };
        const int DefaultZoom = 4;
        const float PanScreensPerSecond = 0.8f;

        static readonly StringBuilder Title = new StringBuilder("Map");
        static readonly StringBuilder KeyboardHint = new StringBuilder("Move: arrows/WASD/drag    Zoom: Q E/wheel    Centre: Space    Back: Esc");
        // The game's own glyph characters (Text.CHAR_*); drawn with replaceIcons 1, so
        // A and B show whichever buttons accept and cancel are set to.
        static readonly StringBuilder PadHint = new StringBuilder()
            .Append(Text.CHAR_L_ANALOG_UP).Append(" Move    ")
            .Append(Text.CHAR_R_ANALOG_UP).Append(" Zoom    ")
            .Append(Text.CHAR_A).Append(" Centre    ").Append(Text.CHAR_B).Append(" Back    ").Append(Text.CHAR_BACK).Append(" Close");
        static readonly StringBuilder Label = new StringBuilder();

        static Texture2D icon;
        static bool iconTried;

        static Vector2 focus;
        // A level in Zooms; fractional while the right stick glides between levels.
        static float zoom = DefaultZoom;
        const float ZoomLevelsPerSecond = 3f;
        const float StickDeadZone = 0.2f;
        static KeyboardState lastKeys;
        static MouseState lastMouse;
        static bool dragging;
        static bool swapped;          // selItem shown to PlayerInv.Draw as the gear slot
        static bool mouseOnGear;
        static bool reportedSlot, reportedOpen;

        // Where the map was last drawn, for mouse input in the next update.
        static Rectangle mapRect;
        static float mapScale = 1f;

        public static bool IsOpen { get; private set; }

        // ---- the game's update (prefix on PlayerInv.Update) ----------------------------

        // The controller's Back button: opens the map page, or closes the menu from it.
        // Seen per rendered frame, acted on in the game's next update tick.
        static long backUntil;

        public static void RequestFromBackButton()
        {
            backUntil = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond / 2;
        }

        static bool BackPending { get { return DateTime.UtcNow.Ticks < backUntil; } }

        /// <summary>
        /// Postfix on Player.UpdateGamepad, which runs just before the game decides
        /// whether to open the Escape menu. Back opens it exactly as Escape does.
        /// </summary>
        public static void AfterUpdateGamepad(Player p)
        {
            if (p == null || !BackPending || p != PlayerTracker.MainPlayer || p.playerInv == null || p.playerInv.active) return;
            p.keyEscape = true;
        }

        public static void BeforeUpdate(PlayerInv inv, Player p)
        {
            if (inv == null || p == null) return;
            EnsureSlot(inv);
            bool ready = inv.active && inv.selCategory == 0 && inv.pickMode == 0
                && !inv.invPicker.active && inv.invPicker.alpha <= 0f
                && (p.dialog == null || p.dialog.menuMgr == null || !p.dialog.menuMgr.active);

            if (BackPending && p == PlayerTracker.MainPlayer)
            {
                if (IsOpen)
                {
                    // Back on the map page closes the whole menu: close the map and let
                    // the game see a cancel on its equipment page, which closes the menu.
                    backUntil = 0;
                    Close(false);
                    p.keyBCancel = true;
                    return;
                }
                if (ready)
                {
                    backUntil = 0;
                    inv.selItem = Slot;
                    Open();
                    ClearMenuKeys(p);
                    return;
                }
            }

            if (IsOpen && (!ready || inv.selItem != Slot)) Close(false);
            if (!ready || inv.selItem != Slot) return;

            if (!IsOpen)
            {
                if (p.keyAccept) Open();
                // The slot holds no item, so keep the game's equip, unequip and info paths away from it.
                p.keyAccept = false;
                p.keyYToggle = false;
                p.keyXInfo = false;
                return;
            }

            // The map has the keys now.
            if (p.keyBCancel) Close(true);
            else Steer(p);
            ClearMenuKeys(p);
        }

        /// <summary>
        /// Keeps the map's buttons from the game's menu: navigation, accept and cancel,
        /// the bumpers, X and Y, and the triggers and stick clicks, which would
        /// otherwise flip the stats panel beside the map (PlayerStats.Update).
        /// </summary>
        static void ClearMenuKeys(Player p)
        {
            p.keyUp = p.keyDown = p.keyLeft = p.keyRight = false;
            p.keyAccept = p.keyBCancel = p.keyCatLeft = p.keyCatRight = p.keyYToggle = p.keyXInfo = false;
            p.keyStatsLeft = p.keyStatsRight = p.keyL3Toggle = p.keyR3Toggle = false;
        }

        static void Open()
        {
            IsOpen = true;
            focus = PlayerTracker.HasPosition ? MapSpace.FromWorld(PlayerTracker.Position) : new Vector2(16384f, 14000f);
            dragging = false;
            lastKeys = Keyboard.GetState();
            lastMouse = Mouse.GetState();
            GameApi.PlayAccept();
            Log.Info("menu map opened at map pixel (" + focus.X.ToString("F0", CultureInfo.InvariantCulture) + ", "
                + focus.Y.ToString("F0", CultureInfo.InvariantCulture) + "), zoom " + zoom);
        }

        static void Close(bool sound)
        {
            IsOpen = false;
            dragging = false;
            if (sound) GameApi.PlayCancel();
            Log.Info("menu map closed");
        }

        static void Steer(Player p)
        {
            KeyboardState keys = Keyboard.GetState();
            KeyboardState before = lastKeys;
            lastKeys = keys;
            MouseState mouse = Mouse.GetState();
            MouseState mouseBefore = lastMouse;
            lastMouse = mouse;
            GamePadState pad = GameApi.Pad(p);
            float dt = Math.Max(0f, Math.Min(0.1f, Game1.frameTime));

            // Zoom: the right stick glides (up is closer); bumpers, triggers, keys and the
            // wheel step one level. The game pages with bumpers and flips the stats panel
            // with triggers, neither of which the map page needs.
            int step = 0;
            if (p.keyCatRight || p.keyStatsRight || Pressed(keys, before, Keys.E) || Pressed(keys, before, Keys.OemPlus) || Pressed(keys, before, Keys.Add)) step++;
            if (p.keyCatLeft || p.keyStatsLeft || Pressed(keys, before, Keys.Q) || Pressed(keys, before, Keys.OemMinus) || Pressed(keys, before, Keys.Subtract)) step--;
            int wheel = mouse.ScrollWheelValue - mouseBefore.ScrollWheelValue;
            if (wheel > 0) step++;
            if (wheel < 0) step--;
            if (step != 0)
            {
                float next = MathHelper.Clamp((float)Math.Round(zoom) + step, 0f, Zooms.Length - 1);
                if (next != zoom) { zoom = next; GameApi.PlaySelect(); }
            }
            float zoomStick = pad.ThumbSticks.Right.Y;
            if (Math.Abs(zoomStick) > StickDeadZone)
                zoom = MathHelper.Clamp(zoom + zoomStick * ZoomLevelsPerSecond * dt, 0f, Zooms.Length - 1);

            if (p.keyAccept && PlayerTracker.HasPosition)
            {
                focus = MapSpace.FromWorld(PlayerTracker.Position);
                GameApi.PlaySelect();
            }

            // Held keys and the stick pan smoothly; the speed is a share of the view per second.
            Vector2 dir = Vector2.Zero;
            if (keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A) || pad.DPad.Left == ButtonState.Pressed) dir.X -= 1;
            if (keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D) || pad.DPad.Right == ButtonState.Pressed) dir.X += 1;
            if (keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W) || pad.DPad.Up == ButtonState.Pressed) dir.Y -= 1;
            if (keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S) || pad.DPad.Down == ButtonState.Pressed) dir.Y += 1;
            // The left stick pans; stick Y is up-positive, screen Y is down-positive.
            Vector2 stick = pad.ThumbSticks.Left;
            if (stick.Length() > StickDeadZone) dir += new Vector2(stick.X, -stick.Y);
            if (dir != Vector2.Zero && mapScale > 0f)
            {
                if (dir.LengthSquared() > 1f) dir.Normalize();
                focus += dir * PanScreensPerSecond * Math.Max(mapRect.Width, mapRect.Height) / mapScale * dt;
            }

            // Dragging with the left button, starting inside the map.
            Point m = new Point(mouse.X, mouse.Y);
            bool down = mouse.LeftButton == ButtonState.Pressed;
            if (down && mouseBefore.LeftButton != ButtonState.Pressed && mapRect.Contains(m)) dragging = true;
            if (!down) dragging = false;
            if (dragging && mapScale > 0f)
                focus -= new Vector2(mouse.X - mouseBefore.X, mouse.Y - mouseBefore.Y) / mapScale;

            focus.X = MathHelper.Clamp(focus.X, 0f, 32768f);
            focus.Y = MathHelper.Clamp(focus.Y, 0f, 32768f);
        }

        static bool Pressed(KeyboardState now, KeyboardState before, Keys key)
        {
            return now.IsKeyDown(key) && !before.IsKeyDown(key);
        }

        /// <summary>Screen px per world unit at a fractional level, geometric between neighbours.</summary>
        static float ZoomAt(float level)
        {
            int lo = (int)Math.Floor(level);
            if (lo >= Zooms.Length - 1) return Zooms[Zooms.Length - 1];
            if (lo < 0) return Zooms[0];
            float t = level - lo;
            return Zooms[lo] * (float)Math.Pow(Zooms[lo + 1] / Zooms[lo], t);
        }

        /// <summary>The game sizes itemAlpha for 29 slots; the selection fade needs one more.</summary>
        static void EnsureSlot(PlayerInv inv)
        {
            if (inv.itemAlpha != null && inv.itemAlpha.Length <= Slot)
            {
                float[] grown = inv.itemAlpha;
                Array.Resize(ref grown, Slot + 1);
                inv.itemAlpha = grown;
            }
        }

        // ---- grid navigation (postfixes on PlayerInvEquip) ------------------------------

        public static void AfterGetEquipPoint(int e, ref Point result)
        {
            if (e == Slot) result = SlotPoint;
        }

        /// <summary>Adds slot 29 to the game's nearest-slot search, with the game's own rules.</summary>
        public static void AfterFindNearest(Vector2 p, int from, ref int result)
        {
            if (from == Slot) return;
            Point cur = PlayerInvEquip.GetEquipPoint(from);
            Point c = SlotPoint;
            bool inDirection = (p.X >= cur.X || c.X < cur.X) && (p.X <= cur.X || c.X > cur.X)
                && (p.Y >= cur.Y || c.Y < cur.Y) && (p.Y <= cur.Y || c.Y > cur.Y);
            if (!inDirection) return;
            float dist = Math.Abs(c.X - p.X) + Math.Abs(c.Y - p.Y);
            if (result == from) { result = Slot; return; }        // the game found nothing
            Point best = PlayerInvEquip.GetEquipPoint(result);
            if (dist < Math.Abs(best.X - p.X) + Math.Abs(best.Y - p.Y)) result = Slot;
        }

        // ---- drawing --------------------------------------------------------------------

        /// <summary>
        /// PlayerInv.Draw picks its bottom-left prompt by slot number and would show the
        /// wrong one for slot 29, so it sees the gear slot instead for the duration.
        /// </summary>
        public static void BeforeInvDraw(PlayerInv inv)
        {
            swapped = false;
            mouseOnGear = false;
            hidePrompts = IsOpen && inv != null && inv.selCategory == 0;
            if (inv != null && inv.selCategory == 0 && inv.selItem == Slot)
            {
                inv.selItem = GearSlot;
                swapped = true;
            }
        }

        public static void AfterInvDraw(PlayerInv inv)
        {
            // If the mouse moved onto the gear meanwhile, the game's choice stands.
            if (swapped && inv != null && inv.selItem == GearSlot && !mouseOnGear) inv.selItem = Slot;
            swapped = false;
            hidePrompts = false;
        }

        // The game's own prompts in the bar under the menu (InvPicker.strs: 79 and 160 left,
        // 80, 81 and 155 middle, 82 "Close" right). While the map is open they would repeat
        // or contradict the map's controls, which are drawn in that bar instead.
        static readonly int[] PromptStrings = { 79, 80, 81, 82, 155, 160 };
        static bool hidePrompts;

        /// <summary>Prefix test for Text.DrawText: true to skip one of the game's menu prompts.</summary>
        public static bool HidesPrompt(StringBuilder s)
        {
            if (!hidePrompts || s == null) return false;
            StringBuilder[] strs = InvPicker.strs;
            if (strs == null) return false;
            foreach (int i in PromptStrings)
                if (i < strs.Length && ReferenceEquals(strs[i], s)) return true;
            return false;
        }

        /// <summary>Draws the map slot next to the gear. Postfix on DrawEquipCategory, map closed.</summary>
        public static void DrawSlot(Player p, Rectangle dRect, float catAlpha, float scale)
        {
            PlayerInv inv = p.playerInv;
            EnsureSlot(inv);
            float alpha = inv.alpha;
            Vector2 at = SlotOrigin(dRect, SlotPoint.X, scale);
            Rectangle box = SlotBox(at, scale);
            InterfaceRender.DrawRect(box, 0.1f * catAlpha * alpha, 1);
            if (!inv.invPicker.active && inv.pickMode == 0)
            {
                if (MouseOver(p, box))
                {
                    inv.selItem = Slot;
                    if (MouseMgr.isClick) p.keyQueueAccept = true;
                }
                if (MouseOver(p, SlotBox(SlotOrigin(dRect, 2, scale), scale))) mouseOnGear = true;
            }
            DrawHighlight(inv.itemAlpha[Slot], at, catAlpha * alpha, scale);
            DrawIcon(at, scale, alpha);
            if (inv.itemAlpha[Slot] > 0f)
                Text.DrawText(Title, HeaderCentre(dRect, scale), new Color(1f, 1f, 1f, inv.itemAlpha[Slot] * alpha), 0.55f * scale, 1);

            if (!reportedSlot)
            {
                reportedSlot = true;
                Log.Info("menu map slot drawn: box (" + box.X + ", " + box.Y + ") " + box.Width + "x" + box.Height
                    + ", menu rect (" + dRect.X + ", " + dRect.Y + ") " + dRect.Width + "x" + dRect.Height
                    + ", HUD scale " + scale.ToString("F2", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Draws the menu with the map in place of the equipment grid. Prefix on
        /// DrawEquipCategory while the map is open (the original is skipped). Runs inside
        /// the game's SpriteBatch (begun with SpriteTools.BeginAlpha).
        /// </summary>
        public static void DrawOpen(Player p, Rectangle dRect, float catAlpha, float scale)
        {
            PlayerInv inv = p.playerInv;
            EnsureSlot(inv);
            float alpha = inv.alpha;

            // Header bar and arrows, as the game draws them.
            Rectangle header = HeaderRect(dRect, scale);
            InterfaceRender.DrawRect(header, alpha * 0.2f, 4);
            GameApi.DrawArrow(new Vector2(header.X + 16f * scale, header.Center.Y - 4f * scale), 0, alpha * 0.5f, 1.35f * scale);
            GameApi.DrawArrow(new Vector2(header.Right - 16f * scale, header.Center.Y - 4f * scale), 1, alpha * 0.5f, 1.35f * scale);

            // The four menu icons; the mouse can still pick the others, which closes the map.
            for (int slot = 26; slot <= Slot; slot++)
            {
                int x = slot == Slot ? SlotPoint.X : slot - 26;
                Vector2 at = SlotOrigin(dRect, x, scale);
                Rectangle box = SlotBox(at, scale);
                InterfaceRender.DrawRect(box, 0.1f * catAlpha * alpha, 1);
                if (slot != Slot && MouseOver(p, box))
                {
                    inv.selItem = slot;
                    if (slot == GearSlot) mouseOnGear = true;
                    if (MouseMgr.isClick) p.keyQueueAccept = true;
                }
                DrawHighlight(inv.itemAlpha[slot], at, catAlpha * alpha, scale);
                if (slot == Slot) DrawIcon(at, scale, alpha);
                else InterfaceRender.DrawIcon(GameIcons[slot - 26], at + new Vector2(54f, 0f) * scale, 0.8f * scale, alpha);
            }
            Text.DrawText(Title, HeaderCentre(dRect, scale), new Color(1f, 1f, 1f, alpha), 0.55f * scale, 1);

            // The map itself, with the mod's own batch so it can be clipped.
            Rectangle area = new Rectangle((int)(dRect.X + 18f * scale), (int)(dRect.Y + 132f * scale),
                (int)(dRect.Width - 36f * scale), (int)(dRect.Height - 142f * scale));
            float mapPerScreen = ZoomAt(zoom) * scale / MapSpace.PixelsPerUnit;
            mapRect = area;
            mapScale = mapPerScreen;
            Overlay.DrawOutsideGameBatch(device => MapView.Draw(device, Overlay.Batch, Overlay.Pixel, area, focus, mapPerScreen,
                22f * scale, 7f * scale, PlayerTracker.HasPosition ? (Vector2?)MapSpace.FromWorld(PlayerTracker.Position) : null,
                5f * scale));

            // Back in the game's batch: a frame, a crosshair, the hint and the nearest marker's name.
            InterfaceRender.DrawRect(area, 0.3f * alpha, 1);
            Vector2 centre = new Vector2(area.X + area.Width / 2f, area.Y + area.Height / 2f);
            Texture2D px = Game1.nullTex;
            if (px != null)
            {
                Color cross = new Color(1f, 1f, 1f, 0.5f * alpha);
                SpriteTools.sprite.Draw(px, new Rectangle((int)centre.X - (int)(8 * scale), (int)centre.Y, (int)(16 * scale), Math.Max(1, (int)scale)), cross);
                SpriteTools.sprite.Draw(px, new Rectangle((int)centre.X, (int)centre.Y - (int)(8 * scale), Math.Max(1, (int)scale), (int)(16 * scale)), cross);
            }
            // The map's controls go in the prompt bar under the menu, in place of the game's
            // prompts (hidden while the map is open), at the game's prompt size and place.
            // This overload squeezes text into maxLen and scales size by 0.8.
            bool keyboard = GameApi.UsingKeyboard(p);
            Text.DrawText(keyboard ? KeyboardHint : PadHint, new Vector2(dRect.X + 20f * scale, dRect.Bottom + 30f * scale),
                new Color(1f, 1f, 1f, alpha), 0.45f * scale, 0, dRect.Width - 40f * scale, p, keyboard ? 0 : 1);

            Vector2 pointer = centre;
            if (MouseMgr.isActive && area.Contains(new Point((int)MouseMgr.mLoc.X, (int)MouseMgr.mLoc.Y))) pointer = MouseMgr.mLoc;
            Marker near = MapView.Nearest(area, focus, mapPerScreen, pointer, 26f * scale);
            if (near != null && near.Name.Length > 0)
            {
                Vector2 at = MapView.ToScreen(area, focus, mapPerScreen, near.Map);
                Label.Clear().Append(near.Name);
                if (near.Group.Length > 0 && near.Group != near.Name) Label.Append("  (").Append(near.Group).Append(')');
                Vector2 textAt = new Vector2(MathHelper.Clamp(at.X, area.X + 150f * scale, area.Right - 150f * scale),
                    Math.Max(area.Y + 16f * scale, at.Y - 22f * scale));
                InterfaceRender.DrawRect(new Rectangle((int)(textAt.X - 150f * scale), (int)(textAt.Y - 13f * scale), (int)(300f * scale), (int)(26f * scale)), 0.8f * alpha, 4);
                Text.DrawText(Label, textAt, new Color(1f, 1f, 1f, alpha), 0.5f * scale, 1, 290f * scale);
            }

            if (!reportedOpen)
            {
                reportedOpen = true;
                Log.Info("menu map drew: area (" + area.X + ", " + area.Y + ") " + area.Width + "x" + area.Height
                    + ", zoom " + zoom + " (" + mapPerScreen.ToString("F3", CultureInfo.InvariantCulture) + " screen px per map px)");
            }
        }

        // Slot geometry, as PlayerInvEquip.DrawEquipCategory computes it for row -1.
        static Vector2 SlotOrigin(Rectangle dRect, int x, float scale)
        {
            Vector2 v = new Vector2(dRect.X, dRect.Y) + new Vector2(-10f + x * 60f, 160f - 66f) * scale;
            v.Y -= 45f * scale;
            return v;
        }

        static Rectangle SlotBox(Vector2 at, float scale)
        {
            return new Rectangle((int)(at.X + 24f * scale), (int)(at.Y - 30f * scale), (int)(60f * scale), (int)(60f * scale));
        }

        static Rectangle HeaderRect(Rectangle dRect, float scale)
        {
            return new Rectangle((int)(dRect.X + 18f * scale), (int)(dRect.Y + 16f * scale + 69f * scale),
                (int)(dRect.Width - 36f * scale), (int)(40f * scale));
        }

        static Vector2 HeaderCentre(Rectangle dRect, float scale)
        {
            Rectangle r = HeaderRect(dRect, scale);
            return new Vector2(r.Center.X, r.Center.Y);
        }

        static void DrawHighlight(float selected, Vector2 at, float alpha, float scale)
        {
            if (selected <= 0f) return;
            Rectangle r = new Rectangle((int)(at.X + 22f * scale), (int)(at.Y - 32f * scale), (int)(64f * scale), (int)(64f * scale));
            InterfaceRender.DrawRect(r, selected * alpha, 1);
            InterfaceRender.DrawRect(r, selected * alpha * 0.5f, 0);
        }

        static void DrawIcon(Vector2 at, float scale, float alpha)
        {
            if (!iconTried)
            {
                iconTried = true;
                try
                {
                    using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SaltMap.mapicon.png"))
                        if (s != null) icon = Images.Decode(s).Upload(Game1.game.GraphicsDevice);
                    if (icon == null) Log.Info("menu map: icon resource missing");
                }
                catch (Exception e) { Log.ErrorOnce("menu map icon", e); }
            }
            if (icon == null) return;
            SpriteTools.sprite.Draw(icon, at + new Vector2(54f, 0f) * scale, null, new Color(1f, 1f, 1f, alpha), 0f,
                new Vector2(icon.Width / 2f, icon.Height / 2f), 0.8f * scale, SpriteEffects.None, 1f);
        }

        static bool MouseOver(Player p, Rectangle r)
        {
            return p.ID == GameStateManager.mainPlayerIdx && MouseMgr.isActive
                && r.Contains(new Point((int)MouseMgr.mLoc.X, (int)MouseMgr.mLoc.Y));
        }
    }
}
