using HarmonyLib;
using UnityEngine.UIElements;

namespace PuckReplayMod
{
    [HarmonyPatch(typeof(UIManager), "Awake")]
    public static class UIManagerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIManager __instance)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnUiManagerAwake(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(UIMainMenu), "Initialize")]
    public static class UIMainMenuInitializePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIMainMenu __instance, VisualElement rootVisualElement)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnMainMenuInitialized(__instance, rootVisualElement);
            }
        }
    }

    [HarmonyPatch(typeof(UIPauseMenu), "Initialize")]
    public static class UIPauseMenuInitializePatch
    {
        [HarmonyPostfix]
        public static void Postfix(UIPauseMenu __instance, VisualElement rootVisualElement)
        {
            ReplayModController instance = ReplayModController.Instance;
            if (instance != null)
            {
                instance.OnPauseMenuInitialized(__instance, rootVisualElement);
            }
        }
    }
}
