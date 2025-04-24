# GoLani SPT Mod Translator

---

**GoLani SPT Mod Translator**는 SPTarkov(Single Player Tarkov)의 다양한 BepInEx 플러그인 모드에 한국어 번역을 쉽게 적용할 수 있는 플러그인입니다.

---

## 목차

1. [주요 기능](#주요-기능)
2. [폴더 구조](#폴더-구조)
3. [설치 및 사용법](#설치-및-사용법)
4. [번역 파일/패치 정의 예시](#번역-파일패치-정의-예시)
5. [입문자를 위한 번역 추가 가이드](#입문자를-위한-번역-추가-가이드)
6. [개발 및 빌드](#개발-및-빌드)

---

## 주요 기능

- **원본 모드 수정 없이** 한국어 번역 적용
- Harmony 라이브러리를 이용한 **동적 런타임 패치**
- **JSON 파일**로 번역 및 패치 정의
- 여러 모드 **동시 지원**
- F12 설정 메뉴(BepInEx ConfigurationManager)에서 모드 전체 활성화/비활성화 및 언어 선택 가능
- 설정 메뉴 자체 한글화 지원

---

## 폴더 구조

```
/
├─ KoreanPatcher.sln         # Visual Studio 솔루션 파일
├─ KoreanPatcher.csproj      # C# 프로젝트 파일
├─ Core/                     # 핵심 로직 (패치, 번역 등)
├─ Models/                   # 데이터 모델 (PatchDefinition)
├─ translations/             # 번역 데이터 폴더
│  ├─ SomeMod_ko.json
│  └─ ...
├─ patch_definitions/        # 패치 정의 폴더
│  ├─ SomeMod_patches.json
│  └─ ...
├─ KoreanPatcher.cs          # 메인 플러그인 클래스
└─ BepInEx/                  # (게임 설치 경로)
   └─ plugins/
      └─ MyKoreanPatcher/
         ├─ KoreanPatcher.dll
         ├─ translations/
         └─ patch_definitions/
```

---

## 설치 및 사용법

1. **빌드:**
   - `KoreanPatcher.sln`을 Visual Studio에서 열고 빌드합니다.
2. **복사:**
   - 빌드 결과물(`KoreanPatcher.dll`)과 `translations/`, `patch_definitions/` 폴더를
     `BepInEx/plugins/MyKoreanPatcher/` 폴더에 복사합니다.
3. **실행:**
   - SPTarkov를 실행합니다.
4. **설정:**
   - 게임 내에서 F12 키를 눌러 ConfigurationManager 메뉴를 엽니다.
   - "모드 활성화" 및 "언어"를 원하는 대로 설정하세요.
   - **설정 변경 후에는 F12 메뉴를 껐다가 다시 여세요!**

---

## 번역 파일/패치 정의 예시

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
    "Enabled": true,
    "TargetAssembly": "TargetModAssemblyName",
    "TargetType": "TargetMod.Namespace.ClassName",
    "TargetMethod": "MethodNameToPatch",
    "PatchType": "PostfixReturnString",
    "TranslationModID": "ModName"
  },
  {
    "Enabled": true,
    "TargetType": "Another.Class.Name",
    "TargetMethod": "MethodWithRefStringParam",
    "PatchType": "PrefixRefStringParameter",
    "ParameterIndex": 0,
    "TranslationModID": "ModName"
  }
]
```

#### PatchType 종류
- `PostfixReturnString`: string 반환값 번역
- `PrefixRefStringParameter`: ref/out string 파라미터 번역 (ParameterIndex 필요)
- `PrefixStringParameter`: 일반 string 파라미터 번역 (ParameterIndex 필요)

#### BepInEx ConfigurationManager 설정 번역용 패치 예시
- `BepInEx.Configuration.ConfigDefinition.get_Section`
- `BepInEx.Configuration.ConfigDefinition.get_Key`
- `BepInEx.Configuration.ConfigDescription.get_Description`
- `System.ComponentModel.DescriptionAttribute.get_Description`
- `System.Enum.ToString`

---

## 입문자를 위한 번역 추가 가이드

### 1. 번역 파일 만들기

1. `translations/ko/` 폴더로 이동합니다.
2. 새 파일을 만듭니다. 파일 이름은 반드시 `[모드이름]_ko.json` (예: `ExampleMod_ko.json`)
3. 아래와 같이 영어와 한글을 "영어": "한글" 형태로 적어줍니다.

```json
{
  "Save": "저장",
  "Cancel": "취소",
  "Settings": "설정",
  "Original English Text": "예시 한글 번역"
}
```
- "Save"는 게임/모드에서 실제로 나오는 영어 단어나 문장입니다.
- "저장"은 여러분이 원하는 한글 번역입니다.

### 2. 패치 정의 파일 만들기

1. `patch_definitions/` 폴더로 이동합니다.
2. 새 파일을 만듭니다. 파일 이름은 반드시 `[모드이름]_patches.json` (예: `ExampleMod_patches.json`)
3. 아래와 같이 작성합니다.

```json
[
  {
    "Enabled": true,
    "TargetAssembly": "ExampleMod", // 번역할 모드의 DLL 이름(확장자 .dll 빼고)
    "TargetType": "ExampleMod.UI.ExampleUI", // 번역할 클래스 전체 이름
    "TargetMethod": "GetButtonText", // 번역할 메소드 이름
    "PatchType": "PostfixReturnString", // 반환값(string) 번역
    "TranslationModID": "ExampleMod" // 위에서 만든 번역 파일명 앞부분과 같아야 함
  },
  {
    "Enabled": true,
    "TargetAssembly": "ExampleMod",
    "TargetType": "ExampleMod.Core.ExampleLogic",
    "TargetMethod": "ShowMessage",
    "PatchType": "PrefixStringParameter", // 파라미터 번역
    "ParameterIndex": 0, // 번역할 string 파라미터 인덱스(0부터)
    "TranslationModID": "ExampleMod"
  }
]
```

#### 각 항목 설명
- `TargetAssembly`: 번역할 모드의 DLL 파일 이름(확장자 .dll은 빼고)
- `TargetType`: 번역할 클래스의 전체 이름(네임스페이스 포함)
- `TargetMethod`: 번역할 함수(메소드) 이름
- `PatchType`: 번역 방식
  - `PostfixReturnString`: 함수의 반환값(string)을 번역
  - `PrefixStringParameter`: 함수의 파라미터(string)를 번역
- `ParameterIndex`: 번역할 파라미터가 여러 개일 때, 몇 번째(string)인지(0부터 시작)
- `TranslationModID`: 번역 파일명 앞부분(예: ExampleMod_ko.json → ExampleMod)

### 3. 번역 적용하기

1. 위 두 파일을 각각 폴더에 넣고 게임을 실행합니다.
2. F12를 눌러 모드가 활성화되어 있는지 확인합니다.
3. 게임 내에서 번역이 적용되는지 확인합니다.

### 💡 추가 팁

- 번역하고 싶은 영어 문장은 게임에서 직접 복사하거나, 기존 번역 파일을 참고하면 됩니다.
- 번역이 바로 적용되지 않으면 F12 메뉴를 껐다가 다시 켜보세요.

---

## 개발 및 빌드

- Visual Studio 2019 이상 (.NET Framework 4.7.2 타겟)
- 필요 라이브러리 (NuGet 또는 직접 참조):
    - `BepInEx.Core`
    - `BepInEx.PluginInfoProps`
    - `UnityEngine.CoreModule`
    - `0Harmony`
    - `Newtonsoft.Json`

솔루션을 열고 빌드하면 `BepInEx/plugins/MyKoreanPatcher/` 폴더에 DLL이 생성됩니다.

---