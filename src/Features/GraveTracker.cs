using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace ValheimSlots
{
    /// <summary>
    /// Remembers where your graves are (per character and world), points the HUD arrow at the nearest one
    /// and removes the record – plus Valheim's death pin on the map – once the grave has been emptied.
    /// Stored in BepInEx/config/lorcisen.valheimslots.graves.txt so it survives restarts.
    /// </summary>
    [HarmonyPatch]
    internal static class GraveTracker
    {
        private sealed class Grave
        {
            public long PlayerId;
            public string World;
            public Vector3 Pos;
            public int Day;
            public float MissingSince = -1f;
        }

        private const float SearchRadius = 20f;      // a tombstone can float/slide a bit
        private const float ConfirmGoneRadius = 25f; // close enough that the tombstone must be loaded
        private const float ConfirmGoneSeconds = 5f;

        private static readonly List<Grave> Graves = new List<Grave>();
        private static bool _loaded;
        private static float _nextCheck;

        private static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".graves.txt");

        /// <summary>Nearest grave in this world for this character, or null.</summary>
        internal static Vector3? Nearest(Player player, out float distance)
        {
            distance = 0f;
            EnsureLoaded();
            Vector3? best = null;
            float bestDist = float.MaxValue;
            foreach (var g in Current(player))
            {
                float d = Vector3.Distance(player.transform.position, g.Pos);
                if (d < bestDist) { bestDist = d; best = g.Pos; }
            }
            distance = bestDist;
            return best;
        }

        // --- Recording ----------------------------------------------------------------------

        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        [HarmonyPrefix]
        private static void Player_CreateTombStone_Prefix(Player __instance, out bool __state)
            => __state = __instance == Player.m_localPlayer && __instance.m_inventory.NrOfItems() > 0
                         && ZoneSystem.instance != null && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathKeepInventory);

        [HarmonyPatch(typeof(Player), nameof(Player.CreateTombStone))]
        [HarmonyPostfix]
        private static void Player_CreateTombStone_Postfix(Player __instance, bool __state)
        {
            if (!__state || ZNet.instance == null || EnvMan.instance == null)
                return;
            EnsureLoaded();
            Graves.Add(new Grave
            {
                PlayerId = Game.instance.GetPlayerProfile().GetPlayerID(),
                World = ZNet.instance.GetWorldName(),
                Pos = __instance.GetCenterPoint(),
                Day = EnvMan.instance.GetDay(ZNet.instance.GetTimeSeconds()),
            });
            Save();
        }

        // --- Tracking / cleanup -----------------------------------------------------------------

        internal static void Tick(Player player)
        {
            if (Time.time < _nextCheck)
                return;
            _nextCheck = Time.time + 1f;
            EnsureLoaded();

            var mine = Current(player);
            if (mine.Count == 0)
                return;

            long myId = Game.instance.GetPlayerProfile().GetPlayerID();
            TombStone[] stones = null;
            bool changed = false;

            foreach (var g in mine)
            {
                float playerDist = Vector3.Distance(player.transform.position, g.Pos);
                if (playerDist > ConfirmGoneRadius + SearchRadius)
                {
                    g.MissingSince = -1f;
                    continue;
                }

                stones ??= UnityEngine.Object.FindObjectsByType<TombStone>(FindObjectsSortMode.None);
                TombStone found = null;
                foreach (var s in stones)
                {
                    if (s == null || s.m_nview == null || !s.m_nview.IsValid() || s.GetOwner() != myId)
                        continue;
                    if (Vector3.Distance(s.transform.position, g.Pos) <= SearchRadius)
                    {
                        found = s;
                        break;
                    }
                }

                if (found != null)
                {
                    g.MissingSince = -1f;
                    if (Vector3.Distance(found.transform.position, g.Pos) > 1f)
                    {
                        g.Pos = found.transform.position; // follow a floating grave
                        changed = true;
                    }
                    continue;
                }

                if (playerDist > ConfirmGoneRadius)
                    continue;
                if (g.MissingSince < 0f)
                    g.MissingSince = Time.time;
                else if (Time.time - g.MissingSince >= ConfirmGoneSeconds)
                {
                    RemoveDeathPin(g.Pos);
                    Graves.Remove(g);
                    changed = true;
                    player.Message(MessageHud.MessageType.TopLeft, L.T("Grave emptied - marker removed", "Graven är tömd - markeringen borttagen"));
                }
            }

            if (changed)
                Save();
        }

        private static void RemoveDeathPin(Vector3 pos)
        {
            var map = Minimap.instance;
            if (map == null)
                return;
            Minimap.PinData closest = null;
            float best = SearchRadius + 5f;
            foreach (var pin in map.m_pins)
            {
                if (pin.m_type != Minimap.PinType.Death)
                    continue;
                float d = Vector3.Distance(pin.m_pos, pos);
                if (d < best) { best = d; closest = pin; }
            }
            if (closest != null)
                map.RemovePin(closest);
        }

        private static List<Grave> Current(Player player)
        {
            var result = new List<Grave>();
            if (ZNet.instance == null || Game.instance == null)
                return result;
            long id = Game.instance.GetPlayerProfile().GetPlayerID();
            string world = ZNet.instance.GetWorldName();
            foreach (var g in Graves)
                if (g.PlayerId == id && g.World == world)
                    result.Add(g);
            return result;
        }

        // --- Persistence -------------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;
            Graves.Clear();
            try
            {
                if (!File.Exists(FilePath))
                    return;
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    var p = line.Split('|');
                    if (p.Length != 6) continue;
                    Graves.Add(new Grave
                    {
                        PlayerId = long.Parse(p[0], CultureInfo.InvariantCulture),
                        World = p[1],
                        Pos = new Vector3(F(p[2]), F(p[3]), F(p[4])),
                        Day = int.Parse(p[5], CultureInfo.InvariantCulture),
                    });
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read the grave list: {e.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (var g in Graves)
                    lines.Add(string.Join("|", g.PlayerId.ToString(CultureInfo.InvariantCulture), g.World.Replace("|", ""),
                        S(g.Pos.x), S(g.Pos.y), S(g.Pos.z), g.Day.ToString(CultureInfo.InvariantCulture)));
                File.WriteAllLines(FilePath, lines);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not save the grave list: {e.Message}");
            }
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        private static string S(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
