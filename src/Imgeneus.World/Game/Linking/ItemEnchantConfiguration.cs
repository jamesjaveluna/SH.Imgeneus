﻿using Imgeneus.Core.Helpers;
using System.Collections.Generic;

namespace Imgeneus.World.Game.Linking
{
    public class ItemEnchantConfiguration : IItemEnchantConfiguration
    {
        private const string ConfigFile = "config/ItemEnchant.json";

        public static ItemEnchantConfiguration LoadFromConfigFile()
        {
            return ConfigurationHelper.Load<ItemEnchantConfiguration>(ConfigFile);
        }

        public Dictionary<string, uint> LapisianEnchantPercentRate { get; set; }

        public Dictionary<string, uint> LapisianEnchantAddValue { get; set; }
    }
}
