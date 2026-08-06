# Init to Hub Boot Contract

- `InitScene.nextScene` defaults to and is serialized as `GameSceneManager.SceneName.Hub` (`1`).
- Boot keeps the existing single `Start -> TransitionTo(nextScene)` call.
- Build Settings order is enabled `InitScene`, `LoadingScene`, `HubScene`, `MainScene`; `InitScene` is first.
- Hub contains no Player and its Stage 1 button assigns stage idx `9001` before transitioning to Main.
- Main reuses `EnsureStageLoadedAsync(9001)`, whose entry load remains resource idx `1040`.
- Player death and Garon completion continue to use the shared Hub return path.

Verification baseline (2026-08-06): EditMode 86/86, PlayMode 1/1, QATestRunner 66/66.
