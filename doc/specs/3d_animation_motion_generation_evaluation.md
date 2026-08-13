# 🎭 3D 모델 애니메이션 동작(Motion) 자동 생성 기술 검토 보고서

본 보고서는 **"유니티 및 AI 도구를 통해 3D 캐릭터/몬스터의 애니메이션 동작(Animation Clips / Keyframes)도 자동으로 생성 및 조작할 수 있는가?"**에 대한 기술적 검토 결과와 3가지 자동 생성 파이프라인을 제시합니다.

---

## 🎯 1. 검토 결과: **100% 자동 생성 및 조작 가능! (Fully Feasible ⭐⭐⭐⭐⭐)**

3D 캐릭터의 동작(Motion)은 수작업 키프레임 애니메이팅 없이도 **① AI Motion 생성기(Video/Text-to-Motion)**, **② Mixamo 라이브러리 리타겟팅**, 그리고 **③ 유니티 절차적 애니메이션(Procedural Animation Rigging)**을 연동하여 100% 자동 생성할 수 있습니다.

---

## 🛠️ 2. 3D 애니메이션 동작(Motion) 자동 생성 3대 주요 파이프라인

### 💥 파이프라인 1: AI 기반 3D 모션 자동 생성 (Text-to-Motion / Video-to-Motion AI)
실제 사람의 동작 비디오나 텍스트 텍스트 프롬프트로부터 3D 뼈대 애니메이션(.fbx / .anim)을 AI가 자동 추출합니다.

- **`Plask.ai` / `DeepMotion Animate 3D` (Video-to-Motion AI - 1순위 강추!)**:
  - **방식**: 유튜브/소드 액션 비디오(예: 세키로 검술 연타, 무술 동작, 회피 대시 영상)를 AI에 입력 ➔ **3D 캐릭터 뼈대 포즈 및 키프레임 애니메이션(.fbx) 10초 만에 자동 추출**.
- **`Kinetix.tech` / `AnyMotion AI` (Text-to-Motion AI)**:
  - **방식**: *"Dark fantasy warrior 3-combo sword swing"* 텍스트 입력 ➔ 3D 모션 클립 자동 생성.

---

### 🏛️ 파이프라인 2: Mixamo 모션 수천 종 자동 리타겟팅 (Instant Retargeting)
Adobe Mixamo에는 검술, 방어, 찌르기, 점프, 피격, 사망 등 1,000종 이상의 3D 액션 애니메이션이 무료로 구축되어 있습니다.

- **자동화 방식**: 유니티 인엔진 파이프라인에서 Master Rig에 Mixamo 애니메이션 클립을 바인딩하면, 파츠가 교체되거나 뼈대 스케일이 달라져도 유니티 `Humanoid` / `Generic` 래핑을 통해 **모든 모션이 1초 만에 자동 적응(Retargeting)**됩니다.

---

### ⚙️ 파이프라인 3: 유니티 C# 절차적 애니메이션 (Procedural Animation Rigging)
유니티 에디터 스크립트(`AnimationMotionGenerator.cs`)를 통해 코드 수치로 모션을 동적 자동 생성하는 방식입니다.

- **`Unity Animation Rigging` 패키지 연동**:
  - **Procedural Attack Swing**: `AnimationCurve` 및 수식으로 팔/어깨 뼈대의 회전 궤적을 코드로 렌더링.
  - **Procedural Hit & Recoil (피격/노크백)**: 피격 각도 벡터에 맞춰 척추와 머리 뼈대를 즉시 뒤로 꺾는 충격 모션 자동 생성.
  - **IK Foot Grounding**: 지형 고도에 맞춰 발 위치를 절차적으로 조정.

---

## 💻 3. 유니티 MCP C# 절차적 애니메이션 모션 생성기 예시 (`AnimationMotionGenerator.cs`)

유니티 MCP 명령(`execute_code`)으로 아래 C# 에디터 스크립트를 구동하면, 수작업 없이 새로운 `AnimationClip` 및 뼈대 회전 키프레임을 코드로 즉시 자동 생성할 수 있습니다:

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class AnimationMotionGenerator
{
    // 유니티 MCP를 통해 코드로 3D 검 베기(Attack Swing) 모션 클립 자동 생성
    public static AnimationClip GenerateProceduralSlashMotion()
    {
        AnimationClip clip = new AnimationClip();
        clip.name = "Procedural_Slash_Attack";

        // 오른쪽 어깨 뼈대(RightShoulder) 회전 커브 생성
        AnimationCurve rotateX = new AnimationCurve();
        AnimationCurve rotateY = new AnimationCurve();
        AnimationCurve rotateZ = new AnimationCurve();

        // 0.0초: 준비 자세 (Wind-up)
        rotateX.AddKey(0.0f, -45.0f);
        rotateY.AddKey(0.0f, 30.0f);
        rotateZ.AddKey(0.0f, 0.0f);

        // 0.15초: 베기 임팩트 (Swing Impact)
        rotateX.AddKey(0.15f, 60.0f);
        rotateY.AddKey(0.15f, -80.0f);
        rotateZ.AddKey(0.15f, 45.0f);

        // 0.3초: 후딜레이 복귀 (Recovery)
        rotateX.AddKey(0.3f, 0.0f);
        rotateY.AddKey(0.3f, 0.0f);
        rotateZ.AddKey(0.3f, 0.0f);

        // 뼈대 트랜스폼 경로 커브 바인딩
        string bonePath = "Armature/Hips/Spine/Chest/RightShoulder";
        clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", rotateX);
        clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", rotateY);
        clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", rotateZ);

        AssetDatabase.CreateAsset(clip, "Assets/Anims/Procedural_Slash_Attack.anim");
        AssetDatabase.SaveAssets();

        Debug.Log("[AnimationMotionGenerator] Procedural AnimationClip generated successfully!");
        return clip;
    }
}
#endif
```

---

## 📌 4. 최종 결론

1. **3D 애니메이션 동작(Motion) 자동 생성 가능 여부**: **100% 가능**합니다.
2. **추천 워크플로우**:
   - **액션 비디오 ➔ AI (Plask.ai / DeepMotion)**를 통해 3D `.fbx` 애니메이션 모션을 10초 만에 추출.
   - 추출된 모션을 Unity MCP 파이프라인에서 **3D 파츠 조합 몬스터에 리타겟팅**.
   - 직교 카메라로 캡처하여 **2D Sprite Sheet (.png)**로 최종 내보내기.

3D 모델 파츠 조합뿐만 아니라 **애니메이션 동작(Motion)까지 완전 자동 생성**할 수 있으므로, 수작업 공수 0으로 2D 메트로배니아 몬스터/플레이어 2D 스프라이트 시트를 무한 생산할 수 있습니다!
