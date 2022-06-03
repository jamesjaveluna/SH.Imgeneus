﻿using System;

namespace Imgeneus.World.Game.Untouchable
{
    public interface IUntouchableManager
    {
        void Init(int ownerId);

        /// <summary>
        /// When true, killable is untouchable and can not be killed.
        /// </summary>
        bool IsUntouchable { get; set; }

        /// <summary>
        /// X next magic attacks will be blocked.
        /// </summary>
        byte BlockedMagicAttacks { get; set; }

        /// <summary>
        /// Event, that is fired when <see cref="BlockedMagicAttacks"/> changes.
        /// </summary>
        event Action<byte> OnBlockedMagicAttacksChanged;
    }
}
