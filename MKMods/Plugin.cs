using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Bindings;

namespace MKMods;

[BepInPlugin("xyz.huestudios.mk.mkmods", "MK Mods", "1.1")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    public static ConfigEntry<bool> alternativeTargeting;
    public static ConfigEntry<bool> nextTargetOrder;
    public static ConfigEntry<float> targetSelectionThreshold;
    public static ConfigEntry<bool> ignoreAlreadySelected;
    public static ConfigEntry<bool> dynamicWeaponSelection;
    public static ConfigEntry<bool> hudNotchLine;

    private void Awake()
    {
        alternativeTargeting = Config.Bind(
            "General",
            "AlternativeTargeting",
            true,
            "Use the alternative target selection algorithm."
        );
        nextTargetOrder = Config.Bind(
            "Target selection",
            "NextTargetOrder",
            false,
            "Select the next target rather than the highest priority one."
        );
        targetSelectionThreshold = Config.Bind(
            "Target selection",
            "TargetSelectionThreshold",
            200f,
            "The maximum distance from the target designator to consider a target."
        );
        ignoreAlreadySelected = Config.Bind(
            "Target selection",
            "IgnoreAlreadySelected",
            false,
            "In NextTargetOrder false, always select the highest priority target even if it is already selected."
        );

        dynamicWeaponSelection = Config.Bind(
            "Weapon selection",
            "DynamicWeaponSelection",
            true,
            "Enable the dynamic weapon selection menu."
        );

        hudNotchLine = Config.Bind(
            "HUD",
            "HudNotchLine",
            true,
            "Display a notch line on the HUD."
        );



        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin mkmods is loaded!");

        var harmony = new Harmony("xyz.huestudios.mk.mkmods");
        harmony.PatchAll();

        Logger.LogInfo($"Patching complete!");
    }
}


[HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
class CombatHUDPatch
{
    static bool Prefix(CombatHUD __instance, bool paint)
    {

        if (!Plugin.alternativeTargeting.Value)
        {
            return true;
        }

        var markers = Traverse.Create(__instance).Field("markers")
            .GetValue<List<HUDUnitMarker>>();

        // That worked, so now we will implement our actual targeting algorithm
        var eligibleTargets = markers
            .Where(marker =>
                // must have the image enabled
                marker.image.enabled
                // must not be excluded by the target list selector
                && !TargetListSelector.i.CheckExclusions(marker.unit)
                // must be within 200 canvas units of the target designator
                && FastMath.Distance(
                    __instance.targetDesignator.gameObject.transform.position,
                    marker.image.transform.position
                ) < Plugin.targetSelectionThreshold.Value
            ).ToList();

        // short circuit if there are no eligible targets
        if (eligibleTargets.Count == 0)
        {
            return false;
        }

        var weaponStation = Traverse.Create(__instance).Field("currentWeaponStation")
            .GetValue<WeaponStation>();

        // Sort them by priority
        eligibleTargets.Sort((a, b) =>
            b.AssessPriority(
                __instance.aircraft,
                __instance.targetDesignator.gameObject,
                weaponStation).CompareTo(
                    a.AssessPriority(
                        __instance.aircraft,
                        __instance.targetDesignator.gameObject,
                        weaponStation)
                ));

        // See what the index of the current target is on the eligible list
        var currentTargetIndex = -1;
        var targetList = Traverse.Create(__instance).Field("targetList")
            .GetValue<List<Unit>>();

        Unit originalTarget = null;

        if (targetList.Count > 0)
        {
            originalTarget = targetList[targetList.Count - 1];
            currentTargetIndex = eligibleTargets.FindIndex(
                marker => marker.unit == targetList[targetList.Count - 1]);
        }

        var nextTargetIndex = -1;

        if (Plugin.nextTargetOrder.Value)
        {
            if (!paint || targetList.Count == 0)
            {
                // The next target will be the next one in the list
                nextTargetIndex = (currentTargetIndex + 1) % eligibleTargets.Count;
            }
            else
            {
                // The next target will be the next one in the list, as long as it 
                // is not already selected

                for (int i = 1; i < eligibleTargets.Count + 1; i++)
                {
                    var potentialTargetIndex = (currentTargetIndex + i)
                        % eligibleTargets.Count;
                    if (!targetList.Contains(
                        eligibleTargets[potentialTargetIndex].unit
                    ))
                    {
                        nextTargetIndex = potentialTargetIndex;
                        break;
                    }
                }
            }
        }
        else
        {
            if ((!paint || targetList.Count == 0) && Plugin.ignoreAlreadySelected.Value)
            {
                // The index will be the highest priority target
                nextTargetIndex = 0;
            }
            else
            {
                // The index will be the highest priority target that is not already 
                // selected
                for (int i = 0; i < eligibleTargets.Count; i++)
                {
                    if (!targetList.Contains(eligibleTargets[i].unit))
                    {
                        nextTargetIndex = i;
                        break;
                    }
                }
            }
        }

        // Debug log next index
        // Plugin.Logger.LogInfo($"Next target index: {nextTargetIndex}");

        // if the new index is still -1, no eligible targets were found
        if (nextTargetIndex == -1)
        {
            return false;
        }

        var selectSound = Traverse.Create(__instance).Field("selectSound")
                    .GetValue<AudioClip>();

        if (!paint || targetList.Count == 0)
        {
            // Deselect the original target
            if (originalTarget != null)
            {
                // if it's only 1, deselect it directly
                if (targetList.Count == 1)
                {
                    __instance.DeSelectUnit(originalTarget);
                }
                else
                {
                    // Deselect everything
                    __instance.DeselectAll(true);
                }
            }
            // Add the new target
            targetList.Add(eligibleTargets[nextTargetIndex].unit);

            if (originalTarget != eligibleTargets[nextTargetIndex].unit)
            {
                // Set the select marker
                eligibleTargets[nextTargetIndex].SelectMarker();
                // Play the target change sound
                SoundManager.PlayInterfaceOneShot(selectSound);
                __instance.aircraft.weaponManager.TargetListChanged();
            }
        }
        else
        {
            // Set the select marker
            eligibleTargets[nextTargetIndex].SelectMarker();
            // We add the target to the list
            targetList.Add(eligibleTargets[nextTargetIndex].unit);
            SoundManager.PlayInterfaceOneShot(selectSound);
            __instance.aircraft.weaponManager.TargetListChanged();
        }

        return false;
    }
}