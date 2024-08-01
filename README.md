# CS2Warcraft

![00198-3001460831](https://github.com/user-attachments/assets/7aece590-36ba-4084-a4ca-bbb5adfd28dc)

# Warcraft Mod for CS2

An open-source Warcraft mod for CS2 featuring a fully-fledged RPG system, inspired by Roflmuffin's initial Warcraft example plugin.

## Features

The mod introduces seven unique classes:

- Barbarian
- Mage
- Necromancer
- Paladin
- Ranger
- Rogue
- Shapeshifter

You can try out the plugin here: [Connect to Server](steam://connect/136.244.80.208:27015)

## Setup

1. **Install the Plugin**
   - Copy the `WarcraftPlugin` folder to `counterstrikesharp -> plugins`.

2. **Necromancer's Ultimate Setup**
   - Download [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager/releases).
   - Add the following to `multiaddonmanager.cfg`:
     ```
     3168265293 //skeleton pack
     ```
   - Ensure your server configuration includes:
     ```
     mp_autoteambalance 0
     mp_limitteams 0
     ```

3. **Rename Zombie Bots**
   - Copy `botprofile.vpk` to `\game\csgo\overrides`.
   - Add the following line in `gameinfo.gi` under the metamod line:
     ```
     Game csgo/overrides/botprofile.vpk
     ```

## Author Notes

By releasing this mod, I sincerely hope it will aid the community in developing RPG mods and support aspiring plugin authors. This project involved significant effort and the discovery of new techniques. Please consider contributing back to the community. Special thanks to the knowledgeable members on Discord for their invaluable assistance.
