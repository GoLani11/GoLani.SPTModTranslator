# SPT-Mod-Locale-Manager

SPTarkov(Single Player Tarkov)의 다양한 BepInEx 플러그인 모드들에 번역을 적용하는 BepInEx 플러그인입니다.

## 주요 기능

*   **원본 모드 수정 없이** 한국어 번역 적용
*   Harmony 라이브러리를 이용한 **동적 런타임 패치**
*   **JSON 설정 파일** 기반
    *   번역할 모드, 클래스, 메소드, 패치 방식 정의 (`patch_definitions/*.json`)
    *   실제 번역 데이터 정의 (`translations/*_ko.json`)
*   여러 모드 **동시 지원**
*   F12 설정 메뉴 (BepInEx ConfigurationManager) 지원
    *   모드별 번역 **활성화/비활성화** 토글
    *   설정 메뉴 자체 한글화 (Section, Key, Description 등)

## 폴더 구조 (개발 및 사용)

```
/
├─ KoreanPatcher.sln         # Visual Studio 솔루션 파일
├─ KoreanPatcher.csproj      # C# 프로젝트 파일 (빌드 시 BepInEx/plugins/MyKoreanPatcher로 출력)
├─ Core/                     # 핵심 로직 (패치, 번역, 설정 서비스)
├─ Models/                   # 데이터 모델 (PatchDefinition)
├─ translations/             # 번역 데이터 폴더
│  ├─ SomeMod_ko.json
│  └─ ...
├─ patch_definitions/        # 패치 정의 폴더
│  ├─ SomeMod_patches.json
│  └─ ...
├─ KoreanPatcher.cs          # 메인 플러그인 클래스 (서비스 오케스트레이션)
├─ .gitignore                # Git 제외 파일 목록
└─ BepInEx/                  # (게임 설치 경로에 있는 BepInEx 폴더)
   └─ plugins/
      └─ MyKoreanPatcher/    # <-- 빌드 결과물(DLL) 및 실행 시 필요한 JSON 복사 위치
         ├─ KoreanPatcher.dll
         ├─ translations/     # (빌드 후 복사 또는 직접 생성/복사)
         └─ patch_definitions/  # (빌드 후 복사 또는 직접 생성/복사)
```

## 사용 방법

1.  **빌드:** `KoreanPatcher.sln` 파일을 Visual Studio에서 열고 빌드합니다.
2.  **배포:**
    *   빌드 결과물인 `KoreanPatcher.dll` 파일이 `BepInEx/plugins/MyKoreanPatcher/` 폴더에 생성됩니다.
    *   개발 시 사용한 `translations/` 폴더와 `patch_definitions/` 폴더를 **내용물과 함께** `BepInEx/plugins/MyKoreanPatcher/` 폴더 안으로 복사합니다.
3.  **게임 실행:** SPTarkov를 실행합니다.
4.  **설정 (선택):** 게임 내에서 F12 키를 눌러 BepInEx ConfigurationManager 메뉴를 열고, 각 모드별 번역 활성화 여부를 설정할 수 있습니다.

## JSON 파일 형식

### 번역 데이터 (`translations/ModName_ko.json`)

```json
{
  "Original English Text": "번역된 한국어 텍스트",
  "Save": "저장"
}
```

### 패치 정의 (`patch_definitions/ModName_patches.json`)

```json
[
  {
    "Enabled": true, // 이 패치 정의를 사용할지 여부
    "TargetAssembly": "TargetModAssemblyName", // 대상 모드의 DLL 이름 (선택 사항, 없으면 전체에서 검색)
    "TargetType": "TargetMod.Namespace.ClassName", // 패치할 클래스 전체 이름
    "TargetMethod": "MethodNameToPatch", // 패치할 메소드 이름
    "PatchType": "PostfixReturnString", // 사용할 패치 핸들러 종류 (아래 참조)
    "TranslationModID": "ModName" // 사용할 translations/ModName_ko.json 파일 지정
  },
  {
    "Enabled": true,
    "TargetType": "Another.Class.Name",
    "TargetMethod": "MethodWithRefStringParam",
    "PatchType": "PrefixRefStringParameter",
    "ParameterIndex": 0, // 패치할 파라미터 인덱스 (0부터 시작)
    "TranslationModID": "ModName"
  }
  // ... 필요한 만큼 추가
]
```

**`PatchType` 종류:**

*   `PostfixReturnString`: `string`을 반환하는 메소드의 반환값을 번역합니다.
*   `PrefixRefStringParameter`: `ref string` 또는 `out string` 파라미터를 번역합니다. `ParameterIndex` 필요.
*   `PrefixStringParameter`: 일반 `string` 파라미터를 번역합니다. `ParameterIndex` 필요.

**BepInEx ConfigurationManager 설정 번역용 패치:**

*   `BepInEx.Configuration.ConfigDefinition.get_Section`
*   `BepInEx.Configuration.ConfigDefinition.get_Key`
*   `BepInEx.Configuration.ConfigDescription.get_Description`
*   `System.ComponentModel.DescriptionAttribute.get_Description`
*   `System.Enum.ToString`

(위 항목들은 `PostfixReturnString` 패치를 사용합니다)

## 개발 및 빌드

*   Visual Studio 2019 이상 (.NET Framework 4.7.2 타겟)
*   필요 라이브러리 (NuGet 또는 직접 참조):
    *   `BepInEx.Core`
    *   `BepInEx.PluginInfoProps`
    *   `UnityEngine.CoreModule`
    *   `0Harmony`
    *   `Newtonsoft.Json`

솔루션을 열고 빌드하면 `BepInEx/plugins/MyKoreanPatcher/` 폴더에 DLL이 생성됩니다.

## 라이선스

(라이선스 정보를 여기에 추가하세요. 예: MIT License) 