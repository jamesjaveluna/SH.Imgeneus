﻿using Imgeneus.Database.Constants;
using Imgeneus.Network.Packets;
using System;
using System.Diagnostics;
using System.Linq;
using System.Timers;

namespace Imgeneus.World.Game.Player
{
    public partial class Character : ITargetable
    {
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

        #region Damage calculation

        /// <summary>
        /// The time since the last attack. Needed for attack calculations.
        /// </summary>
        private Timer _attackTimer = new Timer();

        /// <summary>
        /// This sync object is used for attack timer calculations.
        /// </summary>
        private object syncObject = new object();

        private int _nextSkillNumber;
        /// <summary>
        /// Client sends next skill number, that he/she wants to use.
        /// We should save this number and use skill based on elapsed time.
        /// </summary>
        public int NextSkillNumber
        {
            get => _nextSkillNumber;
            set
            {
                lock (syncObject)
                {

                    if (!_attackTimer.Enabled)
                    {
                        _attackTimer.Start();
                        _nextSkillNumber = value;

                        if (_nextSkillNumber == 255)
                            AutoAttack();
                        else
                            UseSkill(_nextSkillNumber);
                    }
                    else
                    {
                        _nextSkillNumber = value;
                    }
                }
            }
        }

        /// <summary>
        /// Event, that is fired, when character uses any skill.
        /// </summary>
        public Action<Character, ITargetable, Skill, AttackResult> OnUsedSkill;

        /// <summary>
        /// Make character use skill.
        /// </summary>
        /// <param name="skillNumber">unique number of skill; unique is per character(maybe?)</param>
        private void UseSkill(int skillNumber)
        {
            SendAttackStart();
            _nextSkillNumber = 0;

            // TODO: use dictionary here.
            var skill = Skills.First(s => s.Number == skillNumber);

            Damage damage = new Damage(0, 0, 0);
            // TODO: implement use of all skills.
            // For now, just for testing I'm implementing buff to character.
            if (skill.Type == TypeDetail.Buff && (skill.TargetType == TargetType.Caster || skill.TargetType == TargetType.PartyMembers))
            {
                var buff = AddActiveBuff(skill);
                OnUsedSkill?.Invoke(this, this, skill, new AttackResult(AttackSuccess.Critical, damage));
            }
            else
            {
                damage = new Damage(100, 50, 20);
                OnUsedSkill?.Invoke(this, Target, skill, new AttackResult(AttackSuccess.Critical, damage));
            }
        }

        /// <summary>
        /// Event, that is fired, when character uses auto attack.
        /// </summary>
        public event Action<Character, AttackResult> OnAutoAttack;

        /// <summary>
        /// Usual physical attack, "auto attack".
        /// </summary>
        private void AutoAttack()
        {
            SendAttackStart();
            _nextSkillNumber = 0;

            Damage damage = new Damage(33, 0, 0);
            OnAutoAttack?.Invoke(this, new AttackResult(AttackSuccess.Normal, damage));
        }

        /// <summary>
        /// When time has elapsed, we can use another skill.
        /// </summary>
        private void AttackTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // If there is any pending skill.
            if (_nextSkillNumber != 0)
                if (_nextSkillNumber == 255)
                    AutoAttack();
                else
                    UseSkill(_nextSkillNumber);
            else // No pending skill, stop timer.
                _attackTimer.Stop();
        }

        #endregion
    }
}
