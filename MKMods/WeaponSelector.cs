

using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirage.Serialization;
using Mono.Cecil;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements.StyleSheets;

namespace MKMods;


public static class RectTransformExtensions
{

    public static bool Overlaps(this RectTransform a, RectTransform b)
    {
        return a.WorldRect().Overlaps(b.WorldRect());
    }
    public static bool Overlaps(this RectTransform a, RectTransform b, bool allowInverse)
    {
        return a.WorldRect().Overlaps(b.WorldRect(), allowInverse);
    }

    public static Rect WorldRect(this RectTransform rectTransform)
    {
        Vector2 sizeDelta = rectTransform.sizeDelta;
        float rectTransformWidth = sizeDelta.x * rectTransform.lossyScale.x;
        float rectTransformHeight = sizeDelta.y * rectTransform.lossyScale.y;

        Vector3 position = rectTransform.position;
        return new Rect(position.x - rectTransformWidth / 2f, position.y - rectTransformHeight / 2f, rectTransformWidth, rectTransformHeight);
    }

    public static float OverlapAmount(this RectTransform a, RectTransform b)
    {
        // we implement this by checking the overlap of the world rects
        // and then calculating the area of the overlap
        if (!a.Overlaps(b))
        {
            return 0;
        }

        var aRect = a.WorldRect();
        var bRect = b.WorldRect();

        var xOverlap = Mathf.Min(aRect.xMax, bRect.xMax)
            - Mathf.Max(aRect.xMin, bRect.xMin);
        var yOverlap = Mathf.Min(aRect.yMax, bRect.yMax)
            - Mathf.Max(aRect.yMin, bRect.yMin);

        return xOverlap * yOverlap;
    }
}

[HarmonyPatch(typeof(AircraftSelectionMenu), "ShowHardpoints")]
class AircraftSelectionMenuPatch
{

    static bool Prefix(AircraftSelectionMenu __instance)
    {
        if (!Plugin.dynamicWeaponSelection.Value)
        {
            return true;
        }

        Plugin.Logger.LogInfo("AircraftSelectionMenu.ShowHardpoints");
        // We will create a copy of the weapon image, and try to place it so 
        // it hovers over the actual 3d weapon model.

        //var weaponImageArea = Traverse.Create(__instance).Field("weaponImageArea")
        //    .GetValue<GameObject>();

        var weaponSelectionPrefab = Traverse.Create(__instance)
            .Field("weaponSelectionPrefab")
            .GetValue<GameObject>();

        var previewAircraft = Traverse.Create(__instance).Field("previewAircraft")
            .GetValue<Aircraft>();

        WeaponSelectorUpdatePatch.previewAircraft = previewAircraft;

        var weaponManager = previewAircraft.weaponManager;

        foreach (var weaponSelector in __instance.weaponSelectors)
        {
            weaponSelector.Remove();
        }
        __instance.weaponSelectors.Clear();

        if (WeaponSelectorUpdatePatch.lines == null)
        {
            WeaponSelectorUpdatePatch.lines = new List<GameObject>();
        }
        else
        {
            foreach (var line in WeaponSelectorUpdatePatch.lines)
            {
                GameObject.Destroy(line);
            }
            WeaponSelectorUpdatePatch.lines.Clear();
        }

        WeaponSelectorUpdatePatch.weaponSelectorVelocities.Clear();

        var airbase = Traverse.Create(__instance).Field("airbase").GetValue<Airbase>();

        if (weaponManager != null)
        {
            for (int i = 0; i < weaponManager.hardpointSets.Length; i++)
            {
                HardpointSet hardpointSet = weaponManager.hardpointSets[i];
                // spawn a weapon image for each hardpoint set
                var weaponSelectionObject = GameObject.Instantiate(
                    weaponSelectionPrefab,
                    __instance.transform
                );
                weaponSelectionObject.SetActive(true);

                // Make it only 290 units wide
                var rectTransform = weaponSelectionObject.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(290, 80);

                // Set the color to 3E463CC5 and enable fillCenter
                var image = weaponSelectionObject.GetComponent<Image>();

                image.color = new Color(
                    0x3E / 255.0f,
                    0x46 / 255.0f,
                    0x3C / 255.0f,
                    (0xC5 / 2.0f) / 255.0f
                );
                image.fillCenter = true;


                var component = weaponSelectionObject.GetComponent<WeaponSelector>();
                component.Initialize(hardpointSet, __instance, airbase.CurrentHQ);

                __instance.weaponSelectors.Add(component);

                WeaponSelectorUpdatePatch.weaponSelectorVelocities
                    [component] = Vector2.zero;

                // Add the line thingy for it with CDF3CF and 128 alpha
                var line = LineUtility.CreateLine(new Color(
                    0x24 / 255.0f,
                    0xFF / 255.0f,
                    0x2F / 255.0f,
                    128 / 255.0f
                ), thickness: 2);
                line.transform.SetParent(__instance.transform);

                // Add the line to the list of lines
                WeaponSelectorUpdatePatch.lines.Add(line);
            }
        }

        WeaponSelectorUpdatePatch.alreadyInitializedPositions = false;
        WeaponSelectorUpdatePatch.aircraftParts = previewAircraft.GetAllParts();

        // Change size of "RightPanel" object to make it smaller now that we don't
        // need to show the selection buttons
        var rightPanel = GameObject.Find("RightPanel");
        var rightPanelRectTransform = rightPanel.GetComponent<RectTransform>();
        rightPanelRectTransform.sizeDelta = new Vector2(
            rightPanelRectTransform.sizeDelta.x, 300
        );
        // Change the size of it's child to 290
        var rightPanelChild = rightPanel.transform.GetChild(0).gameObject;
        var rightPanelChildRectTransform = rightPanelChild.GetComponent<RectTransform>();
        rightPanelChildRectTransform.sizeDelta = new Vector2(
            rightPanelChildRectTransform.sizeDelta.x, 290
        );


        return false;
    }
}

[HarmonyPatch(typeof(AircraftSelectionMenu), "Update")]
class WeaponSelectorUpdatePatch
{
    public static Dictionary<WeaponSelector, Vector2> weaponSelectorVelocities
        = new Dictionary<WeaponSelector, Vector2>();

    public static bool alreadyInitializedPositions = false;
    public static Aircraft previewAircraft;
    public static List<UnitPart> aircraftParts;
    public static List<GameObject> lines;
    static void Postfix(AircraftSelectionMenu __instance)
    {

        if (!Plugin.dynamicWeaponSelection.Value)
        {
            return;
        }

        for (int i = 0; i < +__instance.weaponSelectors.Count; i++)
        {
            //var weaponImage = AircraftSelectionMenuPatch.weaponSelectionObjects[i];
            //var hardpointSet = AircraftSelectionMenuPatch.hardpointSets[i];

            var weaponSelector = __instance.weaponSelectors[i];
            var selectorObject = weaponSelector.gameObject;

            var hardpointSet = Traverse.Create(weaponSelector).Field("hardpointSet")
                .GetValue<HardpointSet>();

            var hardpoint = hardpointSet.hardpoints[(i % 2) % hardpointSet.hardpoints.Count];
            var hardpointPosition = hardpoint.transform.position;

            var screenPosition = Camera.main.WorldToScreenPoint(hardpointPosition);
            var canvasRect = __instance.GetComponent<RectTransform>().rect;

            var targetCanvasPosition = new Vector2(
                screenPosition.x / Screen.width * canvasRect.width,
                screenPosition.y / Screen.height * canvasRect.height
            );

            var rectTransform = selectorObject.GetComponent<RectTransform>();

            // Update line
            var line = lines[i];

            var possibleLineAnchors = new Vector2[]
            {
                new Vector2(0, rectTransform.rect.yMin),
                new Vector2(0, rectTransform.rect.yMax),
                new Vector2(rectTransform.rect.xMin, 0),
                new Vector2(rectTransform.rect.xMax, 0),
            };

            var closestAnchor = possibleLineAnchors.OrderBy(anchor =>
                Vector2.Distance(anchor + rectTransform.anchoredPosition, targetCanvasPosition)
            ).First();

            LineUtility.UpdateLine(line, closestAnchor + rectTransform.anchoredPosition, targetCanvasPosition);

            if (!alreadyInitializedPositions)
            {
                var multiplier = new Vector2(1f, 1f);
                if (i % 2 == 1)
                {
                    multiplier = new Vector2(1f, -1f);
                }

                multiplier *= 1080f / 4.0f;
                var direction = (targetCanvasPosition - new Vector2(canvasRect.width / 2.0f,
                    canvasRect.height / 2.0f));

                direction.Normalize();

                direction = direction * multiplier;

                selectorObject.GetComponent<RectTransform>().anchoredPosition
                    = direction + new Vector2(canvasRect.width / 2.0f,
                    canvasRect.height / 2.0f);
            }

            // Update velocity
            var targetVelocityVector = targetCanvasPosition -
                selectorObject.GetComponent<RectTransform>().anchoredPosition;


            targetVelocityVector *= Time.deltaTime * 0.75f;

            // If we are overlapping with another selector, apply a force to move
            // away from it
            foreach (var otherSelector in __instance.weaponSelectors)
            {
                if (otherSelector == weaponSelector)
                {
                    continue;
                }

                var otherSelectorObject = otherSelector.gameObject;

                if (selectorObject.GetComponent<RectTransform>().Overlaps(
                    otherSelectorObject.GetComponent<RectTransform>()))
                {
                    var awayVector = selectorObject.GetComponent<RectTransform>().anchoredPosition
                        - otherSelectorObject.GetComponent<RectTransform>().anchoredPosition;

                    awayVector.Normalize();
                    //awayVector *= 50 * Time.deltaTime;
                    awayVector *= selectorObject.GetComponent<RectTransform>().OverlapAmount(
                        otherSelectorObject.GetComponent<RectTransform>()
                    ) * 0.01f;

                    weaponSelectorVelocities[weaponSelector] += awayVector;
                }
            }

            // Raycast from corners and center to see if we are overlapping the aircraft

            var corners = new Vector2[]
            {
                new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMin),
                new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax),
                new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMin),
                new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMax),
                new Vector2(0, rectTransform.rect.yMin),
                new Vector2(0, rectTransform.rect.yMax),
                new Vector2(rectTransform.rect.xMin, 0),
                new Vector2(rectTransform.rect.xMax, 0),
                new Vector2(0, 0),
            };

            var awayCenterStrength = 30;
            foreach (var corner in corners)
            {
                var rayPos = new Vector3(
                        corner.x + rectTransform.position.x,
                        corner.y + rectTransform.position.y,
                        0
                    );
                var ray = Camera.main.ScreenPointToRay(
                    rayPos
                );

                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    // See if the hit object is a child of a part of the aircraft
                    for (int partIndex = 0; partIndex < aircraftParts.Count; partIndex++)
                    {
                        var part = aircraftParts[partIndex];
                        if (hit.collider.transform.IsChildOf(part.transform))
                        {
                            awayCenterStrength = 60;
                        }
                    }
                }
            }

            weaponSelectorVelocities[weaponSelector] += targetVelocityVector;

            // Add radial force to get away from the center
            var center = new Vector2(canvasRect.width / 2, canvasRect.height / 2);
            var centerVector = selectorObject.GetComponent<RectTransform>().anchoredPosition - center;
            centerVector.Normalize();
            //centerVector.x *= 1.5f;
            //centerVector.y *= 2f;
            centerVector *= awayCenterStrength * Time.deltaTime;
            weaponSelectorVelocities[weaponSelector] += centerVector;

            // Add a radial force to get away from the target
            var awayTarget = rectTransform.anchoredPosition - targetCanvasPosition;
            awayTarget.Normalize();
            awayTarget *= 120 * Time.deltaTime;
            weaponSelectorVelocities[weaponSelector] += awayTarget;


            // Apply drag
            weaponSelectorVelocities[weaponSelector] *= 1 - Time.deltaTime * 35;

            // Apply velocity
            selectorObject.GetComponent<RectTransform>().anchoredPosition
                += weaponSelectorVelocities[weaponSelector];
        }

        alreadyInitializedPositions = true;
    }
}

[HarmonyPatch(typeof(CameraSelectionState), "MoveCamera")]
class CameraSelectionStatePatch
{
    static bool Prefix(CameraSelectionState __instance, CameraStateManager cam, float deltaTime)
    {
        if (!Plugin.dynamicWeaponSelection.Value)
        {
            return true;
        }

        float num = GameManager.playerInput.GetAxis("Pan View") * PlayerSettings.viewSensitivity;
        float num2 = GameManager.playerInput.GetAxis("Tilt View") * PlayerSettings.viewSensitivity;
        num *= -0.2f;
        num2 *= -0.2f;
        if (!GameManager.playerInput.GetButton("Free Look"))
        {
            num = 0f;
            num2 = 0f;
        }
        if (Mathf.Abs(num) > 0.75f || Mathf.Abs(num2) > 0.75f)
        {
            //manualViewMode = true;
            Traverse.Create(__instance).Field("manualViewMode").SetValue(true);
        }
        var cameraHeight = Traverse.Create(__instance).Field("cameraHeight").GetValue<float>();
        //if (manualViewMode)
        cam.cameraPivot.transform.Rotate(0f, num * -150f * deltaTime, 0f);
        cameraHeight = Traverse.Create(__instance).Field("cameraHeight").GetValue<float>();
        cameraHeight += num2 * -0.5f * deltaTime;
        cameraHeight = Mathf.Clamp01(cameraHeight);
        Traverse.Create(__instance).Field("cameraHeight").SetValue(cameraHeight);
        var target = Traverse.Create(__instance).Field("target").GetValue<Transform>();
        Quaternion b = Quaternion.LookRotation(target.position - cam.transform.position);
        var cameraDistance = Traverse.Create(__instance).Field("cameraDistance").GetValue<float>();
        cameraHeight = Traverse.Create(__instance).Field("cameraHeight").GetValue<float>();
        Vector3 position = cam.cameraPivot.transform.position + (cam.cameraPivot.transform.forward + Vector3.up * cameraHeight) * cameraDistance * 1.4f;
        Quaternion rotation = Quaternion.Slerp(cam.transform.rotation, b, 5f * deltaTime);
        cam.transform.SetPositionAndRotation(position, rotation);
        cameraDistance = Traverse.Create(__instance).Field("cameraDistance").GetValue<float>();
        var viewDistance = Traverse.Create(__instance).Field("viewDistance").GetValue<float>();
        var cameraSmoothingVel = Traverse.Create(__instance).Field("cameraSmoothingVel").GetValue<float>();
        cameraDistance = Mathf.SmoothDamp(cameraDistance, viewDistance, ref cameraSmoothingVel, 0.5f, float.MaxValue, deltaTime);
        Traverse.Create(__instance).Field("cameraDistance").SetValue(cameraDistance);

        return false;
    }
}


