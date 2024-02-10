using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FadingFlashlights
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource sLogger { get; internal set; }
        public static FFConfig FFConfig { get; internal set; }

        private void Awake()
        {
            sLogger = Logger;
            // Plugin startup logic
            Patches.logger = Logger;
            
            FFConfig = new FFConfig(Config);

            Harmony.CreateAndPatchAll(typeof(Patches));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
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
            float p1 = 1-Plugin.FFConfig.startFade;
            float p2 = Plugin.FFConfig.finalBrightness;
            float unclamped = (1-p2)/p1*(flashlight.insertedBattery.charge-p1)+1;
            float clamped = Mathf.Pow(Mathf.Clamp(unclamped,0,1), Mathf.Pow(2, Plugin.FFConfig.functionExponent));
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


        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "StartDisconnect")]
        public static void PlayerLeave() {
            FFConfig.RevertSync();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        public static void InitializeLocalPlayer() {
            if (FFConfig.IsHost) {
                FFConfig.MessageManager.RegisterNamedMessageHandler("FadingFlashlights_OnRequestConfigSync", FFConfig.OnRequestSync);
                FFConfig.Synced = true;

                return;
            }

            FFConfig.Synced = false;
            FFConfig.MessageManager.RegisterNamedMessageHandler("FadingFlashlights_OnReceiveConfigSync", FFConfig.OnReceiveSync);
            FFConfig.RequestSync();
        }


    } 

    [Serializable]
    public class FFConfig : SyncedInstance<FFConfig> {
        public static ConfigEntry<float> configStartFade;
        public static ConfigEntry<float> configFinalBrightness;
        public static ConfigEntry<float> configFunctionExponent;

        public float startFade;
        public float finalBrightness;
        public float functionExponent;

        public FFConfig(ConfigFile cfg) {
            InitInstance(this);

            configStartFade = cfg.Bind(
                "Flashlights",
                "FadeStart",
                0.5f,
                "Number between 0 and 1 that represents the decimal that the flashlight will start fading at. For example, "+ 
                "0.5 means it will start fading at half battery."
            );
            configFinalBrightness = cfg.Bind(
                "Flashlights",
                "FadeFinalBrightness",
                0f,
                "Number between 0 and 1 that represents the brightness that the flashlight will run out of battery with. For example, "+ 
                "0.5 means it will be at half brightness when it shuts down."
            );
            configFunctionExponent = cfg.Bind(
                "Flashlights",
                "FadeFunctionExponent",
                -1f,
                "The logarhithm (base 2) of the power that the result of the fade function will be put to the power of. " +
                "In simple terms, negative numbers mean that your flashlight will stay brighter for longer. Positive numbers mean that " +
                "your flashlight will get dark quickly, and stay dark. Zero means that the light brightness will follow a straight line over time. " +
                "By default, this is set to -1, which corresponds to a square root."
            );
            startFade = configStartFade.Value;
            finalBrightness = configFinalBrightness.Value;
            functionExponent = configFunctionExponent.Value;
        }

        public void ReloadConfig() {
            startFade = configStartFade.Value;
            finalBrightness = configFinalBrightness.Value;
            functionExponent = configFunctionExponent.Value;
            RequestSync();
        }

        public static void RequestSync() {
            if (!IsClient) return;

            using FastBufferWriter stream = new(IntSize, Allocator.Temp);
            MessageManager.SendNamedMessage("FadingFlashlights_OnRequestConfigSync", 0uL, stream);
        }

        public static void OnRequestSync(ulong clientId, FastBufferReader _) {
            if (!IsHost) return;

            Plugin.sLogger.LogInfo($"Config sync request received from client: {clientId}");

            byte[] array = SerializeToBytes(Instance);
            int value = array.Length;

            using FastBufferWriter stream = new(value + IntSize, Allocator.Temp);

            try {
                stream.WriteValueSafe(in value, default);
                stream.WriteBytesSafe(array);

                MessageManager.SendNamedMessage("FadingFlashlights_OnReceiveConfigSync", clientId, stream);
            } catch(Exception e) {
                Plugin.sLogger.LogInfo($"Error occurred syncing config with client: {clientId}\n{e}");
            }
        }

        public static void OnReceiveSync(ulong _, FastBufferReader reader) {
            if (!reader.TryBeginRead(IntSize)) {
                Plugin.sLogger.LogError("Config sync error: Could not begin reading buffer.");
                return;
            }

            reader.ReadValueSafe(out int val, default);
            if (!reader.TryBeginRead(val)) {
                Plugin.sLogger.LogError("Config sync error: Host could not sync.");
                return;
            }

            byte[] data = new byte[val];
            reader.ReadBytesSafe(ref data, val);

            SyncInstance(data);

            Plugin.sLogger.LogInfo("Successfully synced config with host.");
        }
    }
}
