# MKModsNO

Implements various client-side improvements for the game 
[Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).

## Features

### Improved targeting algorithm 

Tapping the targeting key will unselect all existing targets and switch to the 
next highest priority target. Holding the targeting key will add the next highest
priority target to the selected targets, without removing existing targets.

The targeting radius has also been increased from 100 to 200 canvas units.

In simpler terms, this system prioritizes quickly selecting new individual targets
in a logical way. It's similar to the targeting system of 
[Arma 3](https://store.steampowered.com/agecheck/app/107410).

## How to install

- Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html#where-to-download-bepinex).
- Copy ``MKMods.dll`` into the BepInEx ``plugin`` folder (located in 
``Nuclear Option/BepInEx/plugins).
- Run the game.

## How to build

Building was done in nixos using the provided ``shell.nix`` using the ``dotnet build``
 command. If you're reading this section you probably know how to build dotnet 
projects anyway.

Make sure to change the game path in ``MKMods.csproj``.