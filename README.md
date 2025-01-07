# MKMods

Original Credit goes to MK, these are just my edits i do.

Implements various client-side improvements for the game 
[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).

## Features

### Improved target selection algorithm 

Tapping the targeting key will unselect all existing targets and switch to the 
next highest priority target. Holding the targeting key will add the next highest
priority target to the selected targets, without removing existing targets.

The targeting radius has also been increased from 100 to 200 canvas units.

In simpler terms, this system prioritizes quickly selecting new individual targets
in a logical way. It's similar to the targeting system of 
[Arma 3](https://store.steampowered.com/agecheck/app/107410).

### Dynamic loadout selection

The plane/loadout selection menu now shows a the weapon selection dropdown as a
floating UI item over each hardpoint, as well as a line pointing to it. This allows
for easier visualization of where each weapon is mounted onto the plane.

![Dynamic loadout selection example](https://github.com/mkualquiera/MKModsNO/blob/main/images/dls.png?raw=true)

### HUD notch line

Displays a red line on the HUD that correspond to the notching direction for radar
missiles.

![HUD notch line example](https://github.com/mkualquiera/MKModsNO/blob/main/images/notchline.png?raw=true)

### Audible missile warnings

Plays a warning sound when a missile is locked onto the player's plane. The specific 
sound played corresponds to the countermeasure to the type of missile.

- IR: "Flare"
- ARH, SARH: "Notch"
- ARM: "Radar"
- Optical: "Hide"

### Fuel time and low fuel warning

Displays the remaining fuel time in the HUD, plays a "low fuel" warning sound
when the fuel is below 7 minutes, and a "bingo fuel" warning sound when the fuel
is below 3 minute.

![Fuel time example](https://github.com/mkualquiera/MKModsNO/blob/main/images/fueltime.png?raw=true)

### More

Other stuff is coming soon, like improved cockpit functionality.

## How to install

- Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#where-to-download-bepinex).
- Download the latest release from the [releases page](https://github.com/mkualquiera/MKModsNO/releases).
- Unzip the download into the BepInEx ``plugin`` folder (located in 
``Nuclear Option/BepInEx/plugins``).
- Run the game.

### Configs

This mod supports various config settings like disabling each feature and finetuning
some values. Use the [BepInEx configuration manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) 
to modify these. 

## How to build

Building was done in nixos using the provided ``shell.nix`` using the ``dotnet build``
 command. If you're reading this section you probably know how to build dotnet 
projects anyway.

Make sure to change the game path in ``MKMods.csproj``.

