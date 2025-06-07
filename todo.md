# SPT Korean Localization Framework (SKLF) 개발 Todo 목록

## 📋 개발 단계별 작업 목록

### Phase 1: 기반 구축 (4주) - 우선순위: 높음
- [x] **프로젝트 폴더 구조 설정** `setup-project-structure`
  - src, assets, docs, tests 디렉토리 생성
  - 기본 프로젝트 구조 설정

- [x] **C# 프로젝트 파일 생성** `create-csproj`
  - .csproj 파일 생성
  - 필수 NuGet 패키지 설정 (BepInEx, HarmonyX, Newtonsoft.Json, ImGui.NET)

- [x] **BepInEx 플러그인 기본 구조** `bepinex-plugin-base`
  - BaseUnityPlugin 상속 클래스 구현
  - 플러그인 메타데이터 설정
  - 플러그인 로딩/언로딩 로직

- [x] **HarmonyX 패치 시스템** `harmony-patch-system`
  - Harmony 인스턴스 초기화
  - 기본 텍스트 후킹 메커니즘 구현
  - 패치 적용/해제 관리

- [x] **JSON 번역 파일 파서** `json-parser`
  - JSON 파일 로더 구현
  - 계층적 번역 데이터 구조 정의
  - 파일 I/O 관리자 구현

### Phase 2: 핵심 기능 (6주) - 우선순위: 중간
- [x] **텍스트 인터셉터 구현** `text-interceptor`
  - TextInterceptor 클래스 구현
  - Unity 텍스트 감지 및 번역 적용
  - 컨텍스트 기반 번역 지원

- [x] **캐싱 시스템** `cache-manager`
  - 다단계 캐싱 구현 (메모리 + 디스크)
  - 지연 로딩 (Lazy Loading)
  - 캐시 무효화 및 갱신 로직

- [x] **번역 매니저** `translation-manager`
  - 계층적 번역 검색
  - 폴백 메커니즘 (한글 → 영어 → 원본)
  - 번역 우선순위 관리

- [x] **다중 Unity 텍스트 프레임워크 지원** `unity-text-frameworks`
  - UGUI Text 컴포넌트 지원
  - TextMeshPro 지원
  - IMGUI 텍스트 지원
  - Legacy NGUI 지원

- [x] **ImGui 관리 패널** `imgui-panel`
  - 인게임 번역 관리 UI
  - 실시간 번역 미리보기
  - 번역 누락 항목 리포팅

### Phase 3: 고급 기능 (4주) - 우선순위: 낮음
- [ ] **설정 관리 시스템** `settings-system`
  - 언어 전환 기능
  - 캐시 설정 관리
  - 사용자 환경설정 저장/로드

- [ ] **모드 호환성 매니저** `mod-compatibility`
  - 모드별 텍스트 처리 패턴 분석
  - 충돌 감지 및 해결
  - 의존성 관리

- [ ] **디버그 도구** `debug-tools`
  - 디버그 콘솔 구현
  - 로깅 시스템
  - 성능 모니터링 도구

- [ ] **성능 최적화** `performance-optimization`
  - 프로파일링 및 병목점 분석
  - 메모리 사용량 최적화
  - FPS 영향 최소화

### Phase 4: 테스트 및 품질 보증
- [ ] **테스트 프레임워크 구축** `testing-framework`
  - 단위 테스트 작성
  - 통합 테스트 구현
  - 자동화된 테스트 파이프라인

## 🎯 현재 작업 상태
**다음 작업**: Phase 2 - 텍스트 인터셉터 구현

## 📊 진행률
- **Phase 1**: 5/5 완료 (100%) ✅
- **Phase 2**: 0/5 완료 (0%)
- **Phase 3**: 0/4 완료 (0%)
- **Phase 4**: 0/1 완료 (0%)
- **전체**: 5/15 완료 (33%)

## 🔧 기술 스택
- **언어**: C# (.NET Framework 4.7.2)
- **프레임워크**: BepInEx 5.4.23+, HarmonyX 2.10+
- **UI**: ImGui.NET + UImGui
- **데이터**: JSON (Newtonsoft.Json)
- **빌드**: MSBuild

## 📋 성능 목표
- 초기 로딩 시간: < 2초
- 번역 검색 속도: < 1ms (캐시된 경우)
- 메모리 사용량: < 50MB
- FPS 영향: < 1%

## 🚀 마일스톤
- **M1** (4주): 기본 텍스트 후킹 작동
- **M2** (10주): 인게임 텍스트 번역 성공
- **M3** (14주): 전체 기능 완성
- **M4** (17주): 공개 베타 출시