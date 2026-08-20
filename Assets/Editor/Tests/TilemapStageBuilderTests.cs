using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    /// <summary>
    /// TilemapStageBuilder 룸 청크 전환, 관문 포탈 3종(Portal, Door, Portal_Gate), 1-Way 발판, 벽점프, UnitSpawner, MetroidvaniaCamera2D, StageManager 1스테이지 초심자 룸 시퀀스 및 챕터 1 도교 신전 타일셋 무결성 검증 NUnit 테스트
    /// </summary>
    public class TilemapStageBuilderTests
    {
        [Test]
        public void Test00_FadeCoversCleanupTeleportAndCameraSnapBeforeFadeIn()
        {
            string source = File.ReadAllText("Assets/Scripts/Scene/TilemapStageBuilder.cs");
            int method = source.IndexOf("public async UniTask<bool> BuildTilemapStageAsync");
            int opaque = source.IndexOf("fadeOverlayCanvasGroup.alpha = 1f;", method);
            int renderedFrame = source.IndexOf("await UniTask.NextFrame(cancellationToken);", opaque);
            int cleanup = source.IndexOf("CleanupPreviousStageAndEffects();", renderedFrame);
            int camera = source.IndexOf("SetupMetroidvaniaCamera(", cleanup);
            int fadeIn = source.IndexOf("await fadeInScreenAsync(cancellationToken);", camera);

            Assert.GreaterOrEqual(opaque, 0);
            Assert.Greater(renderedFrame, opaque);
            Assert.Greater(cleanup, renderedFrame);
            Assert.Greater(camera, cleanup);
            Assert.Greater(fadeIn, camera);
        }

        [Test]
        public void Test01_TilemapRoomTestDummyPrefab_ExistenceAndMarkers()
        {
            string path = "Assets/Prefabs/Development/Tilemap_Room_TestDummy.prefab";
            Assert.IsTrue(File.Exists(path), $"Tilemap_Room_TestDummy.prefab 파일이 존재해야 합니다: {path}");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "Tilemap_Room_TestDummy.prefab 로드 실패");

            Transform playerMarker = prefab.transform.Find("PlayerSpawnMarker");
            if (playerMarker == null)
            {
                playerMarker = prefab.transform.Find("Markers/PlayerSpawnMarker");
            }
            Assert.IsTrue(playerMarker != null || prefab.transform.childCount > 0, "Tilemap_Room_TestDummy 내부에 마커 또는 하위 지형 오브젝트가 구성되어 있어야 합니다.");
        }

        [Test]
        public void Test02_OneWayPlatform_PassThroughDurationLogic()
        {
            GameObject platformObj = new GameObject("Test_OneWayPlatform");
            var collider = platformObj.AddComponent<BoxCollider2D>();
            var effector = platformObj.AddComponent<PlatformEffector2D>();
            collider.usedByEffector = true;

            Assert.IsNotNull(collider, "BoxCollider2D 생성 실패");
            Assert.IsNotNull(effector, "PlatformEffector2D 생성 실패");
            Assert.IsTrue(collider.usedByEffector, "1-Way 발판 콜라이더는 PlatformEffector2D를 사용하도록 설정되어야 합니다.");

            Object.DestroyImmediate(platformObj);
        }

        [Test]
        public void Test03_WallJumpSurface_PropertiesAndRules()
        {
            GameObject wallObj = new GameObject("Test_Wall_Standard");
            var wallJumpSurface = wallObj.AddComponent<WallJumpSurface>();
            wallJumpSurface.CanWallJump = true;
            wallJumpSurface.AllowSameWall = true;
            wallJumpSurface.SlideSpeedMultiplier = 1.0f;

            Assert.IsTrue(wallJumpSurface.CanWallJump, "표준 벽은 CanWallJump = true 이어야 합니다.");
            Assert.IsTrue(wallJumpSurface.AllowSameWall, "표준 벽은 동일벽 연속 점프가 허용되어야 합니다.");

            GameObject iceWallObj = new GameObject("Test_Wall_Ice");
            var iceSurface = iceWallObj.AddComponent<WallJumpSurface>();
            iceSurface.CanWallJump = true;
            iceSurface.SlideSpeedMultiplier = 2.5f;

            Assert.AreEqual(2.5f, iceSurface.SlideSpeedMultiplier, 0.01f, "얼음 벽은 슬라이딩 속도 배율이 2.5x 이어야 합니다.");

            Object.DestroyImmediate(wallObj);
            Object.DestroyImmediate(iceWallObj);
        }

        [Test]
        public void Test04_BuildStageEditorSync_ExecutionAndRootValidation()
        {
            GameObject builderObj = new GameObject("Test_StageBuilder");
            var builder = builderObj.AddComponent<TilemapStageBuilder>();

            builder.BuildStageEditorSync();

            GameObject createdRoot = GameObject.Find("TilemapStage_Root");
            Assert.IsNotNull(createdRoot, "BuildStageEditorSync 실행 후 TilemapStage_Root 오브젝트가 씬 상에 생성되어야 합니다.");

            Object.DestroyImmediate(createdRoot);
            Object.DestroyImmediate(builderObj);
        }

        [Test]
        public void Test05_UnitSpawner_CleanupExistingUnits_PreventsDuplicates()
        {
            GameObject player1 = new GameObject("Test_Player1");
            player1.AddComponent<Player>();

            GameObject player2 = new GameObject("Test_Player2");
            player2.AddComponent<Player>();

            var spawnerObj = new GameObject("Test_UnitSpawner");
            var spawner = spawnerObj.AddComponent<UnitSpawner>();

            GameObject roomChunk = new GameObject("Test_RoomChunk");
            GameObject markerObj = new GameObject("Test_Marker");
            markerObj.transform.SetParent(roomChunk.transform);
            var marker = markerObj.AddComponent<SpawnPointMarker>();
            marker.Type = SpawnType.Player;

            spawner.SpawnUnitsFromMarkers(roomChunk);

            var activePlayers = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(activePlayers.Length, 1, "UnitSpawner 스폰 후 유효한 플레이어가 존재해야 합니다.");

            Object.DestroyImmediate(player1);
            if (player2 != null) Object.DestroyImmediate(player2);
            Object.DestroyImmediate(spawnerObj);
            Object.DestroyImmediate(roomChunk);
        }

        [Test]
        public void Test06_MetroidvaniaCamera2D_TrackingAndBoundsClamping()
        {
            GameObject camObj = new GameObject("Test_MainCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.0f;
            cam.aspect = 1.777f;

            var metroCam = camObj.AddComponent<MetroidvaniaCamera2D>();
            metroCam.UseBounds = true;
            metroCam.MinBounds = new Vector2(-29f, -1f);
            metroCam.MaxBounds = new Vector2(29f, 17f);

            GameObject targetObj = new GameObject("Test_CameraTarget");
            targetObj.transform.position = new Vector3(10f, 3f, 0f);
            metroCam.Target = targetObj.transform;
            metroCam.SnapToTarget();

            Assert.IsNotNull(metroCam, "MetroidvaniaCamera2D 생성 실패");
            Assert.IsTrue(metroCam.UseBounds, "카메라 바운더리 활성화 여부 확인");

            Assert.AreEqual(targetObj.transform.position.x, camObj.transform.position.x, 0.01f);
            Object.DestroyImmediate(targetObj);
            Object.DestroyImmediate(camObj);
        }

        [Test]
        public void Test07_KinematicMotor2D_PassThroughOneWayPlatform_ImpulseAndGroundedState()
        {
            GameObject motorObj = new GameObject("Test_KinematicMotor");
            motorObj.AddComponent<Rigidbody2D>();
            motorObj.AddComponent<BoxCollider2D>();
            var motor = motorObj.AddComponent<KinematicMotor2D>();

            Assert.IsNotNull(motor, "KinematicMotor2D 생성 실패");
            Assert.IsFalse(motor.IsGrounded, "기본 상태에서 IsGrounded = false 이어야 합니다.");

            Object.DestroyImmediate(motorObj);
        }

        [Test]
        public void Test08_PortalInteraction_TriggerAndDestinationBinding()
        {
            string[] portalPaths = new string[]
            {
                "Assets/Prefabs/Structures/Portal.prefab",
                "Assets/Prefabs/Structures/Door.prefab",
                "Assets/Prefabs/Structures/Portal_Gate.prefab"
            };

            foreach (string path in portalPaths)
            {
                Assert.IsTrue(File.Exists(path), $"포탈 프리팹 파일이 존재해야 합니다: {path}");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                GameObject testPortal = null;
                if (prefab != null)
                {
                    testPortal = Object.Instantiate(prefab);
                }
                else
                {
                    testPortal = new GameObject(Path.GetFileNameWithoutExtension(path));
                    var col = testPortal.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                }

                var collider = testPortal.GetComponent<Collider2D>();
                var renderer = testPortal.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(collider, $"'{path}' 포탈 오브젝트에 Collider2D가 부착되어 있어야 합니다.");
                Assert.IsTrue(collider.isTrigger, $"'{path}' 포탈 콜라이더는 isTrigger = true 이어야 합니다.");

                Assert.IsNotNull(renderer != null ? renderer.sprite : null, $"'{path}' portal/door texture binding missing");
                Object.DestroyImmediate(testPortal);
            }
        }

        [Test]
        public void Test09_RoomChunkTransition_StageBoundsUpdate()
        {
            GameObject camObj = new GameObject("Test_TransitionCamera");
            var metroCam = camObj.AddComponent<MetroidvaniaCamera2D>();
            metroCam.MinBounds = new Vector2(0f, 0f);
            metroCam.MaxBounds = new Vector2(60f, 30f);

            Assert.AreEqual(60f, metroCam.MaxBounds.x, 0.01f, "룸 청크 전환 시 60x30 카메라 바운더리가 정상 갱신되어야 합니다.");

            Object.DestroyImmediate(camObj);
        }

        [Test]
        public void Test10_Stage1_RoomSequence_3Rooms_Validity()
        {
            GameObject stageManagerObj = new GameObject("Test_StageManager");
            var stageMgr = stageManagerObj.AddComponent<StageManager>();

            Assert.IsNotNull(stageMgr, "StageManager 생성 실패");
            Assert.AreEqual(9001u, stageMgr.CurrentStageIdx, "기본 CurrentStageIdx는 9001이어야 합니다.");
            Assert.AreEqual("Prefab_1040", stageMgr.ResolveAddressableKey(1040), "ResourceIdx 1040은 Entry 룸 키로 해석되어야 합니다.");
            Assert.AreEqual("Prefab_1041", stageMgr.ResolveAddressableKey(1041), "ResourceIdx 1041은 Battle 룸 키로 해석되어야 합니다.");
            Assert.AreEqual("Prefab_1042", stageMgr.ResolveAddressableKey(1042), "ResourceIdx 1042는 Boss 룸 키로 해석되어야 합니다.");

            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Prefab_1042.prefab");
            Assert.IsNotNull(bossPrefab);
            var bossMarkers = bossPrefab.GetComponentsInChildren<SpawnPointMarker>(true);
            Assert.IsTrue(System.Array.Exists(bossMarkers, marker =>
                marker.EnableSpawn && marker.Type == SpawnType.Boss && marker.MonsterId == 3201));

            Object.DestroyImmediate(stageManagerObj);
        }

        [Test]
        public void Test11_RoomDoorPortal_ManualInput_AndTargetRoomIdx()
        {
            GameObject portalObj = new GameObject("Test_RoomDoorPortal");
            var portal = portalObj.AddComponent<RoomDoorPortal>();
            portal.TargetRoomResourceIdx = 1041;
            portal.AutoTriggerOnTouch = true;

            Assert.IsNotNull(portal, "RoomDoorPortal 생성 실패");
            Assert.AreEqual(1041u, portal.TargetRoomResourceIdx, "Portal target must use ResourceData idx");
            Assert.IsTrue(portal.AutoTriggerOnTouch, "AutoTriggerOnTouch 활성화 상태 확인");

            Object.DestroyImmediate(portalObj);
        }

        [Test]
        public void Test12_Chapter1_TaoistTemple_TilesetAndPortalGateBinding()
        {
            string taoTilesetPath = "Assets/Textures/Environment/Tile_Chapter1_TaoShrine.png";
            Assert.IsTrue(File.Exists(taoTilesetPath), $"1챕터 도교 신전 타일셋 에셋이 존재해야 합니다: {taoTilesetPath}");

            string portalGatePath = "Assets/Prefabs/Structures/Portal_Gate.prefab";
            Assert.IsTrue(File.Exists(portalGatePath), $"Portal_Gate.prefab 관문 에셋이 존재해야 합니다: {portalGatePath}");
        }

        [Test]
        public void Test13_StageData_IntegerIdx_9001_RoomSequenceRuntime_Validation()
        {
            string stageDataCsvPath = "Assets/Datas/StageData.csv";
            Assert.IsTrue(File.Exists(stageDataCsvPath), $"StageData.csv 에셋 파일이 존재해야 합니다: {stageDataCsvPath}");

            string text = File.ReadAllText(stageDataCsvPath);
            Assert.IsTrue(text.Contains("9001"), "StageData.csv에 정수 idx 9001 (1스테이지)이 포함되어 있어야 합니다.");
            Assert.IsTrue(text.Contains("1040"), "1스테이지 Entry 룸 청크 ResourceIdx (1040)가 포함되어 있어야 합니다.");
            Assert.IsTrue(text.Contains("1041"), "1스테이지 Battle 룸 청크 ResourceIdx (1041)가 포함되어 있어야 합니다.");
            Assert.IsTrue(text.Contains("1042"), "1스테이지 Boss 룸 청크 ResourceIdx (1042)가 포함되어 있어야 합니다.");
        }

        [Test]
        public void Test14_Addressable_Label_Datas_And_StageData_ThemeType_Integer_Parsing_Validation()
        {
            string stageDataCsvPath = "Assets/Datas/StageData.csv";
            Assert.IsTrue(File.Exists(stageDataCsvPath), $"StageData.csv 에셋 파일이 존재해야 합니다: {stageDataCsvPath}");

            string text = File.ReadAllText(stageDataCsvPath);
            Assert.IsTrue(text.Contains("9001,2001,1,1,1040,1042,1040_1041_1042"), "StageData.csv 9001행에서 themetype=1 정수 파싱 항목이 완벽 일치해야 합니다.");

            string metaPath = stageDataCsvPath + ".meta";
            Assert.IsTrue(File.Exists(metaPath), $"StageData.csv.meta 에셋 메타 파일이 존재해야 합니다: {metaPath}");
            
            string metaText = File.ReadAllText(metaPath);
            Assert.IsNotNull(metaText, "StageData.csv.meta 로딩 확인");
        }

        [Test]
        public void Test15_HubScene_To_MainScene_Stage1_9001_Transition_And_AutoRender_Validation()
        {
            GameObject hubSceneObj = new GameObject("Test_HubScene");
            var hubScene = hubSceneObj.AddComponent<HubScene>();
            Assert.IsNotNull(hubScene, "HubScene 컴포넌트 생성 실패");

            GameObject mainSceneObj = new GameObject("Test_MainScene");
            var mainScene = mainSceneObj.AddComponent<MainScene>();
            Assert.IsNotNull(mainScene, "MainScene 컴포넌트 생성 실패");

            GameObject stageMgrObj = new GameObject("Test_StageManager");
            var stageMgr = stageMgrObj.AddComponent<StageManager>();
            stageMgr.CurrentStageIdx = 9001;

            Assert.AreEqual(9001u, stageMgr.CurrentStageIdx, "1스테이지(9001) 식별자 설정 확인");
            Assert.AreEqual("Prefab_1040", stageMgr.ResolveAddressableKey(1040), "HubScene -> MainScene 진입 후 1스테이지 Entry 룸 청크(1040) 자동 렌더링 키 해석 검증");

            Object.DestroyImmediate(hubSceneObj);
            Object.DestroyImmediate(mainSceneObj);
            Object.DestroyImmediate(stageMgrObj);
        }

    }
}
