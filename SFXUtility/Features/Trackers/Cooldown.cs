﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Cooldown.cs is part of SFXUtility.

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

namespace SFXUtility.Features.Trackers
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using Classes;
    using LeagueSharp;
    using LeagueSharp.Common;
    using LeagueSharp.CommonEx.Core.Events;
    using Properties;
    using SFXLibrary.IoCContainer;
    using SFXLibrary.Logger;
    using SharpDX;
    using Color = SharpDX.Color;
    using Rectangle = SharpDX.Rectangle;

    #endregion

    internal class Cooldown : Base
    {
        private IEnumerable<CooldownObject> _cooldownObjects = new List<CooldownObject>();
        private Trackers _parent;

        public Cooldown(IContainer container)
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
            get { return "Cooldown"; }
        }

        private void OnLoad(EventArgs args)
        {
            try
            {
                if (IoC.IsRegistered<Trackers>())
                {
                    _parent = IoC.Resolve<Trackers>();
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

        private void OnParentInitialized(object sender, EventArgs eventArgs)
        {
            try
            {
                if (_parent.Menu == null)
                    return;

                Menu = new Menu(Name, BaseName + Name);

                Menu.AddItem(new MenuItem(Name + "EnemyEnabled", "Track Enemy").SetValue(true));
                Menu.AddItem(new MenuItem(Name + "AllyEnabled", "Track Ally").SetValue(true));
                Menu.AddItem(new MenuItem(Name + "Enabled", "Enabled").SetValue(false));

                Menu.Item(Name + "EnemyEnabled").ValueChanged +=
                    delegate(object o, OnValueChangeEventArgs args)
                    {
                        foreach (var cd in _cooldownObjects)
                        {
                            cd.Active =
                                Menu.Item(Name + "Enabled").GetValue<bool>() &&
                                (cd.Hero.IsEnemy && args.GetNewValue<bool>() ||
                                 cd.Hero.IsAlly && Menu.Item(Name + "AllyEnabled").GetValue<bool>());
                        }
                    };
                Menu.Item(Name + "AllyEnabled").ValueChanged += delegate(object o, OnValueChangeEventArgs args)
                {
                    foreach (var cd in _cooldownObjects)
                    {
                        cd.Active =
                            Menu.Item(Name + "Enabled").GetValue<bool>() &&
                            (cd.Hero.IsEnemy && Menu.Item(Name + "EnemyEnabled").GetValue<bool>() ||
                             cd.Hero.IsAlly && args.GetNewValue<bool>());
                    }
                };
                Menu.Item(Name + "Enabled").ValueChanged += delegate(object o, OnValueChangeEventArgs args)
                {
                    foreach (var cd in _cooldownObjects)
                    {
                        cd.Active = args.GetNewValue<bool>() &&
                                    (cd.Hero.IsEnemy && Menu.Item(Name + "EnemyEnabled").GetValue<bool>() ||
                                     cd.Hero.IsAlly && Menu.Item(Name + "AllyEnabled").GetValue<bool>());
                    }
                };

                _parent.Menu.AddSubMenu(Menu);

                _cooldownObjects = HeroManager.AllHeroes.Where(hero => !hero.IsMe)
                    .Select(hero => new CooldownObject(hero, Logger)
                    {
                        Active =
                            Enabled &&
                            (hero.IsEnemy && Menu.Item(Name + "EnemyEnabled").GetValue<bool>() ||
                             hero.IsAlly && Menu.Item(Name + "AllyEnabled").GetValue<bool>())
                    });

                RaiseOnInitialized();
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex) {Object = this});
            }
        }

        private class CooldownObject
        {
            private readonly Render.Sprite _hudSprite;
            private readonly List<Render.Line> _spellLines = new List<Render.Line>();
            private readonly SpellSlot[] _spellSlots = {SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.R};
            private readonly List<Render.Text> _spellTexts = new List<Render.Text>();
            private readonly SpellSlot[] _summonerSpellSlots = {SpellSlot.Q, SpellSlot.W};
            private readonly List<Render.Text> _summonerSpellTexts = new List<Render.Text>();
            private readonly List<Render.Sprite> _summonerSprites = new List<Render.Sprite>();
            public readonly Obj_AI_Hero Hero;
            private bool _active;
            private bool _added;

            public CooldownObject(Obj_AI_Hero hero, ILogger logger)
            {
                Hero = hero;
                try
                {
                    _hudSprite =
                        new Render.Sprite(Resources.CD_Hud, default(Vector2))
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    return Visible;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return false;
                                }
                            },
                            PositionUpdate = delegate
                            {
                                try
                                {
                                    return HpBarPostion;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return default(Vector2);
                                }
                            }
                        };
                }
                catch (Exception ex)
                {
                    logger.AddItem(new LogItem(ex) {Object = this});
                }

                for (var i = 0; i < _summonerSpellSlots.Length; i++)
                {
                    try
                    {
                        var index = i;
                        var spell = Hero.Spellbook.GetSpell(_spellSlots[index]);
                        var summoner =
                            Resources.ResourceManager.GetObject(string.Format("CD_{0}", spell.Name.ToLower())) ??
                            Resources.CD_summonerbarrier;
                        var sprite = new Render.Sprite((Bitmap) summoner, default(Vector2))
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    return Visible;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return false;
                                }
                            }
                        };
                        sprite.PositionUpdate = delegate
                        {
                            try
                            {
                                sprite.Crop(new Rectangle(0,
                                    12*
                                    ((spell.CooldownExpires - Game.Time > 0)
                                        ? (int)
                                            (19*
                                             (1f -
                                              ((Math.Abs(spell.Cooldown) > float.Epsilon)
                                                  ? (spell.CooldownExpires - Game.Time)/spell.Cooldown
                                                  : 1f)))
                                        : 19), 12, 12));
                                return new Vector2(HpBarPostion.X + 3, HpBarPostion.Y + 1 + index*13);
                            }
                            catch (Exception ex)
                            {
                                logger.AddItem(new LogItem(ex) {Object = this});
                                return default(Vector2);
                            }
                        };
                        var text = new Render.Text(default(Vector2), string.Empty, 13, Color.White)
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    return Visible;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return false;
                                }
                            }
                        };
                        text.PositionUpdate =
                            delegate
                            {
                                try
                                {
                                    return new Vector2(HpBarPostion.X - 5 - text.text.Length*5,
                                        HpBarPostion.Y + 1 + 13*index);
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return default(Vector2);
                                }
                            };
                        text.TextUpdate = delegate
                        {
                            try
                            {
                                return spell.CooldownExpires - Game.Time > 0f
                                    ? string.Format(spell.CooldownExpires - Game.Time < 1f ? "{0:0.0}" : "{0:0}",
                                        spell.CooldownExpires - Game.Time)
                                    : string.Empty;
                            }
                            catch (Exception ex)
                            {
                                logger.AddItem(new LogItem(ex) {Object = this});
                                return string.Empty;
                            }
                        };
                        _summonerSprites.Add(sprite);
                        _summonerSpellTexts.Add(text);
                    }
                    catch (Exception ex)
                    {
                        logger.AddItem(new LogItem(ex) {Object = this});
                    }
                }

                for (var i = 0; i < _spellSlots.Length; i++)
                {
                    try
                    {
                        var index = i;
                        var spell = Hero.Spellbook.GetSpell(_spellSlots[index]);
                        var line = new Render.Line(default(Vector2), default(Vector2), 4, Color.Green)
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    return Visible &&
                                           Hero.Spellbook.CanUseSpell(_spellSlots[index]) != SpellState.NotLearned;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return false;
                                }
                            },
                            StartPositionUpdate = delegate
                            {
                                try
                                {
                                    return new Vector2(HpBarPostion.X + 18f + index*27f, HpBarPostion.Y + 20f);
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return default(Vector2);
                                }
                            }
                        };
                        line.EndPositionUpdate =
                            delegate
                            {
                                try
                                {
                                    line.Color = spell.CooldownExpires - Game.Time <= 0f
                                        ? Color.Green
                                        : Color.DeepSkyBlue;
                                    return
                                        new Vector2(
                                            line.Start.X +
                                            ((spell.CooldownExpires - Game.Time > 0f &&
                                              Math.Abs(spell.Cooldown) > float.Epsilon)
                                                ? 1f - ((spell.CooldownExpires - Game.Time)/spell.Cooldown)
                                                : 1f)*23f, line.Start.Y);
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return default(Vector2);
                                }
                            };
                        var text = new Render.Text(default(Vector2), string.Empty, 13, Color.White)
                        {
                            VisibleCondition = delegate
                            {
                                try
                                {
                                    return Visible;
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return false;
                                }
                            }
                        };
                        text.PositionUpdate =
                            delegate
                            {
                                try
                                {
                                    return new Vector2(line.Start.X + (23f - text.text.Length*4)/2, line.Start.Y + 7f);
                                }
                                catch (Exception ex)
                                {
                                    logger.AddItem(new LogItem(ex) {Object = this});
                                    return default(Vector2);
                                }
                            };
                        text.TextUpdate = delegate
                        {
                            try
                            {
                                return spell.CooldownExpires - Game.Time > 0f
                                    ? string.Format(spell.CooldownExpires - Game.Time < 1f ? "{0:0.0}" : "{0:0}",
                                        spell.CooldownExpires - Game.Time)
                                    : string.Empty;
                            }
                            catch (Exception ex)
                            {
                                logger.AddItem(new LogItem(ex) {Object = this});
                                return string.Empty;
                            }
                        };
                        _spellLines.Add(line);
                        _spellTexts.Add(text);
                    }
                    catch (Exception ex)
                    {
                        logger.AddItem(new LogItem(ex) {Object = this});
                    }
                }
            }

            public bool Active
            {
                private get { return _active; }
                set
                {
                    _active = value;
                    Update();
                }
            }

            private Vector2 HpBarPostion
            {
                get { return new Vector2(Hero.HPBarPosition.X + -9, Hero.HPBarPosition.Y + (Hero.IsEnemy ? 17 : 14)); }
            }

            private bool Visible
            {
                get
                {
                    return Active && Hero.IsVisible && !Hero.IsDead && Hero.IsHPBarRendered &&
                           Hero.Position.IsOnScreen() && !ObjectManager.Player.InShop();
                }
            }

            private void Update()
            {
                if (_active && !_added)
                {
                    _hudSprite.Add(0);
                    _summonerSprites.ForEach(sp => sp.Add(1));
                    _spellLines.ForEach(sp => sp.Add(2));
                    _spellTexts.ForEach(sp => sp.Add(3));
                    _summonerSpellTexts.ForEach(sp => sp.Add(3));
                    _added = true;
                }
                else if (!_active && _added)
                {
                    _hudSprite.Remove();
                    _summonerSprites.ForEach(sp => sp.Remove());
                    _spellLines.ForEach(sp => sp.Remove());
                    _spellTexts.ForEach(sp => sp.Remove());
                    _summonerSpellTexts.ForEach(sp => sp.Remove());
                    _added = false;
                }
            }
        }
    }
}