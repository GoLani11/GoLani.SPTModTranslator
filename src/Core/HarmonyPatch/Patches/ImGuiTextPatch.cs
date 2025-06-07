using HarmonyLib;
using UnityEngine;
using GoLani.SPTModTranslator.Core.TextDetection;
using GoLani.SPTModTranslator.Integration.BepInEx;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    [HarmonyPatch(typeof(GUI))]
    public static class ImGuiTextPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(GUI.Label), new[] { typeof(Rect), typeof(string) })]
        [HarmonyPrefix]
        public static bool LabelPrefix(Rect position, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "ImGui.Label",
                    MethodName = "Label",
                    MethodParameters = new object[] { position, text },
                    SourceModule = "ImGui"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"ImGui Label 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUI.Label), new[] { typeof(Rect), typeof(string), typeof(GUIStyle) })]
        [HarmonyPrefix]
        public static bool LabelWithStylePrefix(Rect position, ref string text, GUIStyle style)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "ImGui.Label",
                    MethodName = "Label",
                    MethodParameters = new object[] { position, text, style },
                    SourceModule = "ImGui"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"ImGui Label with Style 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUI.Button), new[] { typeof(Rect), typeof(string) })]
        [HarmonyPrefix]
        public static bool ButtonPrefix(Rect position, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "ImGui.Button",
                    MethodName = "Button",
                    MethodParameters = new object[] { position, text },
                    SourceModule = "ImGui"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"ImGui Button 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUI.TextField), new[] { typeof(Rect), typeof(string) })]
        [HarmonyPrefix]
        public static bool TextFieldPrefix(Rect position, ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "ImGui.TextField",
                    MethodName = "TextField",
                    MethodParameters = new object[] { position, text },
                    SourceModule = "ImGui"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"ImGui TextField 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }
}