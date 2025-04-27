# GoLani SPT Mod Translator

---

**GoLani SPT Mod Translator** is a tool that makes it easy to apply translations in various languages to SPTarkov (Single Player Tarkov) mods (plugins). You don't need programming knowledge—just add translation files, and you can see mod texts in Korean or any language you want in-game.

---

## What does this plugin do?

- Apply translations to mods without modifying the original mod files.
- Support translations for multiple mods at the same time.
- Manage translation files and patch rules (patch definitions) as simple text files (JSON).
- Open the settings menu in-game with F12 to easily change the translation language and enable/disable the plugin.
- Automatically extract untranslated texts for you to translate.
- Duplicate patch definitions are automatically ignored for safety.

---

## Folder Structure

```
Project Folder/
├─ translations/           # Where translation files go
│  ├─ ko/                  # Korean translations
│  │   ├─ ExampleMod.json  # Example: Korean translation for ExampleMod
│  ├─ ja/                  # Japanese translations
│  │   ├─ ExampleMod.json
│  └─ ...
├─ patch_definitions/      # Where patch definition files go
│  ├─ ExampleMod_patches.json
│  └─ ...
└─ BepInEx/
   └─ plugins/
      └─ GoLaniSPTModTranslator/
         ├─ GoLaniSPTModTranslator.dll
         ├─ translations/
         └─ patch_definitions/
```

---

## Installation

1. **Download the plugin mod:**
   - Download the latest `GoLani.SPTModTranslator.zip` from the releases and extract it.
2. **Apply the mod:**
   - Put your translation files (e.g., `ModName.json`) into the appropriate language folder under  
     `SPT install folder\BepInEx\plugins\GoLani.SPTModTranslator\translations`.
3. **Run the game:**
   - Start SPTarkov.
4. **Configure:**
   - In-game, press F12 to open the settings menu.
   - Select "Enable Mod" and your preferred "Language".
   - After changing settings, it's best to close and reopen the F12 menu.

---

## How do I create translation files?

1. Go into the language folder you want inside `translations` (e.g., `ko`, `ja`, `en`).
2. Create a new file named `[ModName].json` (e.g., `ExampleMod.json`).
3. Write pairs of original English text and your translation, like this:

```json
{
  "Save": "저장",
  "Cancel": "취소",
  "Settings": "설정",
  "Original English Text": "Sample translation"
}
```
- The left side (English) is the actual text from the game/mod.
- The right side (Korean, etc.) is your translation.

---

## When and why do I need a patch definition file?

> **Important:**
> - The in-game settings menu (names, descriptions, etc. shown via F12) is automatically translated by the plugin.
> - **In most cases, you only need to create translation files!**
> - You only need to create a patch definition file if you want to translate a mod's unique UI, dialogs, buttons, popups, or any part outside the settings menu.

### How to create a patch definition file

1. Go to the `patch_definitions` folder.
2. Create a new file named `[ModName]_patches.json` (e.g., `ExampleMod_patches.json`).
3. Write the following structure:

```json
[
  {
    "Enabled": true,
    "TargetAssembly": "ExampleMod", // The mod's DLL name (without .dll)
    "TargetType": "ExampleMod.UI.ExampleUI", // Full class name to patch
    "TargetMethod": "GetButtonText", // Method name to patch
    "PatchType": "PostfixReturnString", // Patch type (see below)
    "TranslationModID": "ExampleMod" // Should match the translation file name prefix
  },
  {
    "Enabled": true,
    "TargetAssembly": "ExampleMod",
    "TargetType": "ExampleMod.Core.ExampleLogic",
    "TargetMethod": "ShowMessage",
    "PatchType": "PrefixStringParameter",
    "ParameterIndex": 0, // Index of the string parameter to translate (starting from 0)
    "TranslationModID": "ExampleMod"
  }
]
```

#### Key fields explained
- `TargetAssembly`: The mod's DLL file name (without .dll)
- `TargetType`: Full class name (including namespace)
- `TargetMethod`: Method name to patch
- `PatchType`: Patch type (see below)
- `ParameterIndex`: If there are multiple string parameters, which one to translate (starting from 0)
- `TranslationModID`: Prefix of the translation file name (e.g., ExampleMod.json → ExampleMod)

#### PatchType options
- `PostfixReturnString`: Translate the method's return value (string)
- `PrefixRefStringParameter`: Translate a ref/out string parameter (needs ParameterIndex)
- `PrefixStringParameter`: Translate a regular string parameter (needs ParameterIndex)

---

## How to apply and check translations

1. Prepare your translation file (`translations/ko/ExampleMod.json`, etc.) and, if needed, a patch definition file (`patch_definitions/ExampleMod_patches.json`).
2. Run the game and open the F12 settings menu to make sure the plugin is enabled.
3. Check in-game to see if your translations are working.

---

## Frequently Asked Questions (FAQ)

**Q. Do I only need to create translation files?**
- Yes, for most cases (settings menu, etc.), translation files are enough. Only create patch definition files if you want to translate a mod's unique UI/text.

**Q. My translation doesn't show up right away!**
- Try closing and reopening the F12 menu. If it still doesn't work, restart the game.

**Q. How do I find untranslated texts?**
- In the F12 settings, check "Extract untranslated strings." As you explore the game, untranslated texts will be automatically collected. When done, check the `untranslations` folder.

**Q. Can I support multiple languages at once?**
- Yes! Add translation files to `translations/ko/`, `translations/ja/`, `translations/en/`, etc. You can switch languages in the F12 menu.

---

## Developer Build Info

- Visual Studio 2019 or later (.NET Framework 4.7.2)
- Required libraries (via NuGet or direct reference):
    - `BepInEx.Core`
    - `BepInEx.PluginInfoProps`
    - `UnityEngine.CoreModule`
    - `0Harmony`
    - `Newtonsoft.Json`

--- 

## AI Support
- This project was developed with the assistance of Cursor AI.