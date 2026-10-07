using System.Collections.Generic;
using System.Text;

namespace ValheimSlots
{
    /// <summary>
    /// Main-inventory slots the player has locked. Locked slots never receive auto-placed items
    /// and their contents are never stacked into chests, quick-stacked, sorted or used for top-ups.
    /// Stored per character in Player.m_customData so it follows the save file.
    /// </summary>
    internal static class LockedSlots
    {
        private const string Key = "ValheimSlots.locked";

        private static readonly HashSet<Vector2i> Locked = new HashSet<Vector2i>();
        private static Player _owner;

        private static void EnsureLoaded()
        {
            var player = Player.m_localPlayer;
            if (player == _owner)
                return;
            _owner = player;
            Locked.Clear();
            if (player == null || !player.m_customData.TryGetValue(Key, out var raw) || string.IsNullOrEmpty(raw))
                return;
            foreach (var part in raw.Split(';'))
            {
                var xy = part.Split(',');
                if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y))
                    Locked.Add(new Vector2i(x, y));
            }
        }

        private static void Save()
        {
            if (_owner == null)
                return;
            var sb = new StringBuilder();
            foreach (var p in Locked)
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append(p.x).Append(',').Append(p.y);
            }
            if (sb.Length == 0)
                _owner.m_customData.Remove(Key);
            else
                _owner.m_customData[Key] = sb.ToString();
        }

        /// <summary>Locked by us or favorited in Quick Stack - Store - Sort (both mean "leave this slot alone").</summary>
        public static bool IsLocked(Vector2i pos)
            => IsOwnLocked(pos) || (QuickStackStoreCompat.Active && QuickStackStoreCompat.IsFavoritedSlot(pos));

        public static bool IsOwnLocked(Vector2i pos)
        {
            EnsureLoaded();
            return Locked.Count > 0 && Locked.Contains(pos);
        }

        public static bool Toggle(Vector2i pos)
        {
            EnsureLoaded();
            bool nowLocked;
            if (Locked.Remove(pos))
                nowLocked = false;
            else
            {
                Locked.Add(pos);
                nowLocked = true;
            }
            Save();
            return nowLocked;
        }
    }
}
