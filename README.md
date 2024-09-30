<p align="center">
  <img src="https://github.com/user-attachments/assets/7aece590-36ba-4084-a4ca-bbb5adfd28dc" alt="00198-3001460831" width="40%">
</p>
<p align="center">
  (https://www.youtube.com/watch?v=Z9HdF47zPss)
</p>

# Warcraft Mod for CS2

An open-source Warcraft mod for CS2 featuring a fully-fledged RPG system, inspired by Roflmuffin's initial Warcraft example plugin.

## Features

The mod introduces seven unique classes:

- **Barbarian**
- **Mage**
- **Necromancer**
- **Paladin**
- **Ranger**
- **Rogue**
- **Shapeshifter**

You can try out the plugin here: [Connect](https://cs2browser.com/connect/136.244.80.208:27015)

Each class has 3 passive abilities and an ultimate which is unlocked at max level 16.
Ultimate can be activated by binding it in the console, example
     ```
     bind x ultimate
     ```

## Commands
```!class``` - Change current class

```!skills``` - Opens skill selection menu

```!reset``` - Unassign skill points for current class

```!factoryreset``` - Completely resets all your progress on all classes

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

1. **Install the Plugin**
   - Copy the `WarcraftPlugin` folder to `counterstrikesharp -> plugins`.

2. **Necromancer's Ultimate Setup**

   *These steps are not stricly nessesary to get the plugin working, but the necromancers ult may not work correctly without them.*
   - Download [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager/releases).
   - Add the following to `multiaddonmanager.cfg`:
     ```
     3168265293 //skeleton pack
     ```
   - Ensure your server configuration includes:
     ```
     mp_autoteambalance 0
     mp_limitteams 0
     bot_difficulty 4 // 4 or less to avoid spawning 'zombie' bots
     ```
   - Rename Zombie Bots
     - Copy `botprofile.vpk` to `\game\csgo\overrides`.
     - Add the following line in `gameinfo.gi` under the metamod line:
       ```
       Game csgo/overrides/botprofile.vpk
       ```

## Configuration example
Config path: *counterstrikesharp\configs\plugins\WarcraftPlugin\WarcraftPlugin.json*
```json
{
  "DeactivatedClasses": ["Shapeshifter", "Rogue"], //Disables Shapeshifter & Rogue from the plugin
  "ShowCommandAdverts": true, //Enables adverts teaching new players about available commands
  "NecromancerUseZombieModel": false, //Disable Necromancer custom zombie model (disabling need for multiaddon manager)
  "ConfigVersion": 2
}
```

## Author Notes

By releasing this mod, I sincerely hope it will aid the community in developing RPG mods and support aspiring plugin authors. This project involved significant effort and the discovery of new techniques. Please consider contributing back to the community. Special thanks to the knowledgeable members on Discord for their invaluable assistance.
