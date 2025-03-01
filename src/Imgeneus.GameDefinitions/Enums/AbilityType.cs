﻿namespace Imgeneus.GameDefinitions.Enums;

public enum AbilityType : byte
{
    None = 0,
    HP = 1,
    MP = 2,
    SP = 3,
    Str = 4,
    Rec = 5,
    Int = 6,
    Wis = 7,
    Dex = 8,
    Luc = 9,
    HPRegeneration = 10,
    MPRegeneration = 11,
    SPRegeneration = 12,
    Unknown1 = 13,
    Unknown2 = 14,
    AttackRange = 15,
    AttackSpeed = 16,
    MoveSpeed = 17,
    CriticalAttackRate = 18,
    SpellCooldownTime = 19,
    PhysicalAttackRate = 20,
    ShootingAttackRate = 21,
    MagicAttackRate = 22,
    PhysicalAttackPower = 23,
    ShootingAttackPower = 24,
    MagicAttackPower = 25,
    PhysicalDefense = 26,
    ShootingDefense = 27,
    MagicResistance = 28,
    PhysicalEvasionRate = 29,
    ShootingEvasionRate = 30,
    MagicEvasionRate = 31, // not sure here?
    DisablePhysicalAttack = 32,
    DisableShootingAttack = 33,
    DisableMagicAttack = 34,
    ExpGainRate = 35,
    Endurance = 36,
    InventoryItemDrop = 37,
    ExpLossRate = 38,
    WarehouseRecall = 39,
    BlueDragonCharm = 40,
    WhiteTigerCharm = 41,
    RedPhoenixCharm = 42,
    WarehouseSize = 43,
    GoldDropRate = 44,
    EquipmentItemDrop = 45,
    Resurrection = 46,
    Unknown3 = 47,
    AbsorptionAura = 48,
    CureOfGoddess = 49,
    Unknown4 = 50,
    Unknown5 = 51,
    IncreaseHPByStr = 52,
    IncreaseHPByRec = 53,
    IncreaseHPByInt = 54,
    IncreaseHPByWis = 55,
    IncreaseHPByDex = 56,
    IncreaseHPByLuc = 57,
    SacrificeHPPercent = 70,
    SacrificeSPPercent = 71,
    SacrificeMPPercent = 72,
    IncreasePhysicalDefenceByPercent = 73,
    IncreaseMagicDefenceByPercent = 78,
    ReduceCastingTime = 89,
    SacrificeStr = 90,
    SacrificeRec = 91,
    SacrificeInt = 92,
    SacrificeWis = 93,
    SacrificeDex = 94,
    SacrificeLuc = 95,
    IncreaseStrBySacrificing = 110,
    IncreaseRecBySacrificing = 111,
    IncreaseIntBySacrificing = 112,
    IncreaseWisBySacrificing = 113,
    IncreaseDexBySacrificing = 114,
    IncreaseLucBySacrificing = 115
}
