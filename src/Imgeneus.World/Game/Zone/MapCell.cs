﻿using Imgeneus.Core.Extensions;
using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.World.Game.Attack;
using Imgeneus.World.Game.Buffs;
using Imgeneus.World.Game.Country;
using Imgeneus.World.Game.Health;
using Imgeneus.World.Game.Inventory;
using Imgeneus.World.Game.Monster;
using Imgeneus.World.Game.Movement;
using Imgeneus.World.Game.NPCs;
using Imgeneus.World.Game.PartyAndRaid;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Shape;
using Imgeneus.World.Game.Skills;
using Imgeneus.World.Game.Speed;
using Imgeneus.World.Packets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Imgeneus.World.Game.Zone
{
    public class MapCell : IDisposable
    {
        private readonly PacketsHelper _packetsHelper = new PacketsHelper();

        public MapCell(int index, IEnumerable<int> neighborCells, Map map)
        {
            CellIndex = index;
            NeighborCells = neighborCells;
            Map = map;
        }

        public int CellIndex { get; private set; }

        public IEnumerable<int> NeighborCells { get; private set; }

        protected Map Map { get; private set; }

        /// <summary>
        /// Sets cell index to each cell member.
        /// </summary>
        private void AssignCellIndex(IMapMember member)
        {
            member.OldCellId = member.CellId;
            member.CellId = CellIndex;
        }

        #region Players

        /// <summary>
        /// Thread-safe dictionary of connected players. Key is character id, value is character.
        /// </summary>
        private readonly ConcurrentDictionary<int, Character> Players = new ConcurrentDictionary<int, Character>();

        /// <summary>
        /// Adds character to map cell.
        /// </summary>
        public void AddPlayer(Character character)
        {
            Players.TryAdd(character.Id, character);
            AddListeners(character);
            AssignCellIndex(character);

            // Send update players.
            var oldPlayers = character.OldCellId != -1 ? Map.Cells[character.OldCellId].GetAllPlayers(true) : new List<Character>();
            var newPlayers = GetAllPlayers(true);

            var sendPlayerLeave = oldPlayers.Where(p => !newPlayers.Contains(p) && p != character);
            var sendPlayerEnter = newPlayers.Where(p => !oldPlayers.Contains(p));

            foreach (var player in sendPlayerLeave)
            {
                _packetsHelper.SendCharacterLeave(player.Client, character);
                _packetsHelper.SendCharacterLeave(character.Client, player);
            }

            foreach (var player in sendPlayerEnter)
                if (player.Id != character.Id)
                {
                    // Notify players in this map, that new player arrived.
                    _packetsHelper.SendCharacterEnter(player.Client, character);

                    // Notify new player, about already loaded player.
                    _packetsHelper.SendCharacterEnter(character.Client, player);
                }
                else // Original server sends this also to player himself, although I'm not sure if it's needed.
                     // Added it as a fix for admin stealth.
                    if (character.OldCellId == -1)
                    _packetsHelper.SendCharacterEnter(character.Client, character);

            // Send update npcs.
            var oldCellNPCs = character.OldCellId != -1 ? Map.Cells[character.OldCellId].GetAllNPCs(true) : new List<Npc>();
            var newCellNPCs = GetAllNPCs(true);

            var npcToLeave = oldCellNPCs.Where(npc => !newCellNPCs.Contains(npc));
            var npcToEnter = newCellNPCs.Where(npc => !oldCellNPCs.Contains(npc));

            foreach (var npc in npcToLeave)
                _packetsHelper.SendNpcLeave(character.Client, npc);
            foreach (var npc in npcToEnter)
                _packetsHelper.SendNpcEnter(character.Client, npc);

            // Send update mobs.
            var oldCellMobs = character.OldCellId != -1 ? Map.Cells[character.OldCellId].GetAllMobs(true) : new List<Mob>();
            var newCellMobs = GetAllMobs(true);

            var mobToLeave = oldCellMobs.Where(m => !newCellMobs.Contains(m));
            var mobToEnter = newCellMobs.Where(m => !oldCellMobs.Contains(m));

            foreach (var mob in mobToLeave)
                _packetsHelper.SendMobLeave(character.Client, mob);

            foreach (var mob in mobToEnter)
                _packetsHelper.SendMobEnter(character.Client, mob, false);
        }

        /// <summary>
        /// Tries to get player from map cell.
        /// </summary>
        /// <param name="playerId">id of player, that you are trying to get.</param>
        /// <returns>either player or null if player is not presented</returns>
        public Character GetPlayer(int playerId)
        {
            Players.TryGetValue(playerId, out var player);
            return player;
        }

        /// <summary>
        /// Gets all players from map cell.
        /// </summary>
        /// <param name="includeNeighborCells">if set to true includes characters fom neighbor cells</param>
        public IEnumerable<Character> GetAllPlayers(bool includeNeighborCells)
        {
            var myPlayers = Players.Values;
            if (includeNeighborCells)
                return myPlayers.Concat(NeighborCells.Select(index => Map.Cells[index]).SelectMany(cell => cell.GetAllPlayers(false))).Distinct();
            return myPlayers;
        }

        /// <summary>
        /// Gets player near point.
        /// </summary>
        /// <param name="x">x coordinate</param>
        /// <param name="z">z coordinate</param>
        /// <param name="range">minimum range to target, if set to 0 is not calculated</param>
        /// <param name="country">light, dark or both</param>
        /// <param name="includeDead">include dead players or not</param>
        /// <param name="includeNeighborCells">include players from neighbor cells, usually true</param>
        public IEnumerable<IKillable> GetPlayers(float x, float z, byte range, CountryType country = CountryType.None, bool includeDead = false, bool includeNeighborCells = true)
        {
            var myPlayers = Players.Values.Where(
                     p => (includeDead || !p.HealthManager.IsDead) && // filter by death
                     (p.CountryProvider.Country == country || country == CountryType.None) && // filter by fraction
                     (range == 0 || MathExtensions.Distance(x, p.PosX, z, p.PosZ) <= range)); // filter by range
            if (includeNeighborCells)
                return myPlayers.Concat(NeighborCells.Select(index => Map.Cells[index]).SelectMany(cell => cell.GetPlayers(x, z, range, country, includeDead, false))).Distinct();
            return myPlayers;
        }

        /// <summary>
        /// Gets enemies near target.
        /// </summary>
        public IEnumerable<IKillable> GetEnemies(Character sender, IKillable target, byte range)
        {
            IEnumerable<IKillable> mobs = GetAllMobs(true).Where(m => !m.HealthManager.IsDead && MathExtensions.Distance(target.MovementManager.PosX, m.PosX, target.MovementManager.PosZ, m.PosZ) <= range);
            IEnumerable<IKillable> chars = GetAllPlayers(true).Where(p => !p.HealthManager.IsDead && p.CountryProvider.Country != sender.CountryProvider.Country && MathExtensions.Distance(target.MovementManager.PosX, p.PosX, target.MovementManager.PosZ, p.PosZ) <= range);

            return mobs.Concat(chars);
        }

        /// <summary>
        /// Removes player from map cell.
        /// </summary>
        public void RemovePlayer(Character character, bool notifyPlayers)
        {
            RemoveListeners(character);
            Players.TryRemove(character.Id, out var removedCharacter);

            foreach (var mob in GetAllMobs(true).Where(m => m.Target == character))
                mob.ClearTarget();

            if (notifyPlayers)
                foreach (var player in GetAllPlayers(true))
                    _packetsHelper.SendCharacterLeave(player.Client, character);
        }

        /// <summary>
        /// Subscribes to character events.
        /// </summary>
        private void AddListeners(Character character)
        {
            // Map with id is test map.
            if (character.MapProvider.NextMapId == Map.TEST_MAP_ID)
                return;
            character.MovementManager.OnMove += Character_OnMove;
            character.OnMotion += Character_OnMotion;
            character.InventoryManager.OnEquipmentChanged += Character_OnEquipmentChanged;
            character.PartyManager.OnPartyChanged += Character_OnPartyChanged;
            character.SpeedManager.OnAttackOrMoveChanged += Character_OnAttackOrMoveChanged;
            character.SkillsManager.OnUsedSkill += Character_OnUsedSkill;
            character.SkillsManager.OnUsedRangeSkill += Character_OnUsedRangeSkill;
            character.AttackManager.OnAttack += Character_OnAttack;
            character.HealthManager.OnDead += Character_OnDead;
            character.SkillsManager.OnSkillCastStarted += Character_OnSkillCastStarted;
            character.InventoryManager.OnUsedItem += Character_OnUsedItem;
            character.HealthManager.OnMaxHPChanged += Character_OnMaxHPChanged;
            character.HealthManager.OnMaxSPChanged += Character_OnMaxSPChanged;
            character.HealthManager.OnMaxMPChanged += Character_OnMaxMPChanged;
            //character.OnMax_HP_MP_SP_Changed += Character_OnMax_HP_MP_SP_Changed;
            character.HealthManager.OnRecover += Character_OnRecover;
            character.BuffsManager.OnSkillKeep += Character_OnSkillKeep;
            character.ShapeManager.OnShapeChange += Character_OnShapeChange;
            character.HealthManager.OnRebirthed += Character_OnRebirthed;
            character.AdditionalInfoManager.OnAppearanceChanged += Character_OnAppearanceChanged;
            character.VehicleManager.OnStartSummonVehicle += Character_OnStartSummonVehicle;
            character.OnLevelUp += Character_OnLevelUp;
            character.OnAdminLevelChange += Character_OnAdminLevelChange;
            character.VehicleManager.OnVehiclePassengerChanged += Character_OnVehiclePassengerChanged;
            character.TeleportationManager.OnTeleporting += Character_OnTeleport;
        }

        /// <summary>
        /// Unsubscribes from character events.
        /// </summary>
        private void RemoveListeners(Character character)
        {
            character.MovementManager.OnMove -= Character_OnMove;
            character.OnMotion -= Character_OnMotion;
            character.InventoryManager.OnEquipmentChanged -= Character_OnEquipmentChanged;
            character.PartyManager.OnPartyChanged -= Character_OnPartyChanged;
            character.SpeedManager.OnAttackOrMoveChanged -= Character_OnAttackOrMoveChanged;
            character.SkillsManager.OnUsedSkill -= Character_OnUsedSkill;
            character.SkillsManager.OnUsedRangeSkill -= Character_OnUsedRangeSkill;
            character.AttackManager.OnAttack -= Character_OnAttack;
            character.HealthManager.OnDead -= Character_OnDead;
            character.SkillsManager.OnSkillCastStarted -= Character_OnSkillCastStarted;
            character.InventoryManager.OnUsedItem -= Character_OnUsedItem;
            character.HealthManager.OnMaxHPChanged -= Character_OnMaxHPChanged;
            character.HealthManager.OnMaxSPChanged -= Character_OnMaxSPChanged;
            character.HealthManager.OnMaxMPChanged -= Character_OnMaxMPChanged;
            //character.OnMax_HP_MP_SP_Changed -= Character_OnMax_HP_MP_SP_Changed;
            character.HealthManager.OnRecover -= Character_OnRecover;
            character.BuffsManager.OnSkillKeep -= Character_OnSkillKeep;
            character.ShapeManager.OnShapeChange -= Character_OnShapeChange;
            character.HealthManager.OnRebirthed -= Character_OnRebirthed;
            character.AdditionalInfoManager.OnAppearanceChanged -= Character_OnAppearanceChanged;
            character.VehicleManager.OnStartSummonVehicle -= Character_OnStartSummonVehicle;
            character.OnLevelUp -= Character_OnLevelUp;
            character.OnAdminLevelChange -= Character_OnAdminLevelChange;
            character.VehicleManager.OnVehiclePassengerChanged -= Character_OnVehiclePassengerChanged;
            character.TeleportationManager.OnTeleporting -= Character_OnTeleport;
        }

        #region Character listeners

        /// <summary>
        /// Notifies other players about position change.
        /// </summary>
        private void Character_OnMove(int senderId, float x, float y, float z, ushort a, MoveMotion motion)
        {
            // Send other clients notification, that user is moving.
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendCharacterMoves(player.Client, senderId, x, y, z ,a, motion);
        }

        /// <summary>
        /// When player sends motion, we should resend this motion to all other players on this map.
        /// </summary>
        private void Character_OnMotion(Character playerWithMotion, Motion motion)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendCharacterMotion(player.Client, playerWithMotion.Id, motion);
        }

        /// <summary>
        /// Notifies other players, that this player changed equipment.
        /// </summary>
        /// <param name="characterId">player, that changed equipment</param>
        /// <param name="equipmentItem">item, that was worn</param>
        /// <param name="slot">item slot</param>
        private void Character_OnEquipmentChanged(int characterId, Item equipmentItem, byte slot)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendCharacterChangedEquipment(player.Client, characterId, equipmentItem, slot);
        }

        /// <summary>
        ///  Notifies other players, that player entered/left party or got/removed leader.
        /// </summary>
        private void Character_OnPartyChanged(Character sender)
        {
            foreach (var player in GetAllPlayers(true))
            {
                PartyMemberType type = PartyMemberType.NoParty;

                if (sender.PartyManager.IsPartyLead)
                    type = PartyMemberType.Leader;
                else if (sender.PartyManager.HasParty)
                    type = PartyMemberType.Member;

                _packetsHelper.SendCharacterPartyChanged(player.Client, sender.Id, type);
            }
        }

        /// <summary>
        /// Notifies other players, that player changed attack/move speed.
        /// </summary>
        private void Character_OnAttackOrMoveChanged(int senderId, AttackSpeed attack, MoveSpeed move)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendAttackAndMovementSpeed(player.Client, senderId, attack, move);
        }

        /// <summary>
        /// Notifies other players, that player used skill.
        /// </summary>
        private void Character_OnUsedSkill(int senderId, IKillable target, Skill skill, AttackResult attackResult)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendCharacterUsedSkill(player.Client, senderId, target, skill, attackResult);

                if (attackResult.Absorb != 0 && player == target)
                    _packetsHelper.SendAbsorbValue(player.Client, attackResult.Absorb);
            }
        }

        /// <summary>
        /// Notifies other players, that player used auto attack.
        /// </summary>
        private void Character_OnAttack(int senderId, IKillable target, AttackResult attackResult)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendCharacterUsualAttack(player.Client, senderId, target, attackResult);

                if (attackResult.Absorb != 0 && player == target)
                    _packetsHelper.SendAbsorbValue(player.Client, attackResult.Absorb);
            }
        }

        /// <summary>
        /// Notifies other players, that player is dead.
        /// </summary>
        private void Character_OnDead(int senderId, IKiller killer)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendCharacterKilled(player.Client, senderId, killer);
        }

        /// <summary>
        /// Notifies other players, that player starts casting.
        /// </summary>
        private void Character_OnSkillCastStarted(int senderId, IKillable target, Skill skill)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendSkillCastStarted(player.Client, senderId, target, skill);
        }

        /// <summary>
        /// Notifies other players, that player used some item.
        /// </summary>
        private void Character_OnUsedItem(int senderId, Item item)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendUsedItem(player.Client, senderId, item);
        }

        private void Character_OnRecover(int senderId, int hp, int mp, int sp)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendRecoverCharacter(player.Client, senderId, hp, mp, sp);
        }

        private void Character_OnMaxHPChanged(int senderId, int value)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendMaxHitpoints(player.Client, senderId, HitpointType.HP, value);
        }

        private void Character_OnMaxSPChanged(int senderId, int value)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendMaxHitpoints(player.Client, senderId, HitpointType.SP, value);
        }

        private void Character_OnMaxMPChanged(int senderId, int value)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendMaxHitpoints(player.Client, senderId, HitpointType.MP, value);
        }

        /// <summary>
        /// Notifies other players that player's max HP, MP and SP changed
        /// </summary>
        private void Character_OnMax_HP_MP_SP_Changed(IKillable sender)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendMax_HP_MP_SP(player.Client, (Character)sender);
        }

        private void Character_OnSkillKeep(int senderId, Buff buff, AttackResult result)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendSkillKeep(player.Client, senderId, buff.SkillId, buff.SkillLevel, result);
        }

        private void Character_OnShapeChange(int senderId, ShapeEnum shape, int param1, int param2)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendShapeUpdate(player.Client, senderId, shape, param1, param2);
        }

        private void Character_OnUsedRangeSkill(int senderId, IKillable target, Skill skill, AttackResult attackResult)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendUsedRangeSkill(player.Client, senderId, target, skill, attackResult);

                if (attackResult.Absorb != 0 && player == target)
                    _packetsHelper.SendAbsorbValue(player.Client, attackResult.Absorb);
            }
        }

        private void Character_OnRebirthed(int senderId)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendCharacterRebirth(player.Client, senderId);
                _packetsHelper.SendDeadRebirth(player.Client, Players[senderId]);
            }
        }

        private void Character_OnAppearanceChanged(int characterId, byte hair, byte face, byte size, byte gender)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendAppearanceChanged(player.Client, characterId, hair, face, size, gender);
        }

        private void Character_OnStartSummonVehicle(int senderId)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendStartSummoningVehicle(player.Client, senderId);
        }

        /// <summary>
        /// Notifies other players that player levelled up
        /// </summary>
        private void Character_OnLevelUp(Character sender)
        {
            foreach (var player in GetAllPlayers(true))
                // If sender has party, send admin level up
                _packetsHelper.SendLevelUp(player.Client, sender, sender.PartyManager.HasParty);
        }

        /// <summary>
        /// Notifies other players that an admin changed a player's level
        /// </summary>
        private void Character_OnAdminLevelChange(Character sender)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendLevelUp(player.Client, sender, true);
        }

        /// <summary>
        /// Notifies other players that 2 character now move together on 1 vehicle.
        /// </summary>
        private void Character_OnVehiclePassengerChanged(int senderId, int vehicle2CharacterID)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.VehiclePassengerChanged(player.Client, senderId, vehicle2CharacterID);
        }

        /// <summary>
        /// Teleports player to new position.
        /// </summary>
        private void Character_OnTeleport(int senderId, ushort mapId, float x, float y, float z, bool teleportedByAdmin)
        {
            foreach (var p in GetAllPlayers(true))
                Map.PacketFactory.SendCharacterTeleport(p.Client, senderId, mapId, x, y, z, teleportedByAdmin);
        }

        #endregion

        #endregion

        #region Mobs

        /// <summary>
        /// Thread-safe dictionary of monsters loaded to this map. Where key id mob id.
        /// </summary>
        private readonly ConcurrentDictionary<int, Mob> Mobs = new ConcurrentDictionary<int, Mob>();

        /// <summary>
        /// Adds mob to cell.
        /// </summary>
        public void AddMob(Mob mob)
        {
            Mobs.TryAdd(mob.Id, mob);
            AssignCellIndex(mob);
            AddListeners(mob);

            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendMobEnter(player.Client, mob, true);
        }

        /// <summary>
        /// Removes mob from cell.
        /// </summary>
        public void RemoveMob(Mob mob)
        {
            Mobs.TryRemove(mob.Id, out var removedMob);
            RemoveListeners(removedMob);

            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendMobLeave(player.Client, mob);
        }

        /// <summary>
        /// Tries to get mob from map.
        /// </summary>
        /// <param name="mobId">id of mob, that you are trying to get.</param>
        /// <param name="includeNeighborCells">search also in neighbor cells</param>
        /// <returns>either mob or null if mob is not presented</returns>
        public Mob GetMob(int mobId, bool includeNeighborCells)
        {
            Mob mob;
            Mobs.TryGetValue(mobId, out mob);

            if (mob is null && includeNeighborCells) // Maybe mob in neighbor cell?
                foreach (var cellId in NeighborCells)
                {
                    mob = Map.Cells[cellId].GetMob(mobId, false);
                    if (mob != null)
                        break;
                }

            return mob;
        }

        /// <summary>
        /// Called, when mob respawns.
        /// </summary>
        /// <param name="sender">respawned mob</param>
        public void RebirthMob(Mob sender)
        {
            sender.TimeToRebirth -= RebirthMob;

            // Create mob clone, because we can not reuse the same id.
            var mob = sender.Clone();

            // TODO: generate rebirth coordinates based on the spawn area.
            mob.MovementManager.PosX = sender.PosX;
            mob.MovementManager.PosY = sender.PosY;
            mob.MovementManager.PosZ = sender.PosZ;

            AddMob(mob);
        }

        /// <summary>
        /// Gets all mobs from map cell.
        /// </summary>
        /// /// <param name="includeNeighborCells">if set to true includes mobs fom neighbor cells</param>
        public IEnumerable<Mob> GetAllMobs(bool includeNeighborCells)
        {
            var myMobs = Mobs.Values;
            if (includeNeighborCells)
                return myMobs.Concat(NeighborCells.Select(index => Map.Cells[index]).SelectMany(cell => cell.GetAllMobs(false))).Distinct();
            return myMobs;
        }

        /// <summary>
        /// Adds listeners to mob events.
        /// </summary>
        /// <param name="mob">mob, that we listen</param>
        private void AddListeners(Mob mob)
        {
            mob.HealthManager.OnDead += Mob_OnDead;
            mob.MovementManager.OnMove += Mob_OnMove;
            mob.OnAttack += Mob_OnAttack;
            mob.OnUsedSkill += Mob_OnUsedSkill;
            mob.HealthManager.OnRecover += Mob_OnRecover;
        }

        /// <summary>
        /// Removes listeners from mob.
        /// </summary>
        /// <param name="mob">mob, that we listen</param>
        private void RemoveListeners(Mob mob)
        {
            mob.HealthManager.OnDead -= Mob_OnDead;
            mob.MovementManager.OnMove -= Mob_OnMove;
            mob.OnAttack -= Mob_OnAttack;
            mob.OnUsedSkill -= Mob_OnUsedSkill;
            mob.HealthManager.OnRecover -= Mob_OnRecover;
            mob.TimeToRebirth -= RebirthMob;
        }

        private void Mob_OnDead(int senderId, IKiller killer)
        {
            Mobs.TryRemove(senderId, out var mob);
            RemoveListeners(mob);

            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendMobDead(player.Client, senderId, killer);

            // Add experience to killer character/party
            if (killer is Character killerCharacter)
                if (killerCharacter.PartyManager.HasParty)
                    killerCharacter.AddPartyMobExperience(mob.LevelProvider.Level, (ushort)mob.Exp);
                else
                    killerCharacter.AddMobExperience(mob.LevelProvider.Level, (ushort)mob.Exp);

            if (Map is GRBMap)
                (Map as GRBMap).AddPoints(mob.GuildPoints);

            if (mob.ShouldRebirth)
                mob.TimeToRebirth += RebirthMob;

            mob.Dispose();
        }

        private void Mob_OnMove(int senderId, float x, float y, float z, ushort a, MoveMotion motion)
        {
            foreach (var player in GetAllPlayers(true))
                _packetsHelper.SendMobMove(player.Client, senderId, x, z, motion);
        }

        private void Mob_OnAttack(IKiller sender, IKillable target, AttackResult attackResult)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendMobAttack(player.Client, (Mob)sender, target.Id, attackResult);

                if (attackResult.Absorb != 0 && player == target)
                    _packetsHelper.SendAbsorbValue(player.Client, attackResult.Absorb);
            }
        }

        private void Mob_OnUsedSkill(IKiller sender, IKillable target, Skill skill, AttackResult attackResult)
        {
            foreach (var player in GetAllPlayers(true))
            {
                _packetsHelper.SendMobUsedSkill(player.Client, (Mob)sender, target.Id, skill, attackResult);

                if (attackResult.Absorb != 0 && player == target)
                    _packetsHelper.SendAbsorbValue(player.Client, attackResult.Absorb);
            }
        }

        private void Mob_OnRecover(int senderId, int hp, int mp, int sp)
        {
            foreach (var player in GetAllPlayers(true))
                Map.PacketFactory.SendMobRecover(player.Client, senderId, hp);
        }

        #endregion

        #region NPCs

        /// <summary>
        /// Thread-safe dictionary of npcs. Key is npc id, value is npc.
        /// </summary>
        private readonly ConcurrentDictionary<int, Npc> NPCs = new ConcurrentDictionary<int, Npc>();

        /// <summary>
        /// Adds npc to cell.
        /// </summary>
        /// <param name="npc">npc to add</param>
        public void AddNPC(Npc npc)
        {
            if (NPCs.TryAdd(npc.Id, npc))
            {
                AssignCellIndex(npc);
                foreach (var player in GetAllPlayers(true))
                    _packetsHelper.SendNpcEnter(player.Client, npc);
            }
        }

        /// <summary>
        /// Removes npc from cell.
        /// </summary>
        public void RemoveNPC(byte type, ushort typeId, byte count)
        {
            var npcs = NPCs.Values.Where(n => n.Type == type && n.TypeId == typeId).Take(count);
            foreach (var npc in npcs)
            {
                if (NPCs.TryRemove(npc.Id, out var removedNpc))
                {
                    foreach (var player in GetAllPlayers(true))
                        _packetsHelper.SendNpcLeave(player.Client, npc);
                }
            }
        }

        /// <summary>
        /// Gets NPC by id.
        /// </summary>
        /// <param name="includeNeighborCells">search also in neighbor cells</param>
        public Npc GetNPC(int id, bool includeNeighborCells)
        {
            Npc npc;
            NPCs.TryGetValue(id, out npc);

            if (npc is null && includeNeighborCells) // Maybe npc in neighbor cell?
                foreach (var cellId in NeighborCells)
                {
                    npc = Map.Cells[cellId].GetNPC(id, false);
                    if (npc != null)
                        break;
                }

            return npc;
        }

        /// <summary>
        /// Gets all npcs of this cell.
        /// </summary>
        /// <returns>collection of npcs</returns>
        public IEnumerable<Npc> GetAllNPCs(bool includeNeighbors)
        {
            var myNPCs = NPCs.Values;
            if (includeNeighbors)
                return myNPCs.Concat(NeighborCells.SelectMany(index => Map.Cells[index].GetAllNPCs(false))).Distinct();
            return myNPCs;
        }

        #endregion

        #region Items

        /// <summary>
        /// Dropped items.
        /// </summary>
        private readonly ConcurrentDictionary<int, MapItem> Items = new ConcurrentDictionary<int, MapItem>();

        /// <summary>
        /// Adds item on map cell.
        /// </summary>
        /// <param name="item">new added item</param>
        public void AddItem(MapItem item)
        {
            if (Items.TryAdd(item.Id, item))
            {
                AssignCellIndex(item);
                foreach (var player in GetAllPlayers(true))
                    _packetsHelper.SendAddItem(player.Client, item);
            }
        }

        /// <summary>
        /// Tries to get item from map cell.
        /// </summary>
        /// <returns>if item is null, means that item doesn't belong to player yet</returns>
        public MapItem GetItem(int itemId, Character requester, bool includeNeighborCells)
        {
            MapItem mapItem;
            if (Items.TryGetValue(itemId, out mapItem))
            {
                if (mapItem.Owner == null || mapItem.Owner == requester)
                {
                    return mapItem;
                }
                else
                {
                    return null;
                }
            }
            else // Maybe item is in neighbor cell?
            {
                if (includeNeighborCells)
                    foreach (var cellId in NeighborCells)
                    {
                        mapItem = Map.Cells[cellId].GetItem(itemId, requester, false);
                        if (mapItem != null)
                            break;
                    }

                return mapItem;
            }
        }

        /// <summary>
        /// Tries to get all items from map cell.
        /// </summary>
        /// <param name="includeNeighborCells"></param>
        public IEnumerable<MapItem> GetAllItems(bool includeNeighborCells)
        {
            List<MapItem> mapItems = new List<MapItem>();
            if (includeNeighborCells)
            {
                foreach (var cellId in NeighborCells)
                {
                    mapItems.AddRange(Map.Cells[cellId].GetAllItems(false));
                }
            }
            return Items.Values.Concat(mapItems);
        }

        /// <summary>
        /// Removes item from map.
        /// </summary>
        public MapItem RemoveItem(int itemId)
        {
            if (Items.TryRemove(itemId, out var mapItem))
            {
                mapItem.StopRemoveTimer();
                foreach (var player in GetAllPlayers(true))
                    _packetsHelper.SendRemoveItem(player.Client, mapItem);
            }

            return mapItem;
        }

        #endregion

        #region Dispose

        private bool _isDisposed = false;

        public void Dispose()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(Map));

            _isDisposed = true;

            foreach (var p in Players.Values)
                RemoveListeners(p);

            foreach (var m in Mobs.Values)
            {
                RemoveMob(m);
                m.Dispose();
            }

            foreach (var n in NPCs.Values)
                n.Dispose();

            foreach (var i in Items.Values)
                i.Dispose();

            Players.Clear();
            Mobs.Clear();
            NPCs.Clear();
            Items.Clear();

            Map = null;
        }

        #endregion
    }
}
