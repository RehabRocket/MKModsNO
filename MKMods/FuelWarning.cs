

using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Bindings;
using UnityEngine.UI;

namespace MKMods;


class FuelWarningGlobals
{
    public static AudioClip fuelWarningClip;
    public static AudioClip bingoFuelClip;

    public static float lastReading = 0;

    public static float lowFuelTime = 60f * 5f;
    public static float bingoFuelTime = 60f * 1f;

    public static float lastFuelAmount = 1f;

    public static float updateRate = 10f;

    public static GameObject fuelTimeLabel;

    public static bool initialized = false;

    public static void Initialize()
    {
        if (!Plugin.fuelWarnings.Value)
        {
            return;
        }

        lowFuelTime = Plugin.fuelWarningMinutes.Value * 60f;
        bingoFuelTime = Plugin.bingoFuelMinutes.Value * 60f;

        updateRate = Plugin.fuelWarningUpdateRate.Value;

        fuelWarningClip = AudioLoading.LoadAudio("fuel low.mp3");
        bingoFuelClip = AudioLoading.LoadAudio("bingo fuel.mp3");
    }
}

[HarmonyPatch(typeof(FuelGauge), "Refresh")]
public class FuelWarningRefreshPatch
{
    public static void Postfix(FuelGauge __instance)
    {
        if (!Plugin.fuelWarnings.Value)
        {
            return;
        }

        if (!FuelWarningGlobals.initialized)
        {
            // See if we already have a child called fuelTime
            GameObject fuelTimeObj = null;

            foreach (Transform child in __instance.transform)
            {
                if (child.name == "fuelTime")
                {
                    fuelTimeObj = child.gameObject;
                    break;
                }
            }

            // If we don't have it, clone fuelLabel
            if (fuelTimeObj == null)
            {
                GameObject fuelLabel = null;
                foreach (Transform child in __instance.transform)
                {
                    if (child.name == "fuelLabel")
                    {
                        fuelLabel = child.gameObject;
                        break;
                    }
                }

                fuelTimeObj = GameObject.Instantiate(fuelLabel, __instance.transform);

                fuelTimeObj.name = "fuelTime";
                // Move it down a bit
                var currentPos = fuelTimeObj.GetComponent<RectTransform>().anchoredPosition;
                fuelTimeObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(
                    currentPos.x,
                    currentPos.y - 20
                );
            }

            //FuelWarningGlobals.fuelTimeLabel.GetComponent<Text>().text = "(...)";

            FuelWarningGlobals.fuelTimeLabel = fuelTimeObj;
        }

        if (Time.timeSinceLevelLoad - FuelWarningGlobals.lastReading <
            FuelWarningGlobals.updateRate)
        {
            return;
        }

        FuelWarningGlobals.lastReading = Time.timeSinceLevelLoad;
        Aircraft aircraft = Traverse.Create(__instance).Field("aircraft").GetValue<Aircraft>();
        float fuelAmount = aircraft.GetFuelLevel();

        float deltaFuelPerSec = (FuelWarningGlobals.lastFuelAmount - fuelAmount)
            / FuelWarningGlobals.updateRate;

        float fuelTime = float.PositiveInfinity;

        if (deltaFuelPerSec > 0)
        {
            fuelTime = fuelAmount / deltaFuelPerSec;
        }

        FuelWarningGlobals.lastFuelAmount = fuelAmount;

        Plugin.Logger.LogInfo($"Fuel time: {fuelTime}");

        var fuelMinutes = Mathf.FloorToInt(fuelTime / 60f);

        // Display fuel time
        FuelWarningGlobals.fuelTimeLabel.GetComponent<Text>().text = $"({fuelMinutes}m)";

        if (fuelTime < FuelWarningGlobals.bingoFuelTime)
        {
            InterfaceAudio.PlayOneShotV(FuelWarningGlobals.bingoFuelClip, 3f);
            return;
        }
        else if (fuelTime < FuelWarningGlobals.lowFuelTime)
        {
            InterfaceAudio.PlayOneShotV(FuelWarningGlobals.fuelWarningClip, 3f);
            return;
        }
    }
}

[HarmonyPatch(typeof(FuelGauge), "Initialize")]
public class FuelWarningInitializePatch
{
    public static void Postfix(FuelGauge __instance, ref Aircraft aircraft)
    {
        if (!Plugin.fuelWarnings.Value)
        {
            return;
        }

        FuelWarningGlobals.lastFuelAmount = aircraft.GetFuelLevel();
        FuelWarningGlobals.lastReading = Time.timeSinceLevelLoad;
        FuelWarningGlobals.initialized = false;
    }
}