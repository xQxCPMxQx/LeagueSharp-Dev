﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 TurnAround.cs is part of SFXUtility.

 SFXUtility is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXUtility is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXUtility. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXLibrary.Extensions.NET;
using SFXLibrary.Logger;
using SFXUtility.Classes;
using SharpDX;

#endregion

namespace SFXUtility.Features.Others
{
    internal class TurnAround : Base
    {
        private readonly List<SpellInfo> _spellInfos = new List<SpellInfo>
        {
            new SpellInfo("Cassiopeia", "CassiopeiaPetrifyingGaze", 1000f, false, true, 0.65f),
            new SpellInfo("Tryndamere", "MockingShout", 900f, false, false, 0.65f)
        };

        private float _blockMovementTime;
        private Vector3 _lastMove;
        private Others _parent;

        public override bool Enabled
        {
            get
            {
                return !Unloaded && _parent != null && _parent.Enabled && Menu != null &&
                       Menu.Item(Name + "Enabled").GetValue<bool>();
            }
        }

        public override string Name
        {
            get { return Global.Lang.Get("F_TurnAround"); }
        }

        protected override void OnEnable()
        {
            Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
            Obj_AI_Base.OnIssueOrder += OnObjAiBaseIssueOrder;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Obj_AI_Base.OnProcessSpellCast -= OnObjAiBaseProcessSpellCast;
            Obj_AI_Base.OnIssueOrder -= OnObjAiBaseIssueOrder;
            base.OnDisable();
        }

        private void OnObjAiBaseIssueOrder(Obj_AI_Base sender, GameObjectIssueOrderEventArgs args)
        {
            try
            {
                if (sender.IsMe && args.Order == GameObjectOrder.MoveTo || args.Order == GameObjectOrder.AttackTo)
                {
                    _lastMove = args.TargetPosition;
                    if (_blockMovementTime > Game.Time)
                    {
                        args.Process = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (sender == null || !sender.IsValid || sender.Team == ObjectManager.Player.Team ||
                    ObjectManager.Player.IsDead || !ObjectManager.Player.IsTargetable)
                {
                    return;
                }
                var spellInfo =
                    _spellInfos.FirstOrDefault(
                        i => args.SData.Name.Contains(i.Name, StringComparison.OrdinalIgnoreCase));
                if (spellInfo != null)
                {
                    if ((spellInfo.Target && args.Target == ObjectManager.Player) ||
                        (ObjectManager.Player.Distance(sender.ServerPosition) + ObjectManager.Player.BoundingRadius) <=
                        spellInfo.Range)
                    {
                        var moveTo = _lastMove;
                        ObjectManager.Player.IssueOrder(
                            GameObjectOrder.MoveTo,
                            sender.ServerPosition.Extend(
                                ObjectManager.Player.ServerPosition,
                                ObjectManager.Player.ServerPosition.Distance(sender.ServerPosition) +
                                (spellInfo.TurnOpposite ? 100 : -100)));
                        Utility.DelayAction.Add(250, () => Game.SendEmote(Emote.Laugh));
                        _blockMovementTime = Game.Time + spellInfo.CastTime;
                        Utility.DelayAction.Add(
                            (int) ((spellInfo.CastTime + 0.1) * 1000),
                            () => ObjectManager.Player.IssueOrder(GameObjectOrder.MoveTo, moveTo));
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void OnGameLoad(EventArgs args)
        {
            try
            {
                if (Global.IoC.IsRegistered<Others>())
                {
                    _parent = Global.IoC.Resolve<Others>();
                    if (_parent.Initialized)
                    {
                        OnParentInitialized(null, null);
                    }
                    else
                    {
                        _parent.OnInitialized += OnParentInitialized;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnParentInitialized(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                {
                    return;
                }

                Menu = new Menu(Name, Name);

                Menu.AddItem(new MenuItem(Name + "Enabled", Global.Lang.Get("G_Enabled")).SetValue(false));

                _parent.Menu.AddSubMenu(Menu);

                if (!HeroManager.Enemies.Any(h => _spellInfos.Any(i => i.Owner == h.ChampionName)))
                {
                    return;
                }

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private class SpellInfo
        {
            public SpellInfo(string owner, string name, float range, bool target, bool turnOpposite, float castTime)
            {
                Owner = owner;
                Name = name;
                Range = range;
                Target = target;
                TurnOpposite = turnOpposite;
                CastTime = castTime;
            }

            public string Name { get; private set; }
            public string Owner { get; private set; }
            public float Range { get; private set; }
            public bool Target { get; private set; }
            public bool TurnOpposite { get; private set; }
            public float CastTime { get; private set; }
        }
    }
}