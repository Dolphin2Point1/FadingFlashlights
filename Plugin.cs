using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace FadingFlashlights
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            Patches.logger = Logger;
            
            FFConfig.configStartFade = Config.Bind(
                "Flashlights",
                "FadeStart",
                0.5f,
                "Number between 0 and 1 that represents the decimal that the flashlight will start fading at. For example, "+ 
                "0.5 means it will start fading at half battery."
            );
            FFConfig.configFinalBrightness = Config.Bind(
                "Flashlights",
                "FadeFinalBrightness",
                0f,
                "Number between 0 and 1 that represents the brightness that the flashlight will run out of battery with. For example, "+ 
                "0.5 means it will be at half brightness when it shuts down."
            );
            FFConfig.configFunctionExponent = Config.Bind(
                "Flashlights",
                "FadeFunctionExponent",
                -1f,
                "The logarhithm (base 2) of the power that the result of the fade function will be put to the power of. " +
                "In simple terms, negative numbers mean that your flashlight will stay brighter for longer. Positive numbers mean that " +
                "your flashlight will get dark quickly, and stay dark. Zero means that the light brightness will follow a straight line over time. " +
                "By default, this is set to -1, which corresponds to a square root."
            );
            
            Harmony.CreateAndPatchAll(typeof(Patches));
        }
    }

    class Patches {
        public static ManualLogSource logger;
        private static readonly FieldInfo gameObjectField = typeof(MonoBehaviour)
            .GetField("gameObject", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(FlashlightItem), "Start")]
        [HarmonyPrefix]
        public static void AddFlashlightFader(FlashlightItem __instance) {
            __instance.gameObject.AddComponent<FlashlightFaderComponent>();
            __instance.gameObject.GetComponent<FlashlightFaderComponent>().flashlight = __instance;
        }

        [HarmonyPatch(typeof(FlashlightItem), "PocketItem")]
        [HarmonyPostfix]
        public static void PocketItem(FlashlightItem __instance) {
            __instance.gameObject.GetComponent<FlashlightFaderComponent>().PocketItem();
        }
    }

    public class FlashlightFaderComponent : MonoBehaviour {
        public FlashlightItem flashlight;
        private Color initialBulbColor;
        private Color initialBulbGlowColor;
        private Color initialHelmetLightColor;
        private static readonly FieldInfo previousPlayerHeldBy = typeof(FlashlightItem)
            .GetField("previousPlayerHeldBy", BindingFlags.NonPublic | BindingFlags.Instance);
        
        void Start() {
            initialBulbColor = flashlight.flashlightBulb.color;
            initialBulbGlowColor = flashlight.flashlightBulbGlow.color;
        }

        void Update() {
            float p1 = 1-FFConfig.configStartFade.Value;
            float p2 = FFConfig.configFinalBrightness.Value;
            float unclamped = (1-p2)/p1*(flashlight.insertedBattery.charge-p1)+1;
            float clamped = Mathf.Pow(Mathf.Clamp(unclamped,0,1), Mathf.Pow(2, FFConfig.configFunctionExponent.Value));
            if(flashlight.usingPlayerHelmetLight) {
                PlayerControllerB player = (PlayerControllerB)previousPlayerHeldBy.GetValue(flashlight);
                player.helmetLight.color = initialHelmetLightColor * clamped;
            } else {
                flashlight.flashlightBulb.color = initialBulbColor * clamped;
                flashlight.flashlightBulbGlow.color = initialBulbGlowColor * clamped;
            }
        }

        public void PocketItem() {
            PlayerControllerB player = (PlayerControllerB)previousPlayerHeldBy.GetValue(flashlight);
            initialHelmetLightColor = player.helmetLight.color;
        }
    } 

    public class FFConfig {
        public static ConfigEntry<float> configStartFade;
        public static ConfigEntry<float> configFinalBrightness;
        public static ConfigEntry<float> configFunctionExponent;
    }
}
