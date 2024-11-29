using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MKMods;

[BepInPlugin("xyz.huestudios.mk.mkmods", "MK Mods", "1.0")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private void Awake()
    {
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

        var markers = Traverse.Create(__instance).Field("markers")
            .GetValue<List<HUDUnitMarker>>();

        // Compute distance between the target designator and each hud marker
        // Plugin.Logger.LogInfo("Computing distances");

        //List<float> distances = new List<float>();
        //foreach (var marker in markers)
        //{
        //    try
        //    {
        //        var distance = FastMath.Distance(
        //            __instance.targetDesignator.gameObject.transform.position,
        //            marker.image.transform.position
        //        );
        //        distances.Add(distance);
        //    }
        //    catch (System.Exception e)
        //    {
        //        distances.Add(float.PositiveInfinity);
        //        Plugin.Logger.LogError(e);
        //    }
        //}

        //Plugin.Logger.LogInfo("Distances computed");

        // Find the index of the closest marker
        //var smallest = float.PositiveInfinity;
        //var smallestIndex = -1;
        //for (int i = 0; i < distances.Count; i++)
        //{
        //    if (distances[i] < smallest)
        //    {
        //        smallest = distances[i];
        //        smallestIndex = i;
        //    }
        //}

        // Log closest marker
        //Plugin.Logger.LogInfo($"Closest marker: {smallestIndex} at {smallest}");

        //Traverse.Create(__instance).Field("targetList")
        //    .SetValue(new List<Unit> { markers[smallestIndex].unit });

        //Plugin.Logger.LogInfo("CombatHUD.TargetSelect");


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
                ) < 200
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
                var potentialTargetIndex = (currentTargetIndex + i) % eligibleTargets.Count;
                if (!targetList.Contains(eligibleTargets[potentialTargetIndex].unit))
                {
                    nextTargetIndex = potentialTargetIndex;
                    break;
                }
            }
        }

        // Debug log next index
        Plugin.Logger.LogInfo($"Next target index: {nextTargetIndex}");

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