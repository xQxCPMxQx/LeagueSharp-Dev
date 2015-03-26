﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Timers.cs is part of SFXUtility.

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

namespace SFXUtility.Features.Timers
{
    #region

    using System;
    using Classes;
    using LeagueSharp.Common;
    using LeagueSharp.CommonEx.Core.Events;
    using SFXLibrary.IoCContainer;
    using SFXLibrary.Logger;

    #endregion

    internal class Timers : Base
    {
        public Timers(IContainer container)
            : base(container)
        {
            Load.OnLoad += OnLoad;
        }

        public override bool Enabled
        {
            get { return Menu != null && Menu.Item(Name + "Enabled").GetValue<bool>(); }
        }

        public override string Name
        {
            get { return "Timers"; }
        }

        private void OnLoad(EventArgs args)
        {
            try
            {
                Menu = new Menu(Name, BaseName + Name);

                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(false));

                BaseMenu.AddSubMenu(Menu);

                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }
    }
}