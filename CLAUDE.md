# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language Setting

**IMPORTANT: Always communicate in Korean (한국어) when working in this repository.** This is a Korean translation management project, and all communication should be in Korean to maintain consistency with the project's context and user base.

## Thinking Mode

**IMPORTANT: Always use ultrathink mode when working in this repository.** Use detailed thinking processes to analyze problems, plan solutions, and ensure code quality. This helps maintain the high standards of this translation management system.

## Project Overview

This is the **SPT Korean Localization Framework (SKLF)** repository - a comprehensive translation framework for SPT Tarkov BepInEx-based plugin mods. The project aims to provide complete and consistent Korean localization for all SPT mods, overcoming limitations of existing translation tools.

## Development Environment

- **Language**: C# (.NET Framework 4.7.2)
- **Framework**: BepInEx 5.4.23+
- **Patching**: HarmonyX 2.10+
- **UI**: ImGui.NET + UImGui
- **Data Format**: JSON (Newtonsoft.Json)
- **Build System**: MSBuild
- **Target Platform**: Windows 10/11 64-bit
- **Unity Compatibility**: 2020.3.x - 2021.3.x

## Architecture Overview

The framework follows a modular architecture:

```
Core Engine
├── Text Detection Module (Unity text framework hooking)
├── Translation Manager (JSON-based hierarchical translation)
├── Cache Manager (multi-level caching system)
└── Harmony Patch Manager (runtime patching)

Data Layer
├── File I/O Manager
├── JSON Parser
└── Database Interface

UI Components
├── ImGui Panel (in-game translation management)
├── Settings Manager
└── Debug Console

Integration Layer
├── BepInEx Plugin Interface
├── SPT API Connector
└── Mod Compatibility Layer
```

## Performance Requirements

- Initial loading time: < 2 seconds
- Translation lookup speed: < 1ms (cached)
- Memory usage: < 50MB
- FPS impact: < 1%

## Translation Data Structure

Translation files use hierarchical JSON structure:
```json
{
  "version": "1.0.0",
  "language": "ko_KR",
  "translations": {
    "ui": {
      "menu": { "start_game": "게임 시작" }
    },
    "items": {
      "weapon_ak74": "AK-74 소총"
    }
  }
}
```

## Key Technical Challenges

1. **Text Detection**: Support for all Unity text frameworks (UGUI, NGUI, TextMeshPro, IMGUI, TextMesh)
2. **Real-time Hooking**: Harmony patches for dynamic text interception
3. **Mod Compatibility**: Automatic analysis of mod-specific text processing patterns
4. **Performance Optimization**: Multi-level caching and lazy loading

## Development Phases

1. **Phase 1** (4 weeks): BepInEx plugin foundation + basic Harmony patching
2. **Phase 2** (6 weeks): Multi-framework text support + caching system
3. **Phase 3** (4 weeks): Mod compatibility + auto-update features
4. **Phase 4** (3 weeks): Community collaboration tools + web interface

## Critical Implementation Notes

- Must maintain compatibility with SPT-AKI 3.11.0+
- Avoid direct game file modification
- Minimize conflicts with other mods
- Support fallback mechanism (Korean → English → Original)
- Implement context-aware translation support