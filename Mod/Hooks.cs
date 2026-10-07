// Every hook into the game lives in this file. Each one hands straight off to
// mod code inside a try/catch, so a bug in the mod cannot take the game down.
using System;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using ProjectTower;
using ProjectTower.character;
using ProjectTower.hud;
using ProjectTower.player;

namespace SaltMap
{
    // Game1.Update runs once per rendered frame (the game's own logic runs at 60 Hz).
    [HarmonyPatch(typeof(Game1), "Update", new[] { typeof(GameTime) })]
    internal static class UpdateHook
    {
        static void Postfix()
        {
            try { PlayerTracker.Tick(); }
            catch (Exception e) { Log.ErrorOnce("Update", e); }

            try { Progress.Update(PlayerTracker.MainPlayer); }
            catch (Exception e) { Log.ErrorOnce("Progress", e); }

            try { Enemies.Update(); }
            catch (Exception e) { Log.ErrorOnce("Enemies", e); }

            try { Minimap.HandleInput(); }
            catch (Exception e) { Log.ErrorOnce("Minimap input", e); }
        }
    }

    // Runs just before the game decides whether to open the Escape menu (Player.Update),
    // so the controller's Back button can open it the way Escape does.
    [HarmonyPatch(typeof(Player), "UpdateGamepad", new Type[0])]
    internal static class GamepadHook
    {
        static void Postfix(Player __instance)
        {
            try { MenuMap.AfterUpdateGamepad(__instance); }
            catch (Exception e) { Log.ErrorOnce("MenuMap gamepad", e); }
        }
    }

    // GameDraw.Draw calls HUD.DrawModalDialog once per frame in every state, after
    // the game's picture and HUD are on the back buffer and before the modal
    // dialog, mouse cursor and save icon (docs/rendering.md).
    [HarmonyPatch(typeof(HUD), "DrawModalDialog")]
    internal static class DrawHook
    {
        static void Prefix()
        {
            try { Overlay.Draw(Game1.game.GraphicsDevice); }
            catch (Exception e) { Log.ErrorOnce("Draw", e); }
        }
    }

    // ---- the Map entry in the Escape menu (MenuMap.cs) ----------------------------------

    // The menu's input: opens and steers the map, and keeps its keys from the game.
    [HarmonyPatch(typeof(PlayerInv), "Update", new Type[0])]
    internal static class InvUpdateHook
    {
        static void Prefix(PlayerInv __instance, Player ___p)
        {
            try { MenuMap.BeforeUpdate(__instance, ___p); }
            catch (Exception e) { Log.ErrorOnce("MenuMap update", e); }
        }
    }

    [HarmonyPatch(typeof(PlayerInv), "Draw", new[] { typeof(float) })]
    internal static class InvDrawHook
    {
        static void Prefix(PlayerInv __instance)
        {
            try { MenuMap.BeforeInvDraw(__instance); }
            catch (Exception e) { Log.ErrorOnce("MenuMap before draw", e); }
        }

        static void Postfix(PlayerInv __instance)
        {
            try { MenuMap.AfterInvDraw(__instance); }
            catch (Exception e) { Log.ErrorOnce("MenuMap after draw", e); }
        }
    }

    // Skips the game's own menu prompts while the map page is open (its controls are
    // shown in their place). The overload the menu uses for its prompts; the check is a
    // single flag test unless the map page is open.
    [HarmonyPatch(typeof(Text), "DrawText", new[] { typeof(StringBuilder), typeof(Vector2), typeof(Color), typeof(float),
        typeof(int), typeof(float), typeof(Player), typeof(int) })]
    internal static class PromptHook
    {
        static bool Prefix(StringBuilder s)
        {
            try { return !MenuMap.HidesPrompt(s); }
            catch (Exception e) { Log.ErrorOnce("MenuMap prompts", e); return true; }
        }
    }

    // Draws the map slot, or the open map in place of the equipment grid.
    [HarmonyPatch(typeof(PlayerInvEquip), "DrawEquipCategory", new[] { typeof(Character), typeof(Rectangle), typeof(float), typeof(float) })]
    internal static class EquipDrawHook
    {
        static bool Prefix(Player ___p, Rectangle dRect, float catAlpha, float scale)
        {
            if (!MenuMap.IsOpen) return true;
            try
            {
                MenuMap.DrawOpen(___p, dRect, catAlpha, scale);
                return false;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("MenuMap open draw", e);
                return true;
            }
        }

        static void Postfix(Player ___p, Rectangle dRect, float catAlpha, float scale)
        {
            if (MenuMap.IsOpen) return;
            try { MenuMap.DrawSlot(___p, dRect, catAlpha, scale); }
            catch (Exception e) { Log.ErrorOnce("MenuMap slot draw", e); }
        }
    }

    [HarmonyPatch(typeof(PlayerInvEquip), "GetEquipPoint", new[] { typeof(int) })]
    internal static class EquipPointHook
    {
        static void Postfix(int e, ref Point __result)
        {
            try { MenuMap.AfterGetEquipPoint(e, ref __result); }
            catch (Exception ex) { Log.ErrorOnce("MenuMap equip point", ex); }
        }
    }

    [HarmonyPatch(typeof(PlayerInvEquip), "FindEquipNearestToPoint", new[] { typeof(Vector2), typeof(int) })]
    internal static class EquipNearestHook
    {
        static void Postfix(Vector2 p, int pSelItem, ref int __result)
        {
            try { MenuMap.AfterFindNearest(p, pSelItem, ref __result); }
            catch (Exception e) { Log.ErrorOnce("MenuMap nearest", e); }
        }
    }
}
