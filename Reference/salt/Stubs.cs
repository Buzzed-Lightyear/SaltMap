// Compile-time declarations of the game members SaltMap uses. Names, types and
// signatures only; nothing here runs. Keep every field type exactly as in the game:
// run scripts/Check-Stubs.ps1 (needs the game installed) after any change.
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectTower
{
    public class Game1 : Game
    {
        public static Game1 game;
        public static float frameTime;
        public static Texture2D nullTex;
    }

    public class SpriteTools
    {
        public static SpriteBatch sprite;
        public static void BeginAlpha() => throw null;
        public static void End() => throw null;
    }
}

namespace ProjectTower.gamestate
{
    public class GameStateManager
    {
        public static int gameState;
        public static int mainPlayerIdx;
    }
}

namespace ProjectTower.config
{
    public class ConfigMgr
    {
        public static int hudVis;
    }
}

namespace ProjectTower.game
{
    public class GameDraw
    {
        public static RenderTarget2D lightTarg;
        public static RenderTarget2D mainTarg;
        public static RenderTarget2D backTarg;
        public static RenderTarget2D auxTarg;
        public static RenderTarget2D sceneTarg;
    }
}

namespace ProjectTower.character
{
    public class CharMgr
    {
        public static Character[] character;
    }

    public class Character
    {
        public bool exists;
        public Vector2 loc;
        public float dyingFrame;
        public int monsterIdx;
        public float hp;
        public int npcIdx;
        public bool npcFightMode;
        public int playerIdx;
        public bool boss;
    }
}

namespace ProjectTower.player
{
    using ProjectTower.player.dialog;

    public class PlayerMgr
    {
        public static Player[] player;
    }

    public class Player
    {
        public int ID;
        public PlayerDraw draw;
        public int charIdx;
        public PlayerInv playerInv;
        public PlayerDialog dialog;
        public bool keyQueueAccept;
        public bool keyLeft;
        public bool keyRight;
        public bool keyUp;
        public bool keyDown;
        public bool keyAccept;
        public bool keyBCancel;
        public bool keyEscape;
        public bool keyXInfo;
        public bool keyYToggle;
        public bool keyR3Toggle;
        public bool keyL3Toggle;
        public bool keyCatRight;
        public bool keyCatLeft;
        public bool keyStatsRight;
        public bool keyStatsLeft;
        public int gamepadIdx;
        public List<string> flags;
        public void UpdateGamepad() => throw null;
    }

    public class PlayerDraw
    {
    }

    public class PlayerDialog
    {
        public bool active;
        public DialogMenuMgr menuMgr;
    }

    public class PlayerInv
    {
        public PlayerInvEquip invEquip;
        public int selCategory;
        public int selItem;
        public float[] itemAlpha;
        public float alpha;
        public bool active;
        public InvPicker invPicker;
        public int pickMode;
    }

    public class InvPicker
    {
        public float alpha;
        public bool active;
        public static StringBuilder[] strs;
    }

    public class PlayerInvEquip
    {
        public static int FindEquipNearestToPoint(Vector2 p, int pSelItem) => throw null;
        public static Point GetEquipPoint(int e) => throw null;
    }
}

namespace ProjectTower.player.dialog
{
    public class DialogMenuMgr
    {
        public bool active;
    }
}

namespace ProjectTower.hud
{
    public class HUD
    {
    }

    public static class MouseMgr
    {
        public static Vector2 mLoc;
        public static bool isClick;
        public static bool isActive;
    }

    public class InterfaceRender
    {
        public static Texture2D interfaceTex;
        public static void DrawIcon(int icon, Vector2 loc, float scale, float alpha) => throw null;
        public static void DrawRect(Rectangle rect, float alpha, int idx) => throw null;
    }

    public class Text
    {
        // Constants are compiled into the mod, so these values must match the game's.
        public const char CHAR_A = '\u0240';
        public const char CHAR_B = '\u0241';
        public const char CHAR_BACK = '\u0242';
        public const char CHAR_R_ANALOG_UP = '\u0247';
        public const char CHAR_L_ANALOG_UP = '\u02e5';

        public static void DrawText(StringBuilder s, Vector2 loc, Color color, float size, int align) => throw null;
        public static void DrawText(StringBuilder s, Vector2 loc, Color color, float size, int align, float maxLen) => throw null;
        public static void DrawText(StringBuilder s, Vector2 loc, Color color, float size, int align, float maxLen,
            ProjectTower.player.Player p, int replaceIcons) => throw null;
    }
}

namespace ProjectTower.map
{
    using MapEdit.map;
    using ProjectTower.map.pickups;

    public class MapMgr
    {
        public static PickupMgr pickupMgr;
        public static ReserveEntities reserveEntities;
        public static Map map;
    }

    public class ReserveEntities
    {
        public ReserveEntities.ReserveChar[] reserveChar;

        public struct ReserveChar
        {
            public Vector2 loc;
            public int monsterIdx;
            public int npcIdx;
            public bool exists;
            public bool boss;
        }
    }
}

namespace ProjectTower.map.pickups
{
    public class PickupMgr
    {
        public Pickup[] pickup;
    }

    public class Pickup
    {
        public bool exists;
        public string flag;
    }
}

namespace MapEdit.map
{
    public class Map
    {
        public Layer[] layer;
    }

    public class Layer
    {
        public Seg[] seg;
    }

    public class Seg
    {
        public Vector2 loc;
        public string texture;
        public string strFlag;
    }
}

namespace MonsterEdit.monsters
{
    public class MonsterCatalog
    {
        public static MonsterDef[] catalog;
        public static int GetIdxFromString(string monster) => throw null;
    }

    public class MonsterDef
    {
        public string[] title;
        public int type;
        public int flags;
    }
}

namespace LootEdit
{
    public class LootDef
    {
        public string name;
        public string[] title;
    }
}

namespace LootEdit.loot
{
    using LootEdit;

    public class LootCatalog
    {
        public static LootCategory[] category;
    }

    public class LootCategory
    {
        public LootDef[] loot;
    }
}
