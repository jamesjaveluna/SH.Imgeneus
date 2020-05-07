﻿using Imgeneus.Core.DependencyInjection;
using Imgeneus.Database;
using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.DatabaseBackgroundService;
using Imgeneus.World.Game.Trade;
using Imgeneus.World.Packets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MvvmHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Imgeneus.World.Game.Player
{
    public partial class Character : ITargetable
    {
        private readonly ILogger<Character> _logger;
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly CharacterPacketsHelper _packetsHelper;

        public Character(ILogger<Character> logger, IBackgroundTaskQueue taskQueue)
        {
            _logger = logger;
            _taskQueue = taskQueue;
            _packetsHelper = new CharacterPacketsHelper();
            InventoryItems.CollectionChanged += InventoryItems_CollectionChanged;
            Skills.CollectionChanged += Skills_CollectionChanged;
        }

        #region Character info

        public int Id { get; private set; }
        public string Name;
        public Fraction Country;
        public ushort Level;
        public ushort Map;
        public Race Race;
        public CharacterProfession Class;
        public Mode Mode;
        public byte Hair;
        public byte Face;
        public byte Height;
        public Gender Gender;
        public ushort StatPoint;
        public ushort SkillPoint;
        public ushort Strength;
        public ushort Dexterity;
        public ushort Rec;
        public ushort Intelligence;
        public ushort Luck;
        public ushort Wisdom;
        public uint Exp;
        public ushort Kills;
        public ushort Deaths;
        public ushort Victories;
        public ushort Defeats;
        public bool IsAdmin;

        /// <summary>
        ///  Set to 1 if you want character running or to 0 if character is "walking".
        ///  Used to change with Tab in previous episodes.
        /// </summary>
        public byte MoveMotion = 1;

        public bool IsDead;
        public bool HasParty;
        public bool IsPartyLead;

        public int CurrentHP { get; set; }
        public int CurrentMP { get; set; }
        public int CurrentSP { get; set; }

        public int MaxHP { get => CurrentHP * 2; } // TODO: implement max HP. For now return current * 2.
        public int MaxMP { get => CurrentMP * 2; } // TODO: implement max HP. For now return current * 2.
        public int MaxSP { get => CurrentSP * 2; } // TODO: implement max HP. For now return current * 2.

        #endregion

        #region Motion

        /// <summary>
        /// Event, that is fires, when character makes any motion.
        /// </summary>
        public event Action<Character, Motion> OnMotion;

        /// <summary>
        /// Motion, like sit.
        /// </summary>
        public Motion Motion;

        #endregion

        #region Skills

        /// <summary>
        /// Collection of available skills.
        /// </summary>
        public ObservableRangeCollection<Skill> Skills { get; private set; } = new ObservableRangeCollection<Skill>();

        /// <summary>
        /// Player learns new skill.
        /// </summary>
        /// <param name="skillId">skill id</param>
        /// <param name="skillLevel">skill level</param>
        /// <returns>successful or not</returns>
        public void LearnNewSkill(ushort skillId, byte skillLevel)
        {
            using var database = DependencyContainer.Instance.Resolve<IDatabase>();

            if (Skills.Any(s => s.SkillId == skillId && s.SkillLevel == skillLevel))
            {
                // Character has already learned this skill.
                // TODO: log it or throw exception?
            }

            // Find learned skill.
            var dbSkill = database.Skills.First(s => s.SkillId == skillId && s.SkillLevel == skillLevel);
            if (SkillPoint < dbSkill.SkillPoint)
            {
                // Not enough skill points.
                // TODO: log it or throw exception?
            }

            byte skillNumber = 0;

            // Find out if the character has already learned the same skill, but lower level.
            // Find character.
            var dbCharacter = database.Characters.Include(c => c.Skills)
                                               .Where(c => c.Id == Id).First();

            var isSkillLearned = Skills.FirstOrDefault(s => s.SkillId == skillId);
            // If there is skil of lower level => delete it.
            if (isSkillLearned != null)
            {
                _taskQueue.Enqueue(async (args) =>
                    {
                        int charId = (int)args[0];
                        int skillId = (int)args[1];

                        using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                        var skillToRemove = database.CharacterSkills.First(s => s.CharacterId == charId && s.SkillId == skillId);
                        database.CharacterSkills.Remove(skillToRemove);
                        return await database.SaveChangesAsync();
                    },
                    (obj) => { },
                Id, isSkillLearned.Id);

                skillNumber = isSkillLearned.Number;
            }
            // No such skill. Generate new number.
            else
            {
                if (Skills.Any())
                {
                    // Find the next skill number.
                    skillNumber = Skills.Select(s => s.Number).Max();
                    skillNumber++;
                }
                else
                {
                    // No learned skills at all.
                }
            }

            // Save char and learned skill.
            _taskQueue.Enqueue(async (args) =>
                {
                    int charId = (int)args[0];
                    int skillId = (int)args[1];
                    byte skillNumber = (byte)args[2];
                    byte skillPoints = (byte)args[3];

                    using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                    var dbSkill = database.Skills.Find(skillId);
                    var skillToAdd = new DbCharacterSkill()
                    {
                        CharacterId = charId,
                        SkillId = skillId,
                        Skill = dbSkill,
                        Number = skillNumber
                    };

                    database.CharacterSkills.Add(skillToAdd);

                    var character = database.Characters.Find(charId);
                    character.SkillPoint -= skillPoints;

                    await database.SaveChangesAsync();
                    return skillToAdd;
                },
                (result) =>
                {
                    SkillPoint -= dbSkill.SkillPoint;

                    using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                    var skill = Skill.FromDbSkill((DbCharacterSkill)result);
                    Skills.Add(skill);
                    _logger.LogDebug($"Character {Id} learned skill {skill.SkillId} of level {skill.SkillLevel}");

                },
            Id, dbSkill.Id, skillNumber, dbSkill.SkillPoint);

            // Remove previously learned skill.
            if (isSkillLearned != null) Skills.Remove(isSkillLearned);
        }

        /// <summary>
        /// Make character use skill.
        /// </summary>
        /// <param name="skillNumber">unique number of skill; unique is per character(maybe?)</param>
        public (Skill Skill, AttackResult AttackResult) UseSkill(byte skillNumber)
        {
            var skill = Skills.First(s => s.Number == skillNumber);

            Damage damage = new Damage(0, 0, 0);
            // TODO: implement use of all skills.
            // For now, just for testing I'm implementing buff to character.
            if (skill.Type == TypeDetail.Buff && (skill.TargetType == TargetType.Caster || skill.TargetType == TargetType.PartyMembers))
            {
                var buff = AddActiveBuff(skill);
            }
            else
            {
                damage = new Damage(100, 50, 20);
            }

            return (skill, new AttackResult(AttackSuccess.Critical, damage));
        }

        /// <summary>
        /// Usual physical attack, "auto attack".
        /// </summary>
        public AttackResult UsualAttack()
        {
            Damage damage = new Damage(33, 0, 0);
            return new AttackResult(AttackSuccess.Normal, damage);
        }

        #endregion

        #region Buffs

        /// <summary>
        /// Event, that is fired, when player gets new active buff. Maybe we should notify party mmbers?
        /// </summary>
        public event Action<Character, ActiveBuff> OnGotBuff;

        /// <summary>
        /// Active buffs, that increase character characteristic, attack, defense etc.
        /// Don't update it directly, use instead "AddActiveBuff".
        /// </summary>
        public List<ActiveBuff> ActiveBuffs { get; private set; } = new List<ActiveBuff>();

        /// <summary>
        /// Updates collection of active buffs. Also writes changes to database.
        /// </summary>
        /// <param name="skill">skill, that client sends</param>
        /// <returns>Newly added or updated active buff</returns>
        public async Task<ActiveBuff> AddActiveBuff(Skill skill)
        {
            using var database = DependencyContainer.Instance.Resolve<IDatabase>();

            var resetTime = DateTime.UtcNow.AddSeconds(skill.KeepTime);
            var buff = ActiveBuffs.FirstOrDefault(b => b.SkillId == skill.SkillId);
            if (buff != null) // We already have such buff. Try to update reset time.
            {
                if (buff.SkillLevel > skill.SkillLevel)
                {
                    // Do nothing, if character already has higher lvl buff.
                    return buff;
                }
                else
                {
                    // If bufs are the same level, we should only update reset time.
                    if (buff.SkillLevel == skill.SkillLevel)
                    {
                        var dbBuff = database.ActiveBuffs.First(b => b.CharacterId == Id && b.SkillId == skill.Id);
                        dbBuff.ResetTime = resetTime;
                        buff.ResetTime = resetTime;
                        await database.SaveChangesAsync();
                        _logger.LogDebug($"Character {Id} got buff {buff.SkillId} of level {buff.SkillLevel}. Buff will be active next {buff.CountDownInSeconds} seconds");
                    }
                }
            }
            else
            {
                // TODO: find a way to preload skills without calling database each time!
                var dbSkill = database.Skills.FirstOrDefault(s => s.Id == skill.Id);

                // It's a new buff, add it to database.
                var dbBuff = database.ActiveBuffs.Add(new DbCharacterActiveBuff()
                {
                    CharacterId = Id,
                    SkillId = skill.Id,
                    ResetTime = resetTime,
                    Skill = dbSkill
                });
                await database.SaveChangesAsync();

                buff = ActiveBuff.FromDbCharacterActiveBuff(dbBuff.Entity);
                ActiveBuffs.Add(buff);
                _logger.LogDebug($"Character {Id} got buff {buff.SkillId} of level {buff.SkillLevel}. Buff will be active next {buff.CountDownInSeconds} seconds");
            }

            OnGotBuff?.Invoke(this, buff);

            // Notify TCP connection about new buff.
            if (Client != null)
                SendGetBuff(buff);

            return buff;
        }

        #endregion

        #region Inventory

        /// <summary>
        /// Event, that is fired, when some equipment of character changes.
        /// </summary>
        public event Action<Character, Item> OnEquipmentChanged;

        /// <summary>
        /// Collection of inventory items.
        /// </summary>
        public ObservableRangeCollection<Item> InventoryItems { get; private set; } = new ObservableRangeCollection<Item>();

        /// <summary>
        /// Adds item to player's inventory.
        /// </summary>
        /// <param name="itemType">item type</param>
        /// <param name="itemTypeId">item type id</param>
        /// <param name="count">how many items</param>
        public Item AddItemToInventory(Item item)
        {
            // Find free space.
            var free = FindFreeSlotInInventory();

            // Calculated bag slot can not be 0, because 0 means worn item. Newerly created item can not be worn.
            if (free.Bag == 0 || free.Slot == -1)
            {
                return null;
            }

            item.Bag = free.Bag;
            item.Slot = (byte)free.Slot;

            _taskQueue.Enqueue(async (args) =>
                {
                    Item item = (Item)args[0];

                    using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                    var dbItem = new DbCharacterItems()
                    {
                        Type = item.Type,
                        TypeId = item.TypeId,
                        Count = item.Count,
                        Bag = item.Bag,
                        Slot = item.Slot,
                        CharacterId = Id
                    };

                    database.CharacterItems.Add(dbItem);
                    return await database.SaveChangesAsync();
                },
                (obj) => { },
            item.Clone());

            InventoryItems.Add(item);
            _logger.LogDebug($"Character {Id} got item {item.Type} {item.TypeId}");
            return item;
        }

        /// <summary>
        /// Tries to find free slot in inventory.
        /// </summary>
        /// <returns>tuple of bag and slot; slot is -1 if there is no free slot</returns>
        private (byte Bag, int Slot) FindFreeSlotInInventory()
        {
            byte bagSlot = 0;
            int freeSlot = -1;

            if (InventoryItems.Count > 0)
            {
                var maxBag = 5;
                var maxSlots = 24;

                // Go though all bags and try to find any free slot.
                // Start with 1, because 0 is worn items.
                for (byte i = 1; i <= maxBag; i++)
                {
                    var bagItems = InventoryItems.Where(itm => itm.Bag == i).OrderBy(b => b.Slot);
                    for (var j = 0; j < maxSlots; j++)
                    {
                        if (!bagItems.Any(b => b.Slot == j))
                        {
                            freeSlot = j;
                            break;
                        }
                    }

                    if (freeSlot != -1)
                    {
                        bagSlot = i;
                        break;
                    }
                }
            }
            else
            {
                bagSlot = 1; // Start with 1, because 0 is worn items.
                freeSlot = 0;
            }

            return (bagSlot, freeSlot);
        }

        /// <summary>
        /// Removes item from inventory
        /// </summary>
        /// <param name="item">item, that we want to remove</param>
        public Item RemoveItemFromInventory(Item item)
        {
            // If we are giving consumable item.
            if (item.TradeQuantity < item.Count && item.TradeQuantity != 0)
            {
                var clonedItem = item.Clone();
                clonedItem.Count = item.TradeQuantity;

                item.Count -= item.TradeQuantity;
                item.TradeQuantity = 0;

                // TODO: save to database.

                _packetsHelper.SendRemoveItem(Client, clonedItem, false);

                return clonedItem;
            }

            _taskQueue.Enqueue(async (args) =>
                {
                    int charId = (int)args[0];
                    byte bag = (byte)args[1];
                    byte slot = (byte)args[2];

                    using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                    var itemToRemove = database.CharacterItems.First(itm => itm.CharacterId == charId && itm.Bag == bag && itm.Slot == slot);
                    database.CharacterItems.Remove(itemToRemove);
                    return await database.SaveChangesAsync();
                },
                (obj) => { },
            Id, item.Bag, item.Slot);

            InventoryItems.Remove(item);
            _logger.LogDebug($"Character {Id} lost item {item.Type} {item.TypeId}");
            return item;
        }

        /// <summary>
        /// Moves item inside inventory.
        /// </summary>
        /// <param name="currentBag">current bag id</param>
        /// <param name="currentSlot">current slot id</param>
        /// <param name="destinationBag">bag id, where item should be moved</param>
        /// <param name="destinationSlot">slot id, where item should be moved</param>
        /// <returns></returns>
        public async Task<(Item sourceItem, Item destinationItem)> MoveItem(byte currentBag, byte currentSlot, byte destinationBag, byte destinationSlot)
        {
            bool shouldDeleteSourceItemFromDB = false;

            // Find source item.
            var sourceItem = InventoryItems.FirstOrDefault(ci => ci.Bag == currentBag && ci.Slot == currentSlot);

            // Check, if any other item is at destination slot.
            var destinationItem = InventoryItems.FirstOrDefault(ci => ci.Bag == destinationBag && ci.Slot == destinationSlot);
            if (destinationItem is null)
            {
                // No item at destination place.
                // Since there is no destination item we will use source item as destination.
                // The only change, that we need to do is to set new bag and slot.
                destinationItem = sourceItem;
                destinationItem.Bag = destinationBag;
                destinationItem.Slot = destinationSlot;
                shouldDeleteSourceItemFromDB = true;
                sourceItem = new Item() { Bag = currentBag, Slot = currentSlot }; // empty item.
            }
            else
            {
                // There is some item at destination place.
                if (sourceItem.Type == destinationItem.Type && sourceItem.TypeId == destinationItem.TypeId && destinationItem.IsJoinable)
                {
                    // Increase destination item count, if they are joinable.
                    destinationItem.Count += sourceItem.Count;
                    shouldDeleteSourceItemFromDB = true;
                    InventoryItems.Remove(sourceItem);
                    sourceItem = new Item() { Bag = currentBag, Slot = currentSlot }; // empty item.
                }
                else
                {
                    // Swap them.
                    destinationItem.Bag = currentBag;
                    destinationItem.Slot = currentSlot;

                    sourceItem.Bag = destinationBag;
                    sourceItem.Slot = destinationSlot;
                    shouldDeleteSourceItemFromDB = false;
                }
            }

            // Save changes to database.
            using var database = DependencyContainer.Instance.Resolve<IDatabase>();
            var dbItems = database.CharacterItems.Where(ci => ci.CharacterId == Id);
            var dbSourceItem = dbItems.First(itm => itm.Bag == currentBag && itm.Slot == currentSlot);
            var dbDestinationItem = dbItems.FirstOrDefault(itm => itm.Bag == destinationBag && itm.Slot == destinationSlot);

            database.CharacterItems.Remove(dbSourceItem);
            if (dbDestinationItem != null) database.CharacterItems.Remove(dbDestinationItem);

            if (shouldDeleteSourceItemFromDB)
            {
                database.CharacterItems.Add(destinationItem.ToDbItem(Id));
            }
            else
            {
                database.CharacterItems.Add(sourceItem.ToDbItem(Id));
                database.CharacterItems.Add(destinationItem.ToDbItem(Id));
            }

            await database.SaveChangesAsync();

            if (sourceItem.Bag == 0 || destinationItem.Bag == 0)
            {
                var equipmentItem = sourceItem.Bag == 0 ? sourceItem : destinationItem;
                OnEquipmentChanged?.Invoke(this, equipmentItem);

                _logger.LogDebug($"Character {Id} changed equipment on slot {equipmentItem.Slot}");
            }

            return (sourceItem, destinationItem);
        }

        #endregion

        #region Move

        /// <summary>
        /// Event, that is fired, when character changes his/her position.
        /// </summary>
        public event Action<Character> OnPositionChanged;

        public float PosX { get; private set; }
        public float PosY { get; private set; }
        public float PosZ { get; private set; }
        public ushort Angle { get; private set; }

        /// <summary>
        /// Updates player position. Saves change to database if needed.
        /// </summary>
        /// <param name="x">new x</param>
        /// <param name="y">new y</param>
        /// <param name="z">new z</param>
        /// <param name="saveChangesToDB">set it to true, if this change should be saved to database</param>
        public void UpdatePosition(float x, float y, float z, ushort angle, bool saveChangesToDB)
        {
            PosX = x;
            PosY = y;
            PosZ = z;
            Angle = angle;

            _logger.LogDebug($"Character {Id} moved to x={PosX} y={PosY} z={PosZ} angle={Angle}");

            if (saveChangesToDB)
            {
                _taskQueue.Enqueue(async (args) =>
                    {
                        float x = (float)args[0];
                        float y = (float)args[1];
                        float z = (float)args[2];
                        ushort angle = (ushort)args[3];

                        using var database = DependencyContainer.Instance.Resolve<IDatabase>();
                        var dbCharacter = database.Characters.Find(Id);
                        dbCharacter.Angle = angle;
                        dbCharacter.PosX = x;
                        dbCharacter.PosY = y;
                        dbCharacter.PosZ = z;
                        return await database.SaveChangesAsync();
                    },
                (obj) => { },
                x, y, z, angle);
            }

            OnPositionChanged?.Invoke(this);
        }

        #endregion

        #region Target

        /// <summary>
        /// Player fire this event to map in order to get target.
        /// </summary>
        public event Action<Character, int, TargetEntity> OnSeekForTarget;

        private ITargetable _target;
        public ITargetable Target
        {
            get => _target; set
            {
                _target = value;

                if (_target != null)
                {
                    TargetChanged(Target);
                }
            }
        }

        #endregion

        #region Quick skill bar

        /// <summary>
        /// Quick items, i.e. skill bars. Not sure if I need to store it as DbQuickSkillBarItem or need another connector helper class here?
        /// </summary>
        public IEnumerable<DbQuickSkillBarItem> QuickItems;

        #endregion

        #region Trade

        /// <summary>
        /// With whom player is currently trading.
        /// </summary>
        public Character TradePartner;

        /// <summary>
        /// Represents currently open trade window.
        /// </summary>
        public TradeRequest TradeRequest;

        /// <summary>
        /// Otems, that are currently in trade window.
        /// </summary>
        public List<Item> TradeItems = new List<Item>();

        /// <summary>
        /// Money in trade window.
        /// </summary>
        public uint TradeMoney;

        /// <summary>
        /// Money, that belongs to player.
        /// </summary>
        public uint Gold { get; private set; }

        /// <summary>
        /// Changes amount of money.
        /// </summary>
        public void ChangeGold(uint newGold)
        {
            Gold = newGold;

            // TODO: save to database.
        }

        #endregion

        /// <summary>
        /// Creates character from database information.
        /// </summary>
        public static Character FromDbCharacter(DbCharacter dbCharacter, ILogger<Character> logger, IBackgroundTaskQueue taskQueue)
        {
            var character = new Character(logger, taskQueue)
            {
                Id = dbCharacter.Id,
                Name = dbCharacter.Name,
                Level = dbCharacter.Level,
                Map = dbCharacter.Map,
                Race = dbCharacter.Race,
                Class = dbCharacter.Class,
                Mode = dbCharacter.Mode,
                Hair = dbCharacter.Hair,
                Face = dbCharacter.Face,
                Height = dbCharacter.Height,
                Gender = dbCharacter.Gender,
                PosX = dbCharacter.PosX,
                PosY = dbCharacter.PosY,
                PosZ = dbCharacter.PosZ,
                Angle = dbCharacter.Angle,
                StatPoint = dbCharacter.StatPoint,
                SkillPoint = dbCharacter.SkillPoint,
                Strength = dbCharacter.Strength,
                Dexterity = dbCharacter.Dexterity,
                Rec = dbCharacter.Rec,
                Intelligence = dbCharacter.Intelligence,
                Luck = dbCharacter.Luck,
                Wisdom = dbCharacter.Wisdom,
                CurrentHP = dbCharacter.HealthPoints,
                CurrentMP = dbCharacter.StaminaPoints,
                CurrentSP = dbCharacter.ManaPoints,
                Exp = dbCharacter.Exp,
                Gold = dbCharacter.Gold,
                Kills = dbCharacter.Kills,
                Deaths = dbCharacter.Deaths,
                Victories = dbCharacter.Victories,
                Defeats = dbCharacter.Defeats,
                IsAdmin = dbCharacter.User.Authority == 0,
                Country = dbCharacter.User.Faction
            };

            ClearOutdatedValues(dbCharacter);

            character.Skills.AddRange(dbCharacter.Skills.Select(s => Skill.FromDbSkill(s)));
            character.ActiveBuffs.AddRange(dbCharacter.ActiveBuffs.Select(b => ActiveBuff.FromDbCharacterActiveBuff(b)));
            character.InventoryItems.AddRange(dbCharacter.Items.Select(i => Item.FromDbItem(i)));
            character.QuickItems = dbCharacter.QuickItems;

            return character;
        }

        /// <summary>
        ///  TODO: maybe it's better to have db procedure for this?
        ///  For now, we will clear old values, when character is loaded.
        /// </summary>
        private static void ClearOutdatedValues(DbCharacter dbCharacter)
        {
            using var database = DependencyContainer.Instance.Resolve<IDatabase>();
            var outdatedBuffs = dbCharacter.ActiveBuffs.Where(b => b.ResetTime < DateTime.UtcNow);
            database.ActiveBuffs.RemoveRange(outdatedBuffs);

            database.SaveChanges();
        }

    }
}
