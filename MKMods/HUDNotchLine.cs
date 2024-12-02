

using System.Collections.Generic;
using System.Security.Cryptography;
using HarmonyLib;
using Mirage.Serialization;
using UnityEngine;

namespace MKMods;

public class FlightHudPatchGlobals
{
    public static Dictionary<GameObject, GameObject> mapNotchToLine = new Dictionary<GameObject, GameObject>();
    public static Transform centerHudMask;
    public static void Initialize(FlightHud __instance)
    {
        Plugin.Logger.LogInfo("HUDNotchLine Start");
        // Create orange line with thickness 4

        var centerHud = Traverse.Create(__instance).Field("HUDCenter").GetValue<Transform>();

        var mask = MaskUtility.CreateMask();

        // Set name
        mask.name = "MKModsHUDCenterMask";

        mask.transform.SetParent(centerHud, false);
        // Make the mask be 400 pixels wide, centeed in the HUD
        mask.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 400);
        mask.GetComponent<RectTransform>().anchoredPosition = new Vector2(
            0,
            0);

        centerHudMask = mask.transform;
    }
}


[HarmonyPatch(typeof(FlightHud), "Update")]
public class FlightHudPatchUpdate
{

    public static void Postfix(FlightHud __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }

        var centerHud = Traverse.Create(__instance).Field("HUDCenter").GetValue<Transform>();
        // See if the center hud has a child called MKModsHUDCenterMask
        var alreadyInitialized = false;
        foreach (Transform child in centerHud)
        {
            if (child.name == "MKModsHUDCenterMask")
            {
                alreadyInitialized = true;
                break;
            }
        }

        //Plugin.Logger.LogInfo("HUDNotchLine Update");
        if (!alreadyInitialized)
        {
            FlightHudPatchGlobals.Initialize(__instance);
        }


        foreach (var pair in FlightHudPatchGlobals.mapNotchToLine)
        {
            var notchLine = pair.Key;
            var line = pair.Value;

            // The notch line euler angle z is computed as
            // DynamicMap.i.mapImage.transform.eulerAngles.z - quaternion.eulerAngles.y

            // I think this means that quaternion.eulerAngles.y is the angle of 
            // the notch line?

            // nz = dm.z - q.y
            // q.y = dm.z - nz

            var dm = DynamicMap.i.mapImage.transform.eulerAngles.z;
            var nz = notchLine.transform.eulerAngles.z;

            var notchHeading = dm - nz;
            var angle = CombatHUD.i.aircraft.transform.eulerAngles.z;

            // Draw two points such that the line is always in the center of the screen
            // and the angle of the line is the same as the aircraft
            var lineLen = 10000f;
            var topRightX = lineLen * Mathf.Sin(angle * Mathf.Deg2Rad);
            var topRightY = lineLen * Mathf.Cos(angle * Mathf.Deg2Rad);
            var bottomLeftX = -topRightX;
            var bottomLeftY = -topRightY;

            var bottomLeft = new Vector2(bottomLeftX, bottomLeftY);
            var topRight = new Vector2(topRightX, topRightY);

            // Compute a vector notchHeading degrees from the position of the camera
            var notchVector = CameraStateManager.i.transform.position + 10 * new Vector3(
                Mathf.Sin(notchHeading * Mathf.Deg2Rad),
                0,
                Mathf.Cos(notchHeading * Mathf.Deg2Rad)
            );

            // Convert world to screen coordinates
            var notchScreen = Camera.main.WorldToScreenPoint(notchVector);

            bottomLeft = bottomLeft + new Vector2(notchScreen.x, notchScreen.y);
            topRight = topRight + new Vector2(notchScreen.x, notchScreen.y);

            // Draw a debug line from the center of the screen to the notch
            //LineUtility.UpdateLine(FlightHudPatchGlobals.debugLine, new Vector2(halfW, halfH), notchScreen);

            LineUtility.UpdateLine(line, bottomLeft, topRight);


        }
    }
}

[HarmonyPatch(typeof(FlightHud), "OnDestroy")]
class FlightHudPatchOnDestroy
{
    static void Prefix(FlightHud __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }
        //Plugin.Logger.LogInfo("HUDNotchLine OnDestroy");
        FlightHudPatchGlobals.mapNotchToLine.Clear();
    }
}

[HarmonyPatch(typeof(ThreatItem), "FoundIcon")]
class ThreatItemFoundIconPatch
{
    public static void Postfix(ThreatItem __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }
        //Plugin.Logger.LogInfo("HUDNotchLine FoundIcon");
        // See if it has a notch line
        var notchLine = Traverse.Create(__instance).Field("notchLine")
            .GetValue<GameObject>();
        if (notchLine)
        {
            // short circuit if the line already exists
            if (FlightHudPatchGlobals.mapNotchToLine.ContainsKey(notchLine))
            {
                return;
            }
            // Create a line
            var line = LineUtility.CreateLine(new Color(255 / 255f, 66 / 255f, 55 / 255f, 109 / 255f), thickness: 2);
            line.transform.SetParent(FlightHudPatchGlobals.centerHudMask, false);

            // Map the notch line to the line
            FlightHudPatchGlobals.mapNotchToLine[notchLine] = line;
        }
    }
}

[HarmonyPatch(typeof(ThreatItem), "OnDisable")]
class ThreatItemOnDisablePatch
{
    static void Prefix(ThreatItem __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }
        //Plugin.Logger.LogInfo("HUDNotchLine OnDisable");
        // See if it has a notch line
        var notchLine = Traverse.Create(__instance).Field("notchLine")
            .GetValue<GameObject>();
        if (notchLine)
        {
            // Disable the line
            FlightHudPatchGlobals.mapNotchToLine[notchLine].SetActive(false);
        }
    }
}

[HarmonyPatch(typeof(ThreatItem), "OnEnable")]
class ThreatItemOnEnablePatch
{
    static void Prefix(ThreatItem __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }
        Plugin.Logger.LogInfo("HUDNotchLine OnEnable");
        // See if it has a notch line
        var notchLine = Traverse.Create(__instance).Field("notchLine")
            .GetValue<GameObject>();
        if (notchLine)
        {
            // if the line does not exist, call FoundIcon
            if (!FlightHudPatchGlobals.mapNotchToLine.ContainsKey(notchLine))
            {
                ThreatItemFoundIconPatch.Postfix(__instance);
            }
            // Enable the line
            FlightHudPatchGlobals.mapNotchToLine[notchLine].SetActive(true);
        }
    }
}

[HarmonyPatch(typeof(ThreatItem), "OnDestroy")]
class ThreatItemOnDestroyPatch
{
    static void Prefix(ThreatItem __instance)
    {
        if (!Plugin.hudNotchLine.Value)
        {
            return;
        }
        //Plugin.Logger.LogInfo("HUDNotchLine OnDestroy");
        // See if it has a notch line
        var notchLine = Traverse.Create(__instance).Field("notchLine")
            .GetValue<GameObject>();
        if (notchLine)
        {
            // Destroy the line
            GameObject.Destroy(FlightHudPatchGlobals.mapNotchToLine[notchLine]);
            FlightHudPatchGlobals.mapNotchToLine.Remove(notchLine);
        }
    }
}