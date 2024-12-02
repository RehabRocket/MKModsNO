

using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace MKMods;

class MissileVoiceWarningGlobals
{
    public static Dictionary<string, string> seekerToAudioPath = new Dictionary<string, string>
    {
        {new IRSeeker().GetSeekerType(), "flare.mp3"},
        {new OpticalSeeker().GetSeekerType(), "hide.mp3"},
        {new ARHSeeker().GetSeekerType(), "notch.mp3"},
        {new SARHSeeker().GetSeekerType(), "notch.mp3"},
        {new ARMSeeker().GetSeekerType(), "radar.mp3"},
    };

    public static Dictionary<string, AudioClip> seekerToAudioClip = new Dictionary<string, AudioClip>();

    public static Dictionary<string, int> numMissiles = new Dictionary<string, int>
    {
        {new IRSeeker().GetSeekerType(), 0},
        {new OpticalSeeker().GetSeekerType(), 0},
        {new ARHSeeker().GetSeekerType(), 0},
        {new SARHSeeker().GetSeekerType(), 0},
        {new ARMSeeker().GetSeekerType(), 0},
    };

    public static int missileTypeIndex = 0;
    public static float voiceLastPlayed = 0;
    public static void Initialize()
    {
        if (!Plugin.missileVoiceWarnings.Value)
        {
            return;
        }
        Plugin.Logger.LogInfo("MissileVoiceWarning Start");
        // Load audio clips from seekerToAudioPath
        foreach (var kvp in seekerToAudioPath)
        {
            var actualPath = Path.Combine(Plugin.assetsPath, kvp.Value);
            Plugin.Logger.LogInfo($"Loading {actualPath}");
            using (var uwr = UnityWebRequestMultimedia.GetAudioClip(
                $"file://{actualPath}", AudioType.MPEG))
            {
                uwr.SendWebRequest();
                while (!uwr.isDone) { }
                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    var audioClip = DownloadHandlerAudioClip.GetContent(uwr);
                    seekerToAudioClip[kvp.Key] = audioClip;
                    Plugin.Logger.LogInfo($"Loaded {actualPath}");
                }
                else
                {
                    Plugin.Logger.LogError($"Failed to load audio clip from {actualPath}: {uwr.error}");
                }
            }
        }
    }
}


[HarmonyPatch(typeof(ThreatList), "ThreatList_OnMissileWarning")]
class ThreatList_OnMissileWarningPatch
{
    static void Prefix(ThreatList __instance, ref MissileWarning.OnMissileWarning e)
    {
        if (!Plugin.missileVoiceWarnings.Value)
        {
            return;
        }
        var itemLookup = Traverse.Create(__instance).Field("itemLookup")
            .GetValue<Dictionary<int, ThreatItem>>();


        if (itemLookup.ContainsKey(e.missile.persistentID))
        {
            return;
        }

        var seekerType = e.missile.GetSeekerType();
        MissileVoiceWarningGlobals.numMissiles[seekerType]++;

        Plugin.Logger.LogInfo($"MissileVoiceWarning: {seekerType} -> {MissileVoiceWarningGlobals.numMissiles[seekerType]}");
    }
}

[HarmonyPatch(typeof(ThreatList), "ThreatList_OffMissileWarning")]
class ThreatList_OffMissileWarningPatch
{
    static void Prefix(ThreatList __instance, ref MissileWarning.OffMissileWarning e)
    {
        if (!Plugin.missileVoiceWarnings.Value)
        {
            return;
        }
        var itemLookup = Traverse.Create(__instance).Field("itemLookup")
            .GetValue<Dictionary<int, ThreatItem>>();

        if (!itemLookup.ContainsKey(e.missile.persistentID))
        {
            return;
        }

        var seekerType = e.missile.GetSeekerType();
        MissileVoiceWarningGlobals.numMissiles[seekerType]--;

        Plugin.Logger.LogInfo($"MissileVoiceWarning: {seekerType} -> {MissileVoiceWarningGlobals.numMissiles[seekerType]}");
    }
}

[HarmonyPatch(typeof(ThreatList), "Update")]
class ThreatListUpdatePatch
{
    static void Postfix(ThreatList __instance)
    {
        if (!Plugin.missileVoiceWarnings.Value)
        {
            return;
        }
        if (Time.timeSinceLevelLoad - MissileVoiceWarningGlobals.voiceLastPlayed < 1)
        {
            return;
        }

        for (var i = 1; i <= MissileVoiceWarningGlobals.seekerToAudioClip.Count; i++)
        {
            var hypotheticalIndex = (i + MissileVoiceWarningGlobals.missileTypeIndex)
                % MissileVoiceWarningGlobals.seekerToAudioClip.Count;

            var seekerType = MissileVoiceWarningGlobals.seekerToAudioClip.Keys
                .ToList()[hypotheticalIndex];

            if (MissileVoiceWarningGlobals.numMissiles[seekerType] > 0)
            {
                InterfaceAudio.PlayOneShotV(
                    MissileVoiceWarningGlobals.seekerToAudioClip[seekerType],
                    3f
                );
                Plugin.Logger.LogInfo($"Playing {seekerType}");
                MissileVoiceWarningGlobals.voiceLastPlayed = Time.timeSinceLevelLoad;
                MissileVoiceWarningGlobals.missileTypeIndex = hypotheticalIndex;
                break;
            }
        }

    }
}

[HarmonyPatch(typeof(ThreatList), "SetAircraft")]
class ThreatListSetAircraftPatch
{
    static void Prefix(ThreatList __instance, ref Aircraft aircraft)
    {
        if (!Plugin.missileVoiceWarnings.Value)
        {
            return;
        }
        MissileVoiceWarningGlobals.numMissiles = new Dictionary<string, int>
        {
            {new IRSeeker().GetSeekerType(), 0},
            {new OpticalSeeker().GetSeekerType(), 0},
            {new ARHSeeker().GetSeekerType(), 0},
            {new SARHSeeker().GetSeekerType(), 0},
            {new ARMSeeker().GetSeekerType(), 0},
        };
        MissileVoiceWarningGlobals.missileTypeIndex = 0;
        MissileVoiceWarningGlobals.voiceLastPlayed = 0;
    }
}