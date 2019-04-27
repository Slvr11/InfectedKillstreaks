using System;
using System.Collections.Generic;
using InfinityScript;

namespace Infectus
{
    public class Example : BaseScript
    {
        List<string> KillstreakWeapon;
        Dictionary<string, Action<Entity>> StreakDefine = new Dictionary<string, Action<Entity>>();

        public Example()
            : base()
        {
            PlayerConnected += OnPlayerConnected;
            StreakDefine.Add("15_ExpBullet", triggerer => ExpBullet(triggerer));
            StreakDefine.Add("30_AA12", triggerer => AA12(triggerer));
            StreakDefine.Add("4_UAVStrike", triggerer => UAVStrike(triggerer));
            // Add, add, add, above add is an example
        }

        void OnPlayerConnected(Entity player)
        {
            player.OnNotify("joined_team", e =>
            {
                if (player.GetField<string>("sessionteam") == "allies")
                {
                    var streaklist = new List<string>();
                    player.SetField("allks", new Parameter(streaklist)); // Create this list to stack all killstreak
                    player.SetField("kscount", 0); // Delete this line if you want player's killstreak count is included both normal kills and killstreak kills
                    player.Call("notifyonplayercommand", "useks", "+actionslot 4");
                    player.OnNotify("useks", e1 =>
                    {
                        var allks = player.GetField<List<string>>("allks");
                        if (allks.Count > 0)
                        {
                            var lastks = allks[allks.Count - 1];
                            allks.Remove(lastks);
                            player.SetField("allks", new Parameter(allks)); // Mutable list
                            // Now we will find the portable action for lastks in StreakDefine
                            var action = StreakDefine[lastks];
                            action.Invoke(player);
                        }
                    });
                }
            });
        }

        public override void OnPlayerKilled(Entity player, Entity inflictor, Entity attacker, int damage, string mod, string weapon, Vector3 dir, string hitLoc)
        {
            if (attacker.GetField<string>("sessionteam") == "allies" && player.GetField<string>("sessionteam") == "axis" && Call<int>("isplayer", new Parameter(attacker)) != 0)
            {
                // Code for kscount, see above comment:
                if (!KillstreakWeapon.Contains(weapon))
                    attacker.SetField("kscount", attacker.GetField<int>("kscount") + 1);
                else
                    return;
                ///////////////////////////////////////
                // Now we check if attacker have enough kill for a killstreak
                if (StreakDefine.Count > 0)
                {
                    foreach (var pair in StreakDefine)
                    {
                        var split = pair.Key.Split('_'); // For example: 60_supernuke splitted in to 60 and supernuke
                        var killRequired = int.Parse(split[0]);
                        if (attacker.GetField<int>("kscount") == killRequired)
                        {
                            var streaklist = attacker.GetField<List<string>>("allks");
                            if (!streaklist.Contains(pair.Key)) // Add new killstreak only if the attacker doesn't have it
                            {
                                streaklist.Add(pair.Key);
                                attacker.SetField("allks", new Parameter(streaklist)); // Mutable list
                            }
                            break; // Stop the lood anyway
                        }
                    }
                }
            }
        }

        /* Summary:
         * If you want killstreak count include both normal kills and killstreak kills
         * Delete: KillstreakWeapon variable, kscount field and in OnPlayerConnected, check field kills of attacker instead of field kscount
         */

        void ExpBullet(Entity player)
        {
            player.GiveWeapon("killstreak_uav_mp");
            player.SwitchToWeaponImmediate("killstreak_uav_mp");
            player.AfterDelay(1000, entity =>
                player.TakeWeapon("killstreak_uav_mp"));
            Call("iprintlnbold", player.Name + " has Explosive Bullets!");
            player.SetPerk("specialty_explosivebullets", true, true);
            player.Call("iPrintLnBold", string.Format("15 Killstreak! Explosive Bullets Achieved! 30 seconds left."));
            player.AfterDelay(2500, entity =>
                player.Call(33392, "explosive_ammo", 0));
            player.AfterDelay(21000, entity =>
                player.Call("iPrintLnBold", string.Format("10 seconds left for Explosive Bullets!")));
            player.AfterDelay(31000, entity =>
                player.Call("iPrintLnBold", string.Format("Explosive Bullets over!")));
            player.AfterDelay(31010, entity =>
                player.TakeWeapon("specialty_explosivebullets"));
        }
        void AA12(Entity player)
        {
            Call("iprintlnbold", player.Name + " has the AA12!");
            player.Call("iPrintLnBold", string.Format("30 Killstreak! Auto-Shotgun Achieved!"));
            player.AfterDelay(2000, entity =>
                    player.Call(33392, "auto_shotgun", 1));
            player.GiveWeapon("iw5_aa12_mp");
            Stock(player, 32);
            player.SwitchToWeaponImmediate("iw5_aa12_mp");
            player.OnNotify("weapon_fired", (e, _weapon) =>
            {
                // Try to use _weapon instead
                var currentWeapon = player.Call<string>(33490);
                if (player.Call<int>(33470, "iw5_aa12_mp") == 0 && player.Call<int>(33471, "iw5_aa12_mp") == 0)
                    OnInterval(50, () => Stock(player, 0));
                // Missing switch to next weapon, use Stock to keep ammo at 0. Keep weapon. 
            });
        }

        private int Laser_FX;

        void UAVStrike(Entity player)
        {
            giveKillstreakWeapon(player, "uav");
            player.OnNotify("weapon_change", (entity, newWeap) =>
            {
                if (mayDropWeapon((string)newWeap))
                    player.SetField("lastDroppableWeapon", (string)newWeap);
                KillstreakUseWaiter(player, (string)newWeap);
            });

            player.OnNotify("weapon_fired", (ent, weaponName) =>
            {
                if ((string)weaponName != "uav_strike_marker_mp")
                    return;

                player.AfterDelay(900, entity => TakeUAVWeapon(player));

                PrintNameInFeed(player);

                if (player.GetField<string>("customStreak") == "uav")
                {
                    player.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, false);
                    player.Call("SetPlayerData", "killstreaksState", "icons", 0, 0);
                    player.SetField("customStreak", string.Empty);
                }

                Vector3 playerForward = ent.Call<Vector3>("gettagorigin", "tag_weapon") + Call<Vector3>("AnglesToForward", ent.Call<Vector3>("getplayerangles")) * 100000;

                Entity refobject = Call<Entity>("spawn", "script_model", ent.Origin);
                refobject.Call("setmodel", "com_plasticcase_beige_big");
                refobject.SetField("angles", ent.Call<Vector3>("getplayerangles"));
                refobject.Call("moveto", playerForward, 100);
                refobject.Call("hide");
                refobject.OnInterval(50, (refent) =>
                {
                    if (CollidingSoon(refent, ent))
                    {
                        Laser_FX = Call<int>("loadfx", "misc/laser_glow");

                        Call("magicbullet", "uav_strike_projectile_mp", new Vector3(refent.Origin.X, refent.Origin.Y, refent.Origin.Z + 6000), refent.Origin, ent);

                        Entity redfx = Call<Entity>("spawnfx", Laser_FX, refent.Origin);
                        Call("triggerfx", redfx);
                        AfterDelay(4500, () => { redfx.Call("delete"); });
                        return false;
                    }

                    return true;

                });
            });
        }
        private bool CollidingSoon(Entity refobject, Entity player)
        {
            Vector3 endorigin = refobject.Origin + Call<Vector3>("anglestoforward", refobject.GetField<Vector3>("angles")) * 100;

            if (SightTracePassed(refobject.Origin, endorigin, false, player))
                return false;
            else
                return true;
        }
        private bool SightTracePassed(Vector3 StartOrigin, Vector3 EndOrigin, bool tracecharacters, Entity ignoringent)
        {
            int trace = Call<int>("SightTracePassed", new Parameter(StartOrigin), new Parameter(EndOrigin), tracecharacters, new Parameter(ignoringent));
            if (trace > 0)
                return true;
            else
                return false;
        }
        public void PrintNameInFeed(Entity player)
        {
            Call(334, string.Format("UAV Strike called in by {0}", player.GetField<string>("name")));
        }
        public void TakeUAVWeapon(Entity player)
        {
            player.TakeWeapon("uav_strike_marker_mp");
            player.Call("ControlsUnlink");
        }
        private void giveKillstreakWeapon(Entity ent, string streakName)
        {
            string wep = getKillstreakWeapon(streakName);
            if (string.IsNullOrEmpty(wep))
                return;

            ent.SetField("customStreak", streakName);

            ent.Call("giveWeapon", wep, 0, false);
            ent.Call("setActionSlot", 4, "weapon", wep);
            ent.Call("SetPlayerData", "killstreaksState", "hasStreak", 0, true);
            ent.Call("SetPlayerData", "killstreaksState", "icons", 0, getKillstreakIndex("predator_missile"));
            ent.Call(33392, "uav_strike", 0);
        }
        private string getKillstreakWeapon(string streakName)
        {
            string ret = string.Empty;
            ret = Call<string>("tableLookup", "mp/killstreakTable.csv", 1, streakName, 12);
            return ret;
        }
        private int getKillstreakIndex(string streakName)
        {
            int ret = 0;
            ret = Call<int>("tableLookupRowNum", "mp/killstreakTable.csv", 1, streakName) - 1;

            return ret;
        }
        private void KillstreakUseWaiter(Entity ent, string weapon)
        {
            if (weapon == "killstreak_uav_mp")
            {
                var elem = HudElem.CreateFontString(ent, "hudlarge", 2.5f);
                elem.SetPoint("BOTTOMCENTER", "BOTTOMCENTER");
                elem.SetText("Lase target for Predator Strike.");
                ent.TakeWeapon("killstreak_uav_mp");
                ent.AfterDelay(3500, player => elem.SetText(""));
                ent.GiveWeapon("uav_strike_marker_mp");
                ent.SwitchToWeapon("uav_strike_marker_mp");
            }
        }
        private bool mayDropWeapon(string weapon)
        {
            if (weapon == "none")
                return false;

            if (weapon.Contains("ac130"))
                return false;

            string invType = Call<string>("WeaponInventoryType", weapon);
            if (invType != "primary")
                return false;

            return true;
        }
        public bool Stock(Entity player, int amount)
        {
            player.Call("setweaponammostock", "iw5_aa12_mp", amount);
            return true;
        }
    }
}