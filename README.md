# Download latest version:
https://github.com/lilwhitemouse/RimWorld-LWM.DeepStorage/releases

# LWM's DeepStorage - a mod for RimWorld

## Deep Storage Units, yet another storage solution (that will likely be [WIP] for some time).

LWM presents yet another way to deal with your storage needs:  Deep Storage units!

Currently available are multiple buildings that can store more than one object at a time.  Inspired by Skullywag's Extended Storage, this mod takes a different approach - storage buildings that pawns can simply carry multiple items to.  An approach that got a lot harder with 1.0, BTW, but I like the results so far.

This mod will be updating as I tweak items and fix bugs, but I wanted to make it available now that it's mostly working.

Pallets, clothing racks, food storage trays, etc.  You can also write your own &lt;ThingDef&gt;s if you don't like what I've done.

## Requirements
 * HugsLib (load it first, as usual) https://steamcommunity.com/sharedfiles/filedetails/?id=818773962 or on github, etc

## Installation
 * Put the LWM.DeepStorage folder (under _Mod) inside your game's Mod/ folder.  Update often?
 
## Note on Building the Project Yourself
The build project is designed for use on Linux in my personal laptop.  If you want to build the project yourself, you will need to - at a minumum:
 1. Ensure the Requirements/References point to your correct locations for the RimWorld Assembly-CSharp and UnityEngine dlls.
 2. Ensure the nupkg requirement for HugsLib is working
 3. Change the Custom Commands for your system.
 * I am using monodevelop 7.7 and msbuild on the Debian-based SolydX flavour of Linux.
 * All of these details are located in the .csproj file, or you can use monodevelop's UI to change these settings.

## Deep Storage Units
 * Big Shelf - a shelf with enough space to store two items per cell
 * Medicine Cabinet - what it says on the box
 * Meal Tray Racks - efficient storage for meals.  Or desserts.
 * Food Baskets - store raw food, some plant matter, or pile in drugs
 * Meat Hooks - I think you can figure out what these are for
 * Weapon Lockers - currently requires Machining? Efficiently store weapons!
 * Clothing Rack - currently stores both civilian and military clothing - subject to change
 * Pallet - for piling so many things onto!
 * Pallet with Wrapping - can store loose matter, too!
 * Skips - Are you American?  Did you know "Dumpster" is a registered TM?  Rock chunks, heavy resournces

Using Deep Storage - bonus tip:  If you have selected an item in DeepStorage, if you right-click, you jump the storage unit!

## Known Bugs
 * It may be possible to end up with a small amount of free storage from time to time.  (An extra partially filled stack)

## Planned/Likely Changes
 * Possible more options
 * Possible changes to how much/what kinds of stuff is allowed in various units
 * Possible more units

## Compatibility (load Deep Storage after these)
 * Combat Extended - Weapon Lockers can store a maximum total Bulk (Sumghai)
 * RimWorld Search Agency (Hauling Hysteresis): hysteresis disabled for DSUs

## Known To Be Compatible With
 * Common Sense
 * Pick Up and Haul

## Uncompatibility - or - Strange Bugs?
 * Likely uncompatible with other storage solutions that pile lots of things in one place (extended storage, RT_Shelves, ???)
 * I f***ed with the Selector and some mesh Drawing.  It's possible, altho unlikely, this may cause an incompatibility

The code can be found online at: https://github.com/lilwhitemouse/RimWorld-LWM.DeepStorage

On Steam: https://steamcommunity.com/id/littlewhitemouse/myworkshopfiles/?appid=294100

In the Ludeon Forum: https://ludeon.com/forums/index.php?topic=47707.0



Most images used with permission from Skullywag. (Thanks!)

Weapons Cabinets and Lockers are sumghai's. (Also thanks!)

Meat Hooks are (c) LWM.

## License
Almost all code (c) LWM.

Also (currently) includes some code from Ratysz, allowing right-click to select actions on items, which Sumghai had permission to use (also GPL).  Thanks!

Released under GPL 3.0.

All of LWM's code (and sumghai's additions) are also released under LGPL, because I think that the LGPL is the license we should actually be using for mods.  Not that anyone is likely to complain, but altho we have a stupid copyright system, we should still use it correctly.

All of LWM's code (and sumghai's) are also available to Ludeon Studios, should they be incorporated into the base RimWorld game.

Thanks to Marnador for the RimWorld font.  Thanks to Pardeike for Harmony.  Awesome.

Ludeon Studios (the people who make RimWorld) require this: "Portions of the materials used to create this content/mod are trademarks and/or copyrighted works of Ludeon Studios Inc. All rights reserved by Ludeon. This content/mod is not official and is not endorsed by Ludeon." In case you were under any misapprehension. As they sometimes say in books, credit to Ludeon for RW, all mistakes are most certainly my own.
