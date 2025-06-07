using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using GoLani.SPTModTranslator.Core.TextDetection;
using GoLani.SPTModTranslator.Integration.BepInEx;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    [HarmonyPatch(typeof(Text))]
    public static class UnityUITextPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(Text.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool SetTextPrefix(Text __instance, ref string value)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(value))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "UnityUI.Text",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    SourceModule = "UnityUI"
                };

                value = TextInterceptor.InterceptText(value, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Unity UI Text 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch("SetText", new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool SetTextMethodPrefix(Text __instance, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "UnityUI.Text",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    MethodName = "SetText",
                    SourceModule = "UnityUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Unity UI SetText 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TextMesh))]
    public static class UnityTextMeshPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(TextMesh.text), MethodType.Setter)]
        [HarmonyPrefix]
        public static bool SetTextPrefix(TextMesh __instance, ref string value)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(value))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    SourceComponent = __instance,
                    ComponentType = "Unity.TextMesh",
                    TextProperty = "text",
                    TargetGameObject = __instance.gameObject,
                    SourceModule = "Unity3D"
                };

                value = TextInterceptor.InterceptText(value, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Unity TextMesh 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }
}