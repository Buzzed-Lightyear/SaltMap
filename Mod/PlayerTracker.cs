using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Xna.Framework;
using ProjectTower.character;
using ProjectTower.gamestate;
using ProjectTower.player;

namespace SaltMap
{
    /// <summary>
    /// Reads the main player's world position every frame, keeps it for the rest
    /// of the mod, and sends it to serve.py (the tablet map) as "x,y,state" over
    /// local UDP. Replaces the block that was pasted into Game1.Update by hand.
    /// </summary>
    internal static class PlayerTracker
    {
        static readonly IPEndPoint MapServer = new IPEndPoint(IPAddress.Loopback, 47800);
        static readonly UdpClient udp = new UdpClient();

        static int lastState = int.MinValue;
        static int lastCharIdx = int.MinValue;

        /// <summary>False until a character exists for the main player.</summary>
        public static bool HasPosition { get; private set; }

        /// <summary>The main player, or null before the game has created one.</summary>
        public static Player MainPlayer { get; private set; }

        /// <summary>World position of the main player's character (its feet). X right, Y down.</summary>
        public static Vector2 Position { get; private set; }

        public static void Tick()
        {
            int state = (int)GameStateManager.gameState;
            if (state != lastState)
            {
                Log.Info("game state " + (lastState == int.MinValue ? "is" : lastState + " ->") + " " + state);
                lastState = state;
            }

            // Both arrays are created on the game's loading thread, some time after
            // startup, and are assigned before their slots are filled.
            Player[] players = PlayerMgr.player;
            Character[] characters = CharMgr.character;
            if (players == null || characters == null) { HasPosition = false; MainPlayer = null; return; }

            int main = GameStateManager.mainPlayerIdx;
            Player player = players[main < 0 ? 0 : main];
            MainPlayer = player;
            if (player == null) { HasPosition = false; return; }
            int charIdx = player.charIdx;
            if (charIdx < 0 || charIdx >= characters.Length || characters[charIdx] == null)
            { HasPosition = false; return; }

            Vector2 loc = characters[charIdx].loc;
            Position = loc;
            HasPosition = true;
            if (charIdx != lastCharIdx)
            {
                // Slots are reused (1 at the title screen, 0 in a save), so this is worth seeing.
                Log.Info("player character is slot " + charIdx + " at ("
                    + loc.X.ToString("F1", CultureInfo.InvariantCulture) + ", "
                    + loc.Y.ToString("F1", CultureInfo.InvariantCulture) + ")");
                lastCharIdx = charIdx;
            }

            byte[] msg = Encoding.ASCII.GetBytes(
                loc.X.ToString("F1", CultureInfo.InvariantCulture) + "," +
                loc.Y.ToString("F1", CultureInfo.InvariantCulture) + "," +
                state.ToString(CultureInfo.InvariantCulture));
            udp.Send(msg, msg.Length, MapServer);
        }
    }
}
