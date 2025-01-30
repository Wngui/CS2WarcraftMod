using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Classes
{
    public class MonkeyDude : WarcraftClass
    {
        public override string DisplayName => "MonkeyMan";

        public override Color DefaultColor => Color.GreenYellow;

        public override List<IWarcraftAbility> Abilities =>
        [
            new WarcraftAbility("Banana Gun", "Shoots a barrage of bananas that explode on impact."),
            new WarcraftAbility("Monkey Agility", "Increases movement speed and evasion."),
            new WarcraftAbility("Primal Roar", "Emits a roar when killing an enemy that stuns nearby enemies."),
            new WarcraftCooldownAbility("Jungle Fury", "Temporarily increases attack speed and damage.", 60f)
        ];

        public override void Register()
        {
            HookEvent<EventPlayerSpawn>(PlayerSpawn);

            HookAbility(3, Ultimate);
        }

        private void PlayerSpawn(EventPlayerSpawn spawn)
        {
            Console.WriteLine("MonkeyMan has spawned!");
        }

        private void Ultimate()
        {
            Console.WriteLine("MonkeyMan used ultimate!");
        }
    }
}
