# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language Setting

**IMPORTANT: Always communicate in Korean (한국어) when working in this repository.** This is a Korean translation management project, and all communication should be in Korean to maintain consistency with the project's context and user base.

## Project Overview

GoLani SPT Mod Translator is a BepInEx plugin for SPTarkov (Single Player Tarkov) that provides dynamic translation capabilities for game mods without modifying original mod files. The plugin uses Harmony patching to intercept string methods and applies translations based on JSON configuration files.

## Build Commands

- **Build**: `dotnet build` or use Visual Studio
- **Target Framework**: .NET Framework 4.7.2
- **Required Dependencies**: BepInEx.Core, 0Harmony, UnityEngine.CoreModule, Newtonsoft.Json

Note: The project currently uses direct DLL references in the .csproj file pointing to local paths. For development, ensure you have the SPTarkov modding environment set up with the required BepInEx dependencies.

## Core Architecture

### Service Layer Architecture
The project follows a service-oriented architecture with these core components:

- **TranslationService**: Manages loading and retrieving translations from JSON files. Supports dynamic value substitution (e.g., resolution values in strings like "Maximum Width (1920p)"). Handles language switching and untranslated string extraction.

- **PatchService**: Uses Harmony to dynamically patch target methods at runtime. Maintains a mapping between original methods and patch definitions. Supports three patch types: PostfixReturnString, PrefixRefStringParameter, and PrefixStringParameter.

- **PatchDefinitionService**: Loads and manages patch definitions from JSON files that specify which methods to patch and how.

- **UntranslatedExtractor**: Collects untranslated strings during gameplay for translation file generation.

### Data Flow
1. Plugin loads translation files from `translations/{language}/` folders
2. Patch definitions are loaded from `patch_definitions/` folder  
3. Harmony patches are applied to target assemblies/methods
4. When patched methods execute, translations are applied via service layer
5. Untranslated strings are optionally collected for export

### File Structure
- `translations/{lang}/{ModName}.json`: Translation key-value pairs
- `patch_definitions/{ModName}_patches.json`: Method patching configuration
- `Core/`: Service layer implementations
- `Models/`: Data models (PatchDefinition, enums)

### Key Design Patterns
- **Singleton Pattern**: Main plugin class maintains static instance
- **Service Locator**: Core services accessed statically
- **Configuration-Driven**: All patching behavior defined via JSON files
- **Runtime Patching**: Uses Harmony for non-invasive method interception

### Translation System
The translation system supports:
- Multi-language support via folder structure
- Dynamic value substitution using regex patterns
- ConfigMenu special handling for BepInEx configuration UI
- Fallback to original text when translations missing
- Thread-safe untranslated string logging

### Patch Types
- `PostfixReturnString`: Translates method return values
- `PrefixRefStringParameter`: Translates ref/out string parameters  
- `PrefixStringParameter`: Translates input string parameters

## Development Notes

- The plugin initializes during BepInEx Awake() phase
- Configuration changes trigger patch reapplication via Update() loop
- All logging uses BepInEx ManualLogSource
- Thread safety is critical for translation extraction during gameplay
- Patch definitions support duplicate detection and prevention