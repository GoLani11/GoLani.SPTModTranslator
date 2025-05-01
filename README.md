# GoLani SPT Mod Translator

> English README is available here: [README_EN.md](./README_EN.md)

---

**GoLani SPT Mod Translator**는 SPTarkov(싱글플레이어 타르코프) 게임의 다양한 모드(플러그인)에 여러 언어 번역을 쉽게 적용할 수 있도록 도와주는 도구입니다. 별도의 프로그래밍 지식 없이도 번역 파일만 추가하면, 게임 내에서 한글이나 원하는 언어로 모드의 텍스트를 볼 수 있습니다.

---

## 이 플러그인은 어떤 기능을 하나요?

- 원본 모드를 수정하지 않고도 번역을 적용할 수 있습니다.
- 여러 모드의 번역을 동시에 지원합니다.
- 번역 파일과 번역 규칙(패치 정의)을 간단한 텍스트 파일(JSON)로 관리합니다.
- 게임 내에서 F12 키로 설정 메뉴를 열어, 번역 언어와 모드 활성화 여부를 쉽게 바꿀 수 있습니다.
- 번역되지 않은 문장을 자동으로 추출해주는 기능이 있습니다.
- 중복된 번역 규칙(패치 정의)은 자동으로 걸러집니다.

---

## 폴더 구조는 어떻게 되나요?

```
프로젝트 폴더/
├─ translations/           # 번역 파일이 들어가는 곳
│  ├─ ko/                  # 한국어 번역 폴더
│  │   ├─ ExampleMod.json  # 예시: ExampleMod 모드의 한국어 번역
│  ├─ ja/                  # 일본어 번역 폴더
│  │   ├─ ExampleMod.json
│  └─ ...
├─ patch_definitions/      # 번역 규칙(패치 정의) 파일이 들어가는 곳
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

## 설치 방법

1. **플러그인 모드 다운로드:**
   - 최신 릴리즈에서 공유중인 GoLani.SPTModTranslator.zip 을 받고 압축을 해제합니다.
2. **모드 적용:**
   - `SPT 설치 폴더\BepInEx\plugins\GoLani.SPTModTranslator\translations` 내에 
      해당하는 언어 폴더에 `모드 이름.json` 을 넣어둡니다.
3. **실행:**
   - SPTarkov 게임을 실행합니다.
4. **설정:**
   - 게임 안에서 F12 키를 눌러 설정 메뉴를 엽니다.
   - "모드 활성화"와 "언어"를 원하는 대로 선택하세요.
   - 설정을 바꾼 뒤에는 F12 메뉴를 껐다가 다시 여는 것이 좋습니다.

---

## 번역 파일은 어떻게 만드나요?

1. `translations` 폴더 안에 원하는 언어 폴더(예: `ko`, `ja`, `en`)로 들어갑니다.
2. 새 파일을 만듭니다. 파일 이름은 `[모드이름].json` (예: `ExampleMod.json`)
3. 아래와 같이 실제 게임에서 나오는 영어 문장과 번역할 문장을 한 쌍씩 적어줍니다.

```json
{
  "Save": "저장",
  "Cancel": "취소",
  "Settings": "설정",
  "Original English Text": "예시 번역"
}
```
- 왼쪽(영어)은 게임/모드에서 실제로 나오는 문장입니다.
- 오른쪽(한글 등)은 여러분이 원하는 번역입니다.

---

## 패치 정의 파일은 언제, 왜 만들어야 하나요?

> **중요:**
> - 게임 내 F12로 열 수 있는 설정 메뉴(설정명, 설명 등)는 플러그인에서 자동으로 번역이 적용됩니다.
> - **따라서, 대부분의 경우 번역 파일만 만들면 됩니다!**
> - 아래의 "패치 정의 파일"은 각 모드의 고유 UI, 대화창, 버튼, 팝업 등 "설정 메뉴 이외"의 부분을 번역하고 싶을 때만 필요합니다.

### 패치 정의 파일 만드는 방법

1. `patch_definitions` 폴더로 이동합니다.
2. 새 파일을 만듭니다. 파일 이름은 `[모드이름]_patches.json` (예: `ExampleMod_patches.json`)
3. 아래와 같이 작성합니다.

```json
[
  {
    "Enabled": true,
    "TargetAssembly": "ExampleMod", // 번역할 모드의 DLL 이름(확장자 .dll 빼고)
    "TargetType": "ExampleMod.UI.ExampleUI", // 번역할 클래스 전체 이름
    "TargetMethod": "GetButtonText", // 번역할 함수(메소드) 이름
    "PatchType": "PostfixReturnString", // 반환값(string) 번역
    "TranslationModID": "ExampleMod" // 번역 파일명 앞부분과 같아야 함
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

#### 주요 항목 설명
- `TargetAssembly`: 번역할 모드의 DLL 파일 이름(확장자 .dll은 빼고)
- `TargetType`: 번역할 클래스의 전체 이름(네임스페이스 포함)
- `TargetMethod`: 번역할 함수(메소드) 이름
- `PatchType`: 번역 방식 (아래 참고)
- `ParameterIndex`: 번역할 파라미터가 여러 개일 때, 몇 번째(string)인지(0부터 시작)
- `TranslationModID`: 번역 파일명 앞부분(예: ExampleMod.json → ExampleMod)

#### PatchType 종류
- `PostfixReturnString`: 함수의 반환값(string)을 번역
- `PrefixRefStringParameter`: ref/out string 파라미터 번역 (ParameterIndex 필요)
- `PrefixStringParameter`: 일반 string 파라미터 번역 (ParameterIndex 필요)

---

## 번역 적용 및 확인 방법

1. 번역 파일(`translations/ko/ExampleMod.json` 등)과 필요한 경우 패치 정의 파일(`patch_definitions/ExampleMod_patches.json`)을 준비합니다.
2. 게임을 실행하고, F12로 설정 메뉴를 열어 모드가 활성화되어 있는지 확인합니다.
3. 게임 내에서 번역이 잘 적용되는지 확인합니다.

---

## 자주 묻는 질문(FAQ)

**Q. 번역 파일만 만들면 되나요?**
- 네, 대부분의 경우(설정 메뉴 등)는 번역 파일만 있으면 됩니다. 각 모드의 고유 UI/텍스트를 번역하고 싶을 때만 패치 정의 파일이 필요합니다.

**Q. 번역이 바로 적용되지 않아요!**
- F12 메뉴를 껐다가 다시 열어보세요. 그래도 안 되면 게임을 재시작해보세요.

**Q. 번역되지 않은 문장은 어떻게 찾나요?**
- F12 설정에서 "미번역 문자열 추출"을 체크하면, 게임을 탐색하는 동안 번역되지 않은 문장이 자동으로 추출됩니다. 추출이 끝나면 untranslations 폴더에서 확인할 수 있습니다.

**Q. 해상도나 화면 크기와 같은 동적 값이 포함된 텍스트는 어떻게 번역하나요?**
- v1.1.0부터 동적 값이 포함된 문자열 (예: "Maximum Width (1920p)")을 표준화하여 처리하는 기능이 추가되었습니다. 번역 파일에는 `"Maximum Width ({RESOLUTION}p)": "최대 너비 ({RESOLUTION}p)"` 형식으로 추가하면 됩니다. 실제 해상도 값은 자동으로 대체됩니다.

**Q. 여러 언어를 동시에 지원할 수 있나요?**
- 네! `translations/ko/`, `translations/ja/`, `translations/en/` 등 원하는 언어 폴더에 번역 파일을 추가하면, F12 메뉴에서 언어를 바꿔가며 사용할 수 있습니다.

---

## 개발 및 빌드(개발자용)

- Visual Studio 2019 이상 (.NET Framework 4.7.2)
- NuGet 또는 직접 참조:
    - `BepInEx.Core`
    - `BepInEx.PluginInfoProps`
    - `UnityEngine.CoreModule`
    - `0Harmony`
    - `Newtonsoft.Json`

---

## AI 지원
- 이 프로젝트는 개발 과정에서 Cursor AI의 도움을 받아 작성되었습니다.