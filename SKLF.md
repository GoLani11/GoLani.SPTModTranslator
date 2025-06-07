# SPT 타르코프 BepInEx 기반 플러그인 모드 한글화 시스템 PRD
*Product Requirements Document v1.0*

## 1. 프로젝트 개요

### 1.1 프로젝트 명칭
**SPT Korean Localization Framework (SKLF)**  
*SPT 타르코프 한글화 통합 프레임워크*

### 1.2 프로젝트 목표
SPT 타르코프(Single Player Tarkov)의 모든 BepInEx 기반 플러그인 모드들에 대한 완전하고 일관된 한글화를 제공하는 범용 번역 프레임워크 개발. 기존 GoLani.SPTModTranslator의 한계를 극복하고 인게임 텍스트까지 완벽하게 번역되는 시스템 구축.

### 1.3 프로젝트 범위
- **포함 범위**:
  - BepInEx 기반 클라이언트 사이드 모드 번역
  - 서버 사이드 모드 로케일 데이터 처리
  - F12 Configuration Manager 및 인게임 UI 텍스트 번역
  - 동적 생성 텍스트 실시간 번역
  - 커뮤니티 협업 번역 도구
  
- **제외 범위**:
  - 게임 원본 파일 직접 수정
  - 음성/이미지 번역
  - 자동 기계 번역 (수동 번역만 지원)

### 1.4 핵심 가치 제안
1. **완전성**: F12 메뉴부터 인게임 텍스트까지 모든 UI 요소 번역
2. **호환성**: 다양한 모드 구조에 적응하는 범용 시스템
3. **확장성**: 새로운 모드 쉽게 추가 가능
4. **커뮤니티**: 협업 번역 환경 제공
5. **성능**: 게임 성능에 미치는 영향 최소화

## 2. 기능 요구사항

### 2.1 핵심 기능

#### 2.1.1 텍스트 감지 및 후킹
- Unity의 모든 텍스트 프레임워크 지원 (UGUI, NGUI, TextMeshPro, IMGUI, TextMesh)
- Harmony 패치를 통한 실시간 텍스트 가로채기
- 동적 생성 텍스트 자동 감지 및 번역 적용

#### 2.1.2 번역 데이터 관리
- JSON 기반 계층적 번역 파일 구조
- 모드별 독립적인 번역 파일 관리
- 폴백 메커니즘 (한글 → 영어 → 원본)
- 컨텍스트 기반 번역 지원

#### 2.1.3 사용자 인터페이스
- 인게임 번역 관리 패널 (ImGui 기반)
- 실시간 번역 미리보기
- 언어 전환 기능
- 번역 누락 항목 리포팅

#### 2.1.4 캐싱 및 최적화
- 다단계 캐싱 시스템 (메모리 + 디스크)
- 지연 로딩 (Lazy Loading)
- 우선순위 기반 번역 처리

### 2.2 고급 기능

#### 2.2.1 모드 호환성 매니저
- 모드별 텍스트 처리 패턴 자동 분석
- 충돌 감지 및 해결
- 의존성 관리

#### 2.2.2 번역 협업 도구
- 웹 기반 번역 인터페이스
- 버전 관리 시스템 통합
- 번역 검토 및 승인 프로세스

#### 2.2.3 자동 업데이트
- 번역 파일 자동 다운로드
- 게임/모드 버전별 호환성 체크
- 증분 업데이트 지원

## 3. 기술적 요구사항

### 3.1 개발 환경
- **언어**: C# (.NET Framework 4.7.2)
- **프레임워크**: BepInEx 5.4.23+
- **패치 라이브러리**: HarmonyX 2.10+
- **UI**: ImGui.NET + UImGui
- **데이터 형식**: JSON (Newtonsoft.Json)

### 3.2 성능 요구사항
- **초기 로딩 시간**: < 2초
- **번역 검색 속도**: < 1ms (캐시된 경우)
- **메모리 사용량**: < 50MB
- **FPS 영향**: < 1%

### 3.3 호환성 요구사항
- **SPT-AKI 버전**: 3.11.0 이상
- **Unity 버전**: 2020.3.x - 2021.3.x
- **운영체제**: Windows 10/11 64비트
- **다른 모드와의 충돌**: 최소화

## 4. 시스템 아키텍처

### 4.1 전체 구조
```
SPT Korean Localization Framework
├── Core Engine
│   ├── Text Detection Module
│   ├── Translation Manager
│   ├── Cache Manager
│   └── Harmony Patch Manager
├── Data Layer
│   ├── File I/O Manager
│   ├── JSON Parser
│   └── Database Interface
├── UI Components
│   ├── ImGui Panel
│   ├── Settings Manager
│   └── Debug Console
└── Integration Layer
    ├── BepInEx Plugin Interface
    ├── SPT API Connector
    └── Mod Compatibility Layer
```

### 4.2 핵심 컴포넌트 설계

#### 4.2.1 Text Interceptor
```csharp
public class TextInterceptor : ITextProcessor
{
    private readonly ITranslationProvider translationProvider;
    private readonly ICacheManager cacheManager;
    
    public string ProcessText(string originalText, TextContext context)
    {
        // 캐시 확인
        if (cacheManager.TryGetTranslation(originalText, out string cached))
            return cached;
            
        // 번역 검색
        var translation = translationProvider.GetTranslation(originalText, context);
        
        // 캐시 저장
        cacheManager.Store(originalText, translation);
        
        return translation;
    }
}
```

#### 4.2.2 번역 데이터 구조
```json
{
  "version": "1.0.0",
  "language": "ko_KR",
  "translations": {
    "ui": {
      "menu": {
        "start_game": "게임 시작",
        "settings": "설정",
        "exit": "종료"
      }
    },
    "items": {
      "weapon_ak74": "AK-74 소총",
      "ammo_545x39": "5.45x39mm 탄약"
    }
  }
}
```

### 4.3 데이터 흐름
1. **텍스트 감지**: Harmony 패치가 게임 내 텍스트 렌더링 감지
2. **번역 요청**: Translation Manager에 번역 요청
3. **캐시 확인**: 메모리/디스크 캐시 순차 검색
4. **번역 적용**: 찾은 번역을 UI에 적용
5. **폴백 처리**: 번역 없을 시 원본 텍스트 유지

## 5. 개발 계획

### 5.1 개발 단계

#### Phase 1: 기반 구축 (4주)
- [ ] BepInEx 플러그인 기본 구조 개발
- [ ] Harmony 패치 시스템 구현
- [ ] 기본 텍스트 후킹 메커니즘
- [ ] JSON 번역 파일 파서

#### Phase 2: 핵심 기능 (6주)
- [ ] 다중 텍스트 프레임워크 지원
- [ ] 캐싱 시스템 구현
- [ ] 번역 매니저 개발
- [ ] 기본 UI 패널 제작

#### Phase 3: 고급 기능 (4주)
- [ ] 모드 호환성 시스템
- [ ] 자동 업데이트 기능
- [ ] 성능 최적화
- [ ] 디버그 도구

#### Phase 4: 커뮤니티 도구 (3주)
- [ ] 웹 번역 인터페이스
- [ ] GitHub 통합
- [ ] 문서화
- [ ] 배포 시스템

### 5.2 마일스톤
- **M1** (4주): 기본 텍스트 후킹 작동
- **M2** (10주): 인게임 텍스트 번역 성공
- **M3** (14주): 전체 기능 완성
- **M4** (17주): 공개 베타 출시

## 6. 위험 요소 및 대응 방안

### 6.1 기술적 위험
| 위험 요소 | 영향도 | 발생 가능성 | 대응 방안 |
|---------|--------|------------|----------|
| Unity IL2CPP 변환 | 높음 | 중간 | Mono 버전 타겟팅, 대체 후킹 방법 준비 |
| SPT 업데이트로 인한 호환성 | 중간 | 높음 | 버전별 호환성 매트릭스 관리 |
| 성능 저하 | 중간 | 중간 | 프로파일링 도구 활용, 최적화 지속 |
| 텍스트 렌더링 다양성 | 높음 | 높음 | 포괄적 텍스트 감지 알고리즘 개발 |

### 6.2 프로젝트 위험
| 위험 요소 | 대응 방안 |
|---------|----------|
| 개발자 리소스 부족 | 오픈소스 기여자 모집, 모듈화 설계로 분산 개발 가능 |
| 번역자 참여 부족 | 사용하기 쉬운 도구 제공, 기여자 인정 시스템 |
| 라이선스 문제 | MIT 라이선스 채택, 의존성 라이선스 검토 |

## 7. 성공 지표

### 7.1 기술적 지표
- 텍스트 번역 커버리지: > 95%
- 평균 응답 시간: < 1ms
- 메모리 사용량: < 50MB
- 크래시 발생률: < 0.1%

### 7.2 사용자 지표
- 월간 활성 사용자: > 1,000명
- 번역 기여자 수: > 50명
- 지원 모드 수: > 100개
- 사용자 만족도: > 4.5/5

## 8. 부록

### 8.1 기술 스택 요약
- **Core**: C#, .NET Framework 4.7.2
- **Framework**: BepInEx 5.4.23, HarmonyX 2.10
- **UI**: ImGui.NET
- **Data**: JSON (Newtonsoft.Json)
- **Build**: MSBuild, GitHub Actions
- **Collaboration**: Crowdin/Weblate

### 8.2 참고 자료
- BepInEx 공식 문서
- Harmony 패칭 가이드
- Unity Localization 베스트 프랙티스
- SPT-AKI 모딩 문서

### 8.3 용어 정의
- **SPT**: Single Player Tarkov
- **BepInEx**: Unity 게임 모딩 프레임워크
- **Harmony**: .NET 런타임 패칭 라이브러리
- **SKLF**: SPT Korean Localization Framework