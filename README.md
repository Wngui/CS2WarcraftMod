<p align="center">
  <img src="https://github.com/user-attachments/assets/4f6fac3a-d098-4a41-8f46-2612e112529a">
</p>

<p align="center">
  <a href="https://www.youtube.com/watch?v=Z9HdF47zPss" target="_blank"><img src="https://img.shields.io/badge/Gameplay-Video-red?style=for-the-badge&logo=youtube" alt="Gameplay Video"></a>
  <a href="https://github.com/Wngui/CS2WarcraftMod/wiki" target="_blank"><img src="https://img.shields.io/badge/Developer-Wiki-blue?style=for-the-badge&logo=github" alt="Developer Wiki"></a>
  <a href="https://discord.gg/VvD8aUHCNW" target="_blank"><img src="https://img.shields.io/badge/Join-Discord-5865F2?style=for-the-badge&logo=discord&logoColor=white" alt="Discord"></a>
</p>

# Warcraft Mod for CS2

An open-source Warcraft mod for CS2 featuring a fully-fledged RPG system, based on Roflmuffin's Warcraft example plugin.
You can try out the plugin here: [Connect](https://cs2browser.com/connect/136.244.80.208:27015)

## Features

Eight unique classes:

- **Barbarian**
- **Mage**
- **Necromancer**
- **Paladin**
- **Ranger**
- **Rogue**
- **Shapeshifter**
- **Tinker**

Each class has 3 passive abilities and an ultimate which is unlocked at max level 16.

![image](https://github.com/user-attachments/assets/3a96b1ba-0173-4b3e-8e2a-43b1ac091247)

Ultimate can be activated by binding it in the console, example
     ```
     bind x ultimate
     ```


## Commands
```!class``` - Change current class

```!skills``` - Opens skill selection menu

```!reset``` - Unassign skill points for current class

```!factoryreset``` - Completely resets progress for current class

```!addxp <amount>``` - Admin only, adds x amount of xp to current class

```!commands``` - Lists all commands

## Class Abilities

### Barbarian

- **Carnage**: Increase damage dealt with shotguns.
- **Battle-Hardened**: Increase your health by 20/40/60/80/100.
- **Throwing Axe**: Chance to hurl an exploding throwing axe when firing.
- **Bloodlust**: Grants infinite ammo, movement speed, and health regeneration. 

### Mage

- **Fireball**: Infuses molotovs with fire magic, causing a huge explosion on impact.
- **Ice Beam**: Chance to freeze enemies in place.
- **Mana Shield**: Passive magical shield, which regenerates armor over time.
- **Teleport**: When you press your ultimate key, you will teleport to the spot you're aiming.

### Necromancer

- **Life Drain**: Harness dark magic to siphon health from foes and restore your own vitality.
- **Poison Cloud**: Infuses smoke grenades with potent toxins, damaging enemies over time.
- **Splintered Soul**: Chance to cheat death with a fraction of vitality.
- **Raise Dead**: Resurrect powerful undead minions to fight alongside you. 

### Paladin

- **Healing Aura**: Emit an aura that gradually heals nearby allies over time.
- **Holy Shield**: Surround yourself with a protective barrier that absorbs incoming damage.
- **Smite**: Infuse your attacks with divine energy, potentially stripping enemy armor.
- **Divine Resurrection**: Instantly revive a random fallen ally.

### Ranger

- **Light Footed**: Nimbly perform a dash in midair, by pressing jump.
- **Ensnare Trap**: Place a trap by throwing a decoy.
- **Marksman**: Additional damage with scoped weapons.
- **Arrowstorm**: Call down a deadly volley of arrows using the ultimate key.

### Rogue

- **Stealth**: Become partially invisible for 1/2/3/4/5 seconds, when killing someone.
- **Sneak Attack**: When you hit an enemy in the back, you do an additional 5/10/15/20/25 damage.
- **Blade Dance**: Increases movement speed and damage with knives.
- **Smokebomb**: When nearing death, you will automatically drop a smokebomb, letting you cheat death. 

### Shapeshifter

- **Adaptive Disguise**: Chance to spawn with an enemy disguise, revealed upon attacking.
- **Doppelganger**: Create a temporary inanimate clone of yourself, using a decoy grenade.
- **Imposter Syndrome**: Chance to be notified when revealed by enemies on radar.
- **Morphling**: Transform into an unassuming object.

### Tinker
- **Attack Drone**: Deploy a drone that attacks nearby enemies.
- **Spare Parts**: Chance to not lose ammo when firing 
- **Spring Trap**: Deploy a trap which launches players into the air.
- **Drone Swarm**: Summon a swarm of attack drones that damage all nearby enemies.

## Setup

**Install the Plugin**
   - Copy the `WarcraftPlugin` folder to `counterstrikesharp -> plugins`.

## Configuration example
Config path: *counterstrikesharp\configs\plugins\WarcraftPlugin\WarcraftPlugin.json*
```jsonc
{
  "ConfigVersion": 3
  "DeactivatedClasses": ["Shapeshifter", "Rogue"], //Disables Shapeshifter & Rogue from the plugin
  "ShowCommandAdverts": true, //Enables adverts teaching new players about available commands
  "DefaultClass": "ranger", //Sets the default class for new players
  "DisableNamePrefix": true //Removes level and class info from player names
}
```

## Author Notes

By releasing this mod, I sincerely hope it will aid the community in developing RPG mods and support aspiring plugin authors. This project involved significant effort and the discovery of new techniques. Please consider contributing back to the community. Special thanks to the knowledgeable members on Discord for their invaluable assistance.

## Credits

Roflmuffin - CounterStrikeSharp & Base plugin</br>
csportalsk - Testing and bug reporting</br>
pZyk - Bug reporting & feedback
