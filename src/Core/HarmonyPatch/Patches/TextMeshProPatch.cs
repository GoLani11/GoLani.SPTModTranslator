using HarmonyLib;
using TMPro;
using GoLani.SPTModTranslator.Core.TextDetection;
using GoLani.SPTModTranslator.Integration.BepInEx;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    [HarmonyPatch(typeof(TextMeshProUGUI))]
    public static class TextMeshProUGUIPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(TextMeshProUGUI.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool SetTextPrefix(TextMeshProUGUI __instance, ref string value)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(value))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "TextMeshPro.UGUI",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    SourceModule = "TextMeshPro"
                };

                value = TextInterceptor.InterceptText(value, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"TextMeshPro UGUI 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch("SetText", new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool SetTextMethodPrefix(TextMeshProUGUI __instance, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "TextMeshPro.UGUI",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    MethodName = "SetText",
                    SourceModule = "TextMeshPro"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"TextMeshPro SetText 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TextMeshPro))]
    public static class TextMeshProPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(TextMeshPro.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool SetTextPrefix(TextMeshPro __instance, ref string value)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(value))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "TextMeshPro.3D",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    SourceModule = "TextMeshPro"
                };

                value = TextInterceptor.InterceptText(value, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"TextMeshPro 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch("SetText", new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool SetTextMethodPrefix(TextMeshPro __instance, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "TextMeshPro.3D",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    MethodName = "SetText",
                    SourceModule = "TextMeshPro"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"TextMeshPro SetText 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }
}