

using UnityEngine;
using UnityEngine.UI;

namespace MKMods;


class LineUtility
{
    // Class that makes it easy to draw lines by creating a gameobject
    // with a 1px wide Image because unity is too stupid to have a line
    // primitive

    // The line starts being a pixel
    public static GameObject CreateLine(Color color, float thickness = 1)
    {
        var line = new GameObject("Line");
        var image = line.AddComponent<Image>();
        image.color = color;
        image.rectTransform.sizeDelta = new Vector2(thickness, 1);

        // Set the right material for the line
        image.material = new Material(Shader.Find("UI/Default"));

        // Set the pivot to the bottom center
        image.rectTransform.pivot = new Vector2(0.5f, 0);

        return line;
    }

    // Update the line to be between two points
    public static void UpdateLine(GameObject line, Vector2 start, Vector2 end)
    {
        line.transform.position = start;
        var diff = end - start;
        line.transform.localScale = Vector3.one
            + Vector3.up * (diff.magnitude * (1080f / (float)Screen.height) - 8f);

        // Set the rotation to the angle between the two points
        var z = (0f - Mathf.Atan2(diff.x, diff.y)) * Mathf.Rad2Deg;

        line.transform.eulerAngles = new Vector3(0, 0, z);
    }
}

class MaskUtility
{
    // Class that makes it easy to set up a UI mask by creating a gameobject
    // All children of the mask will be masked
    public static GameObject CreateMask()
    {
        var mask = new GameObject("Mask");
        var imageComponent = mask.AddComponent<Image>();
        imageComponent.isMaskingGraphic = true;
        var maskComponent = mask.AddComponent<Mask>();

        maskComponent.showMaskGraphic = false;

        return mask;
    }
}