﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Health.cs is part of SFXUtility.

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

namespace SFXUtility.Features.Drawings
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Linq;
    using Classes;
    using LeagueSharp;
    using LeagueSharp.Common;
    using LeagueSharp.CommonEx.Core.Events;
    using SFXLibrary;
    using SFXLibrary.IoCContainer;
    using SFXLibrary.Logger;
    using ObjectHandler = LeagueSharp.CommonEx.Core.ObjectHandler;

    #endregion

    internal class Health : Base
    {
        private IEnumerable<Obj_BarracksDampener> _inhibs = new List<Obj_BarracksDampener>();
        private Drawings _parent;
        private IEnumerable<Obj_AI_Turret> _turrets = new List<Obj_AI_Turret>();

        public Health(IContainer container)
            : base(container)
        {
            Load.OnLoad += OnLoad;
        }

        public override bool Enabled
        {
            get
            {
                return _parent != null && _parent.Enabled && Menu != null &&
                       Menu.Item(Name + "Enabled").GetValue<bool>();
            }
        }

        public override string Name
        {
            get { return "Health"; }
        }

        private void InhibitorHealth()
        {
            if (!Menu.Item(Name + "InhibitorEnabled").GetValue<bool>())
                return;
            foreach (var inhibitor in _inhibs)
            {
                if (inhibitor.IsValid && !inhibitor.IsDead && inhibitor.Health > 0.1f)
                {
                    var percent = ((int) (inhibitor.Health/inhibitor.MaxHealth)*100);
                    Draw.TextCentered(Drawing.WorldToMinimap(inhibitor.Position),
                        Menu.Item(Name + "InhibitorColor").GetValue<Color>(),
                        Menu.Item(Name + "InhibitorPercentage").GetValue<bool>()
                            ? (percent == 0 ? 1 : percent).ToString(CultureInfo.InvariantCulture)
                            : ((int) inhibitor.Health).ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                InhibitorHealth();
                TurretHealth();
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private void OnParentInitialized(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                    return;

                Menu = new Menu(Name, BaseName + Name);

                var inhibitorMenu = new Menu("Inhibitor", Name + "Inhibitor");
                inhibitorMenu.AddItem(new MenuItem(Name + "InhibitorColor", "Color").SetValue(Color.Yellow));
                inhibitorMenu.AddItem(new MenuItem(Name + "InhibitorEnabled", "Enabled").SetValue(false));
                inhibitorMenu.AddItem(new MenuItem(Name + "InhibitorPercentage", "Percentage").SetValue(true));

                var turretMenu = new Menu("Turret", Name + "Turret");
                turretMenu.AddItem(new MenuItem(Name + "TurretColor", "Color").SetValue(Color.Yellow));
                turretMenu.AddItem(new MenuItem(Name + "TurretEnabled", "Enabled").SetValue(false));
                turretMenu.AddItem(new MenuItem(Name + "TurretPercentage", "Percentage").SetValue(true));

                Menu.AddSubMenu(inhibitorMenu);
                Menu.AddSubMenu(turretMenu);

                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(false));

                _parent.Menu.AddSubMenu(Menu);

                _turrets =
                    ObjectHandler.GetFast<Obj_AI_Turret>()
                        .Where(t => t.IsValid && !t.IsDead && t.Health > 0.1f && t.Health < 9999f);
                _inhibs =
                    ObjectHandler.GetFast<Obj_BarracksDampener>().Where(i => i.IsValid && !i.IsDead && i.Health > 0.1f);

                if (!_turrets.Any() || !_inhibs.Any())
                    return;

                HandleEvents(_parent);
                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        protected override void OnEnable()
        {
            Drawing.OnDraw += OnDrawingDraw;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            Drawing.OnDraw -= OnDrawingDraw;
            base.OnDisable();
        }

        private void OnLoad(EventArgs args)
        {
            try
            {
                if (IoC.IsRegistered<Drawings>())
                {
                    _parent = IoC.Resolve<Drawings>();
                    if (_parent.Initialized)
                        OnParentInitialized(null, null);
                    else
                        _parent.OnInitialized += OnParentInitialized;
                }
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private void TurretHealth()
        {
            if (!Menu.Item(Name + "TurretEnabled").GetValue<bool>())
                return;
            foreach (var turret in _turrets)
            {
                if (turret.IsValid && !turret.IsDead && turret.Health > 0f && turret.Health < 9999f)
                {
                    var percent = ((int) (turret.Health/turret.MaxHealth)*100);
                    Draw.TextCentered(Drawing.WorldToMinimap(turret.Position),
                        Menu.Item(Name + "TurretColor").GetValue<Color>(),
                        Menu.Item(Name + "TurretPercentage").GetValue<bool>()
                            ? (percent == 0 ? 1 : percent).ToString(CultureInfo.InvariantCulture)
                            : ((int) turret.Health).ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}