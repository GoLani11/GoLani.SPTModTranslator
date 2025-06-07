using HarmonyLib;
using UnityEngine;
using GoLani.SPTModTranslator.Core.TextDetection;
using GoLani.SPTModTranslator.Integration.BepInEx;

namespace GoLani.SPTModTranslator.Core.HarmonyPatch
{
    [HarmonyPatch(typeof(GUILayout))]
    public static class LegacyGUITextPatch
    {
        private static ITextInterceptor TextInterceptor => 
            SKLFPlugin.Instance?.GetComponent<ITextInterceptor>();

        [HarmonyPatch(nameof(GUILayout.Label), new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool LabelPrefix(ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "GUILayout.Label",
                    MethodName = "Label",
                    MethodParameters = new object[] { text },
                    SourceModule = "LegacyGUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Legacy GUI Label 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUILayout.Label), new[] { typeof(string), typeof(GUILayoutOption[]) })]
        [HarmonyPrefix]
        public static bool LabelWithOptionsPrefix(ref string text, GUILayoutOption[] options)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "GUILayout.Label",
                    MethodName = "Label",
                    MethodParameters = new object[] { text, options },
                    SourceModule = "LegacyGUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Legacy GUI Label with Options 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUILayout.Button), new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool ButtonPrefix(ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "GUILayout.Button",
                    MethodName = "Button",
                    MethodParameters = new object[] { text },
                    SourceModule = "LegacyGUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Legacy GUI Button 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUILayout.TextField), new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool TextFieldPrefix(ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "GUILayout.TextField",
                    MethodName = "TextField",
                    MethodParameters = new object[] { text },
                    SourceModule = "LegacyGUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Legacy GUI TextField 번역 중 오류: {ex.Message}");
            }

            return true;
        }

        [HarmonyPatch(nameof(GUILayout.TextArea), new[] { typeof(string) })]
        [HarmonyPrefix]
        public static bool TextAreaPrefix(ref string text)
        {
            if (TextInterceptor == null || string.IsNullOrEmpty(text))
                return true;

            try
            {
                var context = new TextInterceptionContext
                {
                    ComponentType = "GUILayout.TextArea",
                    MethodName = "TextArea",
                    MethodParameters = new object[] { text },
                    SourceModule = "LegacyGUI"
                };

                text = TextInterceptor.InterceptText(text, context);
            }
            catch (System.Exception ex)
            {
                SKLFPlugin.Logger?.LogError($"Legacy GUI TextArea 번역 중 오류: {ex.Message}");
            }

            return true;
        }
    }
}