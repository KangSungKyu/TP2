# 비동기 마이그레이션 및 수명 주기 명세서 (Async Lifecycle)

## 개요
- 목표: UniTask 기반 비동기 흐름, CancellationToken 전파 규칙, UniTaskVoid/Forget 사용 정책을 표준화하여 메모리/객체 수명 문제를 예방한다.

## 핵심 인터페이스 (함수 시그니처)
- UniTask InitAsync(Action onComplete = null, CancellationToken cancellationToken = default)
- UniTask EnsureDataLoadedAsync()
- UniTask<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
- UniTaskVoid FireAndForgetSafe(Func<CancellationToken, UniTask> task, CancellationToken cancellationToken)

## 제약(Constraints)
- 절대 금지: public async void 메서드 사용(유닛테스트/예외 추적 불가). 예외: Unity 메시지 콜백 중 불가피한 경우에만 내부적으로 UniTaskVoid로 래핑하고 예외 로깅을 반드시 수행.
- UniTaskVoid / .Forget() 사용 규칙:
  - 사용 시 반드시 CancellationToken을 전달하거나 내부 try/catch로 예외를 완전 수집해야 함.
  - 로그 레벨: 예외 발생 시 Debug.LogException으로 출력하고 복구 루틴 실행
- CancellationToken 전파:
  - 씬/객체 소멸 시(GetCancellationTokenOnDestroy()) 토큰을 최상위부터 하위 콜에 전달
  - 장기 실행 작업은 외부 토큰 없이 실행하지 않음
- UniTask Delay/타이머는 게임 속도 의존성 여부를 명시:
  - 물리 관련 타이밍은 FixedDeltaTime 기반 루프에서 처리하고, Delay는 게임 속도(scale)와 무관하도록 설정 검토

## 마이그레이션 권장 패턴
- 기존 Task/async-await 사용부는 UniTask로 교체하되, API 경계(외부 라이브러리)와 상호운용을 위해 Task<T> 변환 유틸을 제공
- Fire-and-forget 패턴은 FireAndForgetSafe로 래핑하여 토큰과 예외 처리 일괄 관리
