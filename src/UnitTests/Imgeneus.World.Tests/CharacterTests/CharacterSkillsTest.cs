﻿using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.Game.Skills;
using Imgeneus.World.Game;
using Imgeneus.World.Game.Attack;
using Imgeneus.World.Game.Inventory;
using Imgeneus.World.Game.PartyAndRaid;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Shape;
using Imgeneus.World.Game.Speed;
using Parsec.Shaiya.Skill;
using System.ComponentModel;
using System.Threading.Tasks;
using Xunit;
using Element = Imgeneus.Database.Constants.Element;

namespace Imgeneus.World.Tests.CharacterTests
{
    public class CharacterSkillsTest : BaseTest
    {
        [Fact]
        [Description("Dispel should clear debuffs.")]
        public void DispelTest()
        {
            var character = CreateCharacter();

            character.BuffsManager.AddBuff(new Skill(Panic_Lvl1, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(Dispel, 0, 0), character, out var _));
            character.SkillsManager.UseSkill(new Skill(Dispel, 0, 0), character);
            Assert.Empty(character.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("With untouchable all attacks should miss.")]
        public void UntouchableTest()
        {
            var character = CreateCharacter();

            var character2 = CreateCharacter();
            character2.AttackManager.AlwaysHit = false;

            var attackSuccess = (character2 as IKiller).AttackManager.AttackSuccessRate(character, TypeAttack.ShootingAttack, new Skill(BullsEye, 0, 0));
            Assert.True(attackSuccess); // Bull eye has 100% success rate.

            // Use untouchable.
            character.BuffsManager.AddBuff(new Skill(Untouchable, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            attackSuccess = (character2 as IKiller).AttackManager.AttackSuccessRate(character, TypeAttack.ShootingAttack, new Skill(BullsEye, 0, 0));
            Assert.False(attackSuccess); // When target is untouchable, bull eye is going to fail.
        }

        [Fact]
        [Description("Archer should miss if fighter used 'FleetFoot' skill.")]
        public void FleetFootTest()
        {
            var fighter = CreateCharacter();
            var archer = CreateCharacter(profession: CharacterProfession.Archer);
            archer.AttackManager.AlwaysHit = false;

            fighter.BuffsManager.AddBuff(new Skill(FleetFoot, 0, 0), null);
            Assert.Single(fighter.BuffsManager.ActiveBuffs);

            var attackSuccess = (archer as IKiller).AttackManager.AttackSuccessRate(fighter, TypeAttack.ShootingAttack);
            Assert.False(attackSuccess);
        }

        [Fact]
        [Description("Transformation should raise shape change event.")]
        public void TransformationTest()
        {
            var character = CreateCharacter();
            var shapeChangeCalled = false;
            character.ShapeManager.OnTranformated += (int sender, bool transformed) => shapeChangeCalled = transformed;

            character.BuffsManager.AddBuff(new Skill(Transformation, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            Assert.True(shapeChangeCalled);
        }

        [Fact]
        [Description("BerserkersRage can be activated and disactivated.")]
        public void BerserkersRageTest()
        {
            var character = CreateCharacter();
            var skill = new Skill(BerserkersRage, 0, 0);
            Assert.True(skill.CanBeActivated);

            character.BuffsManager.AddBuff(skill, null);

            Assert.Single(character.BuffsManager.ActiveBuffs);
            Assert.True(skill.IsActivated);

            character.BuffsManager.AddBuff(skill, null);

            Assert.Empty(character.BuffsManager.ActiveBuffs);
            Assert.False(skill.IsActivated);
        }

        [Fact]
        [Description("Wild Rage should decrease HP and SP.")]
        public void WildRageTest()
        {
            var character1 = CreateCharacter();
            var character2 = CreateCharacter();

            var result = character1.AttackManager.CalculateDamage(character2, TypeAttack.PhysicalAttack, Element.None, 1, 5, 0, 0, new Skill(WildRage, 0, 0));
            Assert.True(result.Damage.HP > WildRage.DamageHP);
            Assert.Equal(result.Damage.SP, WildRage.DamageSP);
        }

        [Fact]
        [Description("Deadly Strike should generate 2 range attacks.")]
        public void DeadlyStrikeTest()
        {
            var character1 = CreateCharacter();
            var character2 = CreateCharacter();

            var numberOfRangeAttacks = 0;
            character1.SkillsManager.OnUsedRangeSkill += (int senderId, IKillable killable, Skill skill, AttackResult res) => numberOfRangeAttacks++;

            character1.SkillsManager.UseSkill(new Skill(DeadlyStrike, 0, 0), character1, character2);

            Assert.Equal(2, numberOfRangeAttacks);
        }

        [Fact]
        [Description("Nettle Sting should generate as many range attacks as many targets it got.")]
        public void NettleStingTest()
        {
            var map = testMap;
            var character1 = CreateCharacter(map: map);
            var character2 = CreateCharacter(map: map, country: Fraction.Dark);
            var character3 = CreateCharacter(map: map, country: Fraction.Dark);
            var character4 = CreateCharacter(map: map, country: Fraction.Dark);

            var numberOfRangeAttacks = 0;
            character1.SkillsManager.OnUsedRangeSkill += (int senderId, IKillable killable, Skill skill, AttackResult res) => numberOfRangeAttacks++;

            var numberOfUsedSkill = 0;
            character1.SkillsManager.OnUsedSkill += (int senderId, IKillable killable, Skill skill, AttackResult res) => numberOfUsedSkill++;

            character1.StatsManager.WeaponMinAttack = 1;
            character1.StatsManager.WeaponMaxAttack = 1;

            var character2GotDamage = false;
            var character3GotDamage = false;
            var character4GotDamage = false;

            character2.HealthManager.OnGotDamage += (int senderId, IKiller character1) => character2GotDamage = true;
            character3.HealthManager.OnGotDamage += (int senderId, IKiller character1) => character3GotDamage = true;
            character4.HealthManager.OnGotDamage += (int senderId, IKiller character1) => character4GotDamage = true;

            character1.SkillsManager.UseSkill(new Skill(NettleSting, 0, 0), character1, character2);

            Assert.Equal(2, numberOfRangeAttacks);
            Assert.Equal(1, numberOfUsedSkill);
            Assert.True(character2GotDamage);
            Assert.True(character3GotDamage);
            Assert.True(character4GotDamage);
        }

        [Fact]
        [Description("Nettle Sting can be used only with Spear.")]
        public void NettleStingNeedsSpearTest()
        {
            var character = CreateCharacter();
            var character2 = CreateCharacter(country: Fraction.Dark);
            character.InventoryManager.AddItem(new Item(databasePreloader.Object, enchantConfig.Object, itemCreateConfig.Object, FireSword.Type, FireSword.TypeId));
            character.InventoryManager.MoveItem(1, 0, 0, 5);

            character.SkillsManager.CanUseSkill(new Skill(NettleSting, 0, 0), character2, out var result);
            Assert.Equal(AttackSuccess.WrongEquipment, result);

            // Give spear.
            character.InventoryManager.AddItem(new Item(databasePreloader.Object, enchantConfig.Object, itemCreateConfig.Object, Spear.Type, Spear.TypeId));
            character.InventoryManager.MoveItem(1, 0, 0, 5);

            character.SkillsManager.CanUseSkill(new Skill(NettleSting, 0, 0), character2, out result);
            Assert.Equal(AttackSuccess.Normal, result);
        }

        [Fact]
        [Description("Eraser should make x2 hp damage and should kill skill owner.")]
        public void EraserTest()
        {
            var character = CreateCharacter();
            var character2 = CreateCharacter(country: Fraction.Dark);

            character.HealthManager.FullRecover();
            Assert.Equal(character.HealthManager.MaxHP, character.HealthManager.CurrentHP);

            var result = character.AttackManager.CalculateAttackResult(new Skill(Eraser, 0, 0), character2, Element.None, 0, 0, 0, 0);
            Assert.Equal(character.HealthManager.CurrentHP * 2, result.Damage.HP);

            character.SkillsManager.UseSkill(new Skill(Eraser, 0, 0), character, character2);
            Assert.True(character.HealthManager.IsDead);
        }

        [Fact]
        [Description("BloodyArc should hit enemy near caster without providing target.")]
        public void BloodyArcTest()
        {
            var map = testMap;
            var character1 = CreateCharacter(map: map);
            character1.StatsManager.WeaponMinAttack = 1;
            character1.StatsManager.WeaponMaxAttack = 1;
            var character2 = CreateCharacter(map: map, country: Fraction.Dark);

            character2.HealthManager.FullRecover();
            Assert.Equal(character2.HealthManager.MaxHP, character2.HealthManager.CurrentHP);

            character1.SkillsManager.UseSkill(new Skill(BloodyArc, 0, 0), character1);

            Assert.True(character2.HealthManager.CurrentHP < character2.HealthManager.MaxHP);
        }

        [Fact]
        [Description("IntervalTraining should decrease str and increase dex.")]
        public void IntervalTrainingTest()
        {
            var character = CreateCharacter();
            character.StatsManager.TrySetStats(str: 50);
            character.StatsManager.ExtraStr = 50;

            Assert.Equal(100, character.StatsManager.TotalStr);
            Assert.Equal(0, character.StatsManager.TotalDex);

            character.BuffsManager.AddBuff(new Skill(IntervalTraining, 0, 0), null);

            Assert.Empty(character.BuffsManager.ActiveBuffs); // IntervalTraining is passive buff.
            Assert.Equal(96, character.StatsManager.TotalStr);
            Assert.Equal(4, character.StatsManager.TotalDex);
        }

        [Fact]
        [Description("MagicVeil should block 3 magic attacks.")]
        public void MagicVeilTest()
        {
            var character = CreateCharacter();
            var character2 = CreateCharacter();
            character2.AttackManager.AlwaysHit = false;

            character.BuffsManager.AddBuff(new Skill(MagicVeil, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            var missed = 0;
            character2.SkillsManager.OnUsedSkill += (int senderId, IKillable target, Skill skill, AttackResult result) =>
            {
                if (result.Success == AttackSuccess.Miss)
                    missed++;
            };

            character2.SkillsManager.UseSkill(new Skill(MagicRoots_Lvl1, 0, 0), character2, character);
            character2.SkillsManager.UseSkill(new Skill(MagicRoots_Lvl1, 0, 0), character2, character);
            character2.SkillsManager.UseSkill(new Skill(MagicRoots_Lvl1, 0, 0), character2, character);

            Assert.Equal(3, missed);
            Assert.Empty(character.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("EtainsEmbrace should require 850 MP.")]
        public void CheckNeedMPTest()
        {
            var character = CreateCharacter();
            Assert.Equal(0, character.HealthManager.CurrentMP);

            var canUse = character.SkillsManager.CanUseSkill(new Skill(EtainsEmbrace, 0, 0), null, out var result);
            Assert.False(canUse);
            Assert.Equal(AttackSuccess.NotEnoughMPSP, result);

            character.HealthManager.ExtraMP += 1000;
            character.HealthManager.FullRecover();

            canUse = character.SkillsManager.CanUseSkill(new Skill(EtainsEmbrace, 0, 0), null, out result);
            Assert.True(canUse);
            Assert.Equal(AttackSuccess.Normal, result);
        }

        [Fact]
        [Description("EtainsEmbrace should block all debuffs.")]
        public void EtainsEmbraceTest()
        {
            var character = CreateCharacter();
            character.SkillsManager.UseSkill(new Skill(EtainsEmbrace, 0, 0), character);

            Assert.Single(character.BuffsManager.ActiveBuffs);

            character.BuffsManager.AddBuff(new Skill(Panic_Lvl1, 0, 0), null);

            Assert.Single(character.BuffsManager.ActiveBuffs); // Panic won't be added.
        }

        [Fact]
        [Description("MagicMirror should mirrow magic damage.")]
        public void MagicMirrorTest()
        {
            var character1 = CreateCharacter();
            var character2 = CreateCharacter();

            character1.HealthManager.FullRecover();
            character2.HealthManager.FullRecover();

            Assert.Equal(character1.HealthManager.MaxHP, character1.HealthManager.CurrentHP);
            Assert.Equal(character2.HealthManager.MaxHP, character2.HealthManager.CurrentHP);

            character1.SkillsManager.UseSkill(new Skill(MagicMirror, 0, 0), character1);
            character2.SkillsManager.UseSkill(new Skill(MagicRoots_Lvl1, 0, 0), character2, character1);

            Assert.Equal(character1.HealthManager.MaxHP, character1.HealthManager.CurrentHP);
            Assert.True(character2.HealthManager.MaxHP > character2.HealthManager.CurrentHP);
        }

        [Fact]
        [Description("PersistBarrier should stop, when player is moving.")]
        public void PersistBarrierShouldStopWhenMovingTest()
        {
            var character1 = CreateCharacter();
            character1.HealthManager.FullRecover();
            character1.SkillsManager.UseSkill(new Skill(PersistBarrier, 0, 0), character1);

            Assert.NotEmpty(character1.BuffsManager.ActiveBuffs);

            character1.MovementManager.PosX += 1;
            character1.MovementManager.RaisePositionChanged();

            Assert.Empty(character1.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("PersistBarrier should stop, when player has not enought mana.")]
        public async Task PersistBarrierShouldStopWhenMPTest()
        {
            var character1 = CreateCharacter();
            character1.HealthManager.IncreaseHP(100);
            character1.HealthManager.CurrentMP = 15;
            character1.HealthManager.CurrentSP = 100;

            character1.SkillsManager.UseSkill(new Skill(PersistBarrier, 0, 0), character1);

            Assert.NotEmpty(character1.BuffsManager.ActiveBuffs);

            await Task.Delay(1100); // Wait ~ 1 sec

            Assert.Equal(1, character1.HealthManager.CurrentMP); // 7% from 200 is 14. 15 - 14 == 1 

            await Task.Delay(1100); // Wait ~ 1 sec

            Assert.Empty(character1.BuffsManager.ActiveBuffs); // Not enough mana, should cancel buff.
        }

        [Fact]
        [Description("PersistBarrier should stop, when player used any other skill.")]
        public void PersistBarrierShouldStopWhenUseOtherSkillTest()
        {
            var character1 = CreateCharacter();
            character1.HealthManager.IncreaseHP(100);
            character1.HealthManager.CurrentMP = 15;
            character1.HealthManager.CurrentSP = 100;

            character1.SkillsManager.UseSkill(new Skill(PersistBarrier, 0, 0), character1);

            Assert.NotEmpty(character1.BuffsManager.ActiveBuffs);

            character1.SkillsManager.UseSkill(new Skill(Dispel, 0, 0), character1);

            Assert.Empty(character1.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("Healing should increase HP.")]
        public void HealingTest()
        {
            var character1 = CreateCharacter();
            var priest = CreateCharacter();
            priest.StatsManager.TrySetStats(wis: 1);

            Assert.Equal(0, character1.HealthManager.CurrentHP);

            var result = new AttackResult();

            priest.SkillsManager.OnUsedSkill += (int senderId, IKillable target, Skill skill, AttackResult res) => result = res;
            priest.SkillsManager.UseSkill(new Skill(Healing, 0, 0), priest, character1);

            Assert.Equal(54, character1.HealthManager.CurrentHP);
            Assert.Equal(AttackSuccess.Normal, result.Success);
            Assert.Equal(54, result.Damage.HP);
        }

        [Fact]
        [Description("Healing Can not be used on opposite faction.")]
        public void HealingCanNotUseOnOppositeFactionTest()
        {
            var character1 = CreateCharacter(country: Fraction.Dark);
            var priest = CreateCharacter();

            var canUse = priest.SkillsManager.CanUseSkill(new Skill(Healing, 0, 0), character1, out var result);
            Assert.False(canUse);
            Assert.Equal(AttackSuccess.WrongTarget, result);
        }

        [Fact]
        [Description("Healing Can not be used on duel opponent.")]
        public void HealingCanNotUseOnDuelOpponentTest()
        {
            var character1 = CreateCharacter();
            var priest = CreateCharacter();

            character1.DuelManager.OpponentId = priest.Id;
            priest.DuelManager.OpponentId = character1.Id;
            character1.DuelManager.Start();
            priest.DuelManager.Start();

            var canUse = priest.SkillsManager.CanUseSkill(new Skill(Healing, 0, 0), character1, out var result);
            Assert.False(canUse);
            Assert.Equal(AttackSuccess.WrongTarget, result);
        }

        [Fact]
        [Description("Healing can be used on self.")]
        public void HealingCanBeUsedSelfTest()
        {
            var priest = CreateCharacter();
            var canUse = priest.SkillsManager.CanUseSkill(new Skill(Healing, 0, 0), priest, out var result);
            Assert.True(canUse);
        }

        [Fact]
        [Description("Healing can be used on self during duel.")]
        public void HealingCanBeUsedSelfDuringDuelTest()
        {
            var priest = CreateCharacter();
            priest.DuelManager.OpponentId = 1;

            var canUse = priest.SkillsManager.CanUseSkill(new Skill(Healing, 0, 0), priest, out var result);
            Assert.True(canUse);
        }

        [Fact]
        [Description("Healing can not be used on dead player.")]
        public void HealingCanNotBeUsedOnDeadPlayerTest()
        {
            var priest = CreateCharacter();
            var character1 = CreateCharacter();

            character1.HealthManager.DecreaseHP(1, CreateCharacter());
            var canUse = priest.SkillsManager.CanUseSkill(new Skill(Healing, 0, 0), character1, out var result);
            Assert.False(canUse);
            Assert.Equal(AttackSuccess.WrongTarget, result);
        }

        [Fact]
        [Description("Frost Barrier can make damage to enemy nearby.")]
        public async void FrostBarrierTest()
        {
            var map = testMap;

            var priest = CreateCharacter(map);
            var character1 = CreateCharacter(map, country: Fraction.Dark);

            Assert.False(character1.HealthManager.IsDead);

            priest.SkillsManager.UseSkill(new Skill(FrostBarrier, 0, 0), priest);

            await Task.Delay(1100); // wait ~1 sec untill Frost Barrier work.

            Assert.True(character1.HealthManager.IsDead);
        }

        [Fact]
        [Description("Frost Barrier can make damage to duel opponent nearby.")]
        public async void FrostBarrierDuelTest()
        {
            var map = testMap;

            var priest = CreateCharacter(map);
            var character1 = CreateCharacter(map);

            Assert.False(character1.HealthManager.IsDead);

            priest.DuelManager.OpponentId = character1.Id;
            priest.DuelManager.Start();

            priest.SkillsManager.UseSkill(new Skill(FrostBarrier, 0, 0), priest);

            await Task.Delay(1100); // wait ~1 sec untill Frost Barrier work.

            Assert.True(character1.HealthManager.IsDead);
        }

        [Fact]
        [Description("Resurrection should resurrect.")]
        public void ResurrectionTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter();

            character.HealthManager.DecreaseHP(1, CreateCharacter());

            Assert.True(character.HealthManager.IsDead);
            Assert.Equal(0, character.HealthManager.CurrentHP);

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(Resurrection, 0, 0), character, out var _));
            priest.SkillsManager.UseSkill(new Skill(Resurrection, 0, 0), priest, character);

            Assert.False(character.HealthManager.IsDead);
            Assert.Equal(100, character.HealthManager.CurrentHP);
        }

        [Fact]
        [Description("Resurrection can not be used on not-dead player.")]
        public void ResurrectionCanNotBeUsedOnNonDeadTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter();

            Assert.False(character.HealthManager.IsDead);
            Assert.False(priest.SkillsManager.CanUseSkill(new Skill(Resurrection, 0, 0), character, out var _));
        }

        [Fact]
        [Description("Resurrection can not be used on opposite country.")]
        public void ResurrectionCanNotBeUsedOnOppositeCountryTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter(country: Fraction.Dark);

            character.HealthManager.DecreaseHP(1, CreateCharacter());

            Assert.True(character.HealthManager.IsDead);
            Assert.Equal(0, character.HealthManager.CurrentHP);

            Assert.False(priest.SkillsManager.CanUseSkill(new Skill(Resurrection, 0, 0), character, out var _));
        }

        [Fact]
        [Description("Hypnosis immobilizes and prevents attack.")]
        public void HypnosisTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter(country: Fraction.Dark);

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(Hypnosis, 0, 0), character, out var _));
            priest.SkillsManager.UseSkill(new Skill(Hypnosis, 0, 0), priest, character);

            Assert.Equal(AttackSpeed.CanNotAttack, character.SpeedManager.TotalAttackSpeed);
            Assert.False(character.SpeedManager.IsAbleToAttack);

            Assert.Equal(MoveSpeed.CanNotMove, character.SpeedManager.TotalMoveSpeed);
            Assert.True(character.SpeedManager.Immobilize);
        }


        [Fact]
        [Description("Hypnosis should stop, when got damage.")]
        public void HypnosisStopOnDamageTest()
        {
            var priest = CreateCharacter();
            priest.StatsManager.WeaponMinAttack = 1;
            priest.StatsManager.WeaponMaxAttack = 1;

            var character = CreateCharacter(country: Fraction.Dark);
            character.HealthManager.FullRecover();

            priest.SkillsManager.UseSkill(new Skill(Hypnosis, 0, 0), priest, character);

            Assert.Single(character.BuffsManager.ActiveBuffs);

            priest.AttackManager.Target = character;
            priest.AttackManager.AutoAttack(priest);

            Assert.Empty(character.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("Evolution changes shape and player can not use any skill.")]
        public void EvolutionTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter();

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(Leadership, 0, 0), null, out var _));

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(Evolution, 0, 0), character, out var _));
            priest.SkillsManager.UseSkill(new Skill(Evolution, 0, 0), priest, character);

            Assert.Equal(ShapeEnum.Fox, character.ShapeManager.Shape);
            Assert.False(character.SkillsManager.CanUseSkill(new Skill(Leadership, 0, 0), null, out var _));
        }

        [Fact]
        [Description("Evolution can not be used on opposite country.")]
        public void EvolutionCanNotBeUsedOnOppositeCountryTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter(country: Fraction.Dark);
            Assert.False(priest.SkillsManager.CanUseSkill(new Skill(Evolution, 0, 0), character, out var _));
        }

        [Fact]
        [Description("Polymorph transforms into pig and pig can not attack or use skills.")]
        public void PolymorphTest()
        {
            var priest = CreateCharacter();
            var character = CreateCharacter(country: Fraction.Dark);

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(Polymorph, 0, 0), character, out var _));
            priest.SkillsManager.UseSkill(new Skill(Polymorph, 0, 0), priest, character);

            Assert.Equal(ShapeEnum.Pig, character.ShapeManager.Shape);
            Assert.False(character.SkillsManager.CanUseSkill(new Skill(Leadership, 0, 0), character, out var _));
            Assert.False(character.AttackManager.CanAttack(IAttackManager.AUTO_ATTACK_NUMBER, priest, out var _));
        }

        [Fact]
        [Description("Polymorph should stop, when get damage.")]
        public void PolymorphStopsOnDamageTest()
        {
            var priest = CreateCharacter();
            priest.StatsManager.WeaponMinAttack = 1;
            priest.StatsManager.WeaponMaxAttack = 1;

            var character = CreateCharacter(country: Fraction.Dark);
            character.HealthManager.FullRecover();

            priest.SkillsManager.UseSkill(new Skill(Polymorph, 0, 0), priest, character);

            Assert.Single(character.BuffsManager.ActiveBuffs);

            priest.AttackManager.Target = character;
            priest.AttackManager.AutoAttack(priest);

            Assert.Empty(character.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("Detection finds player in stealth.")]
        public void DetectionTest()
        {
            var map = testMap;
            var priest = CreateCharacter(map);
            var character = CreateCharacter(map, country: Fraction.Dark);

            character.SkillsManager.UseSkill(new Skill(Stealth, 0, 0), character);
            Assert.True(character.StealthManager.IsStealth);

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(Detection, 0, 0), null, out var _));
            priest.SkillsManager.UseSkill(new Skill(Detection, 0, 0), priest);

            Assert.False(character.StealthManager.IsStealth);
        }

        [Fact]
        [Description("HealingPrayer heals party members.")]
        public void HealingPrayerTest()
        {
            var map = testMap;
            var priest = CreateCharacter(map);
            var character = CreateCharacter(map);

            var party = new Party(packetFactoryMock.Object);
            priest.PartyManager.Party = party;
            character.PartyManager.Party = party;

            var usedSkill = false;
            var usedRangeSkill = false;

            priest.SkillsManager.OnUsedSkill += (int senderId, IKillable target, Skill skill, AttackResult result) =>
            {
                if (target is null && result.Damage.HP == 0)
                    usedSkill = true;
            };

            priest.SkillsManager.OnUsedRangeSkill += (int senderId, IKillable target, Skill skill, AttackResult result) =>
            {
                if (target == character)
                    usedRangeSkill = true;
            };

            Assert.True(priest.SkillsManager.CanUseSkill(new Skill(HealingPrayer, 0, 0), null, out var _));
            priest.SkillsManager.UseSkill(new Skill(HealingPrayer, 0, 0), priest);

            Assert.True(usedSkill);
            Assert.True(usedRangeSkill);
            Assert.True(character.HealthManager.CurrentHP > 0);
        }

        [Fact]
        [Description("TransformationAssassin should turn into chicken if no mob is selected.")]
        public void TransformationAssassinIntoChickenTest()
        {
            var character = CreateCharacter();

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(TransformationAssassin, 0, 0), character, out var _));
            character.SkillsManager.UseSkill(new Skill(TransformationAssassin, 0, 0), character);

            Assert.Equal(ShapeEnum.Chicken, character.ShapeManager.Shape);
        }

        [Fact]
        [Description("TransformationAssassin should turn into mob if mob is selected.")]
        public void TransformationAssassinIntoMobTest()
        {
            var map = testMap;
            var character = CreateCharacter(map);
            var mob = CreateMob(Wolf.Id, map);

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(TransformationAssassin, 0, 0), mob, out var _));
            character.SkillsManager.UseSkill(new Skill(TransformationAssassin, 0, 0), character, mob);

            Assert.Equal(ShapeEnum.Mob, character.ShapeManager.Shape);
            Assert.Equal(mob.MobId, character.ShapeManager.MobId);
        }

        [Fact]
        [Description("Disguise should turn into opposite faction.")]
        public void DisguiseTest()
        {
            var character = CreateCharacter();

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(Disguise, 0, 0), character, out var _));
            character.SkillsManager.UseSkill(new Skill(Disguise, 0, 0), character);

            Assert.Equal(ShapeEnum.OppositeCountry, character.ShapeManager.Shape);
        }

        [Fact]
        [Description("Disguise should turn into opposite faction character if character is selected.")]
        public void DisguiseIntoCharacterTest()
        {
            var character = CreateCharacter();
            var character2 = CreateCharacter(country: Fraction.Dark);

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(Disguise, 0, 0), character2, out var _));
            character.SkillsManager.UseSkill(new Skill(Disguise, 0, 0), character, character2);

            Assert.Equal(ShapeEnum.OppositeCountryCharacter, character.ShapeManager.Shape);
            Assert.Equal(character2.Id, character.ShapeManager.CharacterId);
        }

        [Fact]
        [Description("Misfortune should decrease luc and dex.")]
        public void MisfortuneTest()
        {
            var character = CreateCharacter();
            character.StatsManager.ExtraDex = 50;
            character.StatsManager.ExtraLuc = 50;

            Assert.Equal(50, character.StatsManager.TotalDex);
            Assert.Equal(50, character.StatsManager.TotalLuc);

            var character2 = CreateCharacter(country: Fraction.Dark);
            Assert.True(character2.SkillsManager.CanUseSkill(new Skill(Misfortune, 0, 0), character, out var _));

            character2.SkillsManager.UseSkill(new Skill(Misfortune, 0, 0), character2, character);

            Assert.Equal(0, character.StatsManager.TotalDex);
            Assert.Equal(0, character.StatsManager.TotalLuc);
        }

        [Fact]
        [Description("StunSlam should prevent any attack.")]
        public void StunSlamTest()
        {
            var character = CreateCharacter();
            character.HealthManager.FullRecover();

            Assert.True(character.SkillsManager.CanUseSkill(new Skill(Leadership, 0, 0), null, out var _));

            var character2 = CreateCharacter();
            character2.SkillsManager.UseSkill(new Skill(StunSlam, 0, 0), character2, character);

            Assert.False(character.SkillsManager.CanUseSkill(new Skill(Leadership, 0, 0), null, out var _));
        }

        [Fact]
        [Description("DeathTouch should damage 65% of HP.")]
        public void DeathTouchTest()
        {
            var character = CreateCharacter();
            character.HealthManager.FullRecover();
            Assert.Equal(100, character.HealthManager.CurrentHP);

            var character2 = CreateCharacter();
            character2.SkillsManager.UseSkill(new Skill(DeathTouch, 0, 0), character2, character);

            Assert.Equal(35, character.HealthManager.CurrentHP);
        }

        [Fact]
        [Description("PhantomAssault should make damage and sleep debuff.")]
        public void PhantomAssaultTest()
        {
            var map = testMap;
            var character = CreateCharacter(map);
            character.HealthManager.FullRecover();

            var character2 = CreateCharacter(map, country: Fraction.Dark);
            character2.StatsManager.WeaponMinAttack = 1;
            character2.StatsManager.WeaponMaxAttack = 1;
            character2.SkillsManager.UseSkill(new Skill(PhantomAssault, 0, 0), character2, character);

            Assert.True(character.HealthManager.CurrentHP < character.HealthManager.MaxHP);
            Assert.NotEmpty(character.BuffsManager.ActiveBuffs);
        }
    }
}
