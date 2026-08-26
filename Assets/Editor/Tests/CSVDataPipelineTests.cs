using NUnit.Framework;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.TestTools;

namespace QA.Tests
{
    public class CSVDataPipelineTests
    {
        [Test]
        public void Test01_ResourceDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,path\n1001,Prefabs/Units/Player";
            ResourceDataTable table = new ResourceDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "ResourceDataTable 파싱 검증");
        }

        [Test]
        public void Test02_TextDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,en,kr\n2001,Player,플레이어";
            TextDataTable table = new TextDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "TextDataTable 파싱 검증");
        }

        [Test]
        public void Test_TextData_LocalizationDefaultsFallbackAndStrictHeader()
        {
            var previous = GameLanguageSettings.Current;
            try
            {
                Assert.AreEqual(GameLanguage.En, GameLanguageSettings.RuntimeDefault);
                Assert.AreEqual(GameLanguage.Kr, GameLanguageSettings.PrototypeDefault);
                var table = new TextDataTable();
                table.LoadData("idx,en,kr\n2001,Player,플레이어\n2002,Fallback,\n2003,,누락");
                GameLanguageSettings.Current = GameLanguage.En;
                Assert.AreEqual("Player", table.GetText(2001));
                GameLanguageSettings.Current = GameLanguage.Kr;
                Assert.AreEqual("플레이어", table.GetText(2001));
                Assert.AreEqual("Fallback", table.GetText(2002));
                bool warned = false;
                Application.LogCallback handler = (message, _, type) =>
                    warned |= type == LogType.Warning && message.Contains("TextData idx 2003");
                Application.logMessageReceived += handler;
                try
                {
                    Assert.AreEqual(string.Empty, table.GetText(2003));
                }
                finally
                {
                    Application.logMessageReceived -= handler;
                }
                Assert.IsTrue(warned);
                Assert.Throws<CsvHelper.HeaderValidationException>(() =>
                    new TextDataTable().LoadData("idx,text\n2001,Legacy"));
            }
            finally
            {
                GameLanguageSettings.Current = previous;
            }
        }

        [Test]
        public void Test03_UnitBaseDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,nametextidx,unittype,prefabid,animatorid,maxhp,maxmp,maxposture,atk,def,movespeed,visualoffsety,hitboxradius,faction\n" +
                               "3001,2001,1,1001,1010,100,50,100,10,2,5.0,0.6,0.5,1";
            UnitBaseDataTable table = new UnitBaseDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "UnitBaseDataTable 파싱 검증");
        }

        [Test]
        public void Test04_MonsterBaseDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,detectrange,attackrange,patternidxlist\n" +
                               "5101,10.0,2.0,6001_6002";
            MonsterDataTable table = new MonsterDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "MonsterDataTable 파싱 검증");
        }

        [Test]
        public void Test05_MonsterAndBossPatternDataTable_ParsingAndKeyValidity()
        {
            var csv = new StringBuilder("idx,patternnametextidx,executiontype,triggertype,triggervalue,randomweight,predelay,postdelay,cooldown,damage,chasetimeout,skillidx,minstartdistance,maxstartdistance,projectileresourceidx,projectilespeed,projectilemaxdistance\n");
            for (uint idx = 6001; idx <= 6008; idx++)
                csv.AppendLine($"{idx},2010,1,3,2.0,100,0.2,0.5,3.0,15,1.0,7001,0,0,0,0,0");
            for (uint idx = 6100; idx <= 6103; idx++)
                csv.AppendLine($"{idx},2010,1,3,2.0,100,0.2,0.5,3.0,15,1.0,7001,0,0,0,0,0");
            MonsterPatternDataTable table = new MonsterPatternDataTable();
            table.LoadData(csv.ToString());
            Assert.AreEqual(12, table.GetDataCount(), "일반/가론 패턴 통합 파싱 검증");
            for (uint idx = 6001; idx <= 6008; idx++) Assert.IsTrue(table.TryGetPatternData(idx, out _));
            for (uint idx = 6100; idx <= 6103; idx++) Assert.IsTrue(table.TryGetPatternData(idx, out _));
            Assert.Throws<CsvHelper.HeaderValidationException>(() => new MonsterPatternDataTable().LoadData(
                "idx,patternnametextidx,executiontype,triggertype,triggervalue,randomweight,predelay,postdelay,cooldown,damage,chasetimeout,skillidx\n6005,2016,4,3,10,100,0.6,0.7,2.5,14,1,7002"));
        }

        [Test]
        public void Test06_SkillDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,nametextidx,animationclip,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,attackmotiontime,motionphasemask,effectidx,animstate\n" +
                               "7001,2101,Skill_01,1.5,0.0,0.5,5,1.0,1,1,0.15,0.05,0.1,0.2,3,8001,7\n" +
                               "7002,2102,Skill_Fireball,5.0,0.2,3.0,20,2.5,0,1,0.12,0.08,0.15,0.4,0,8002,7";

            SkillDataTable table = new SkillDataTable();
            table.LoadData(csvContent);

            Assert.AreEqual(2, table.GetDataCount(), "SkillDataTable은 2개의 스킬 마스터 데이터를 로드해야 합니다.");
            Assert.IsTrue(table.TryGetSkillData(7001, out var skill7001), "uint idx (7001) 스킬 데이터 조회가 가능해야 합니다.");
            Assert.AreEqual(7001u, skill7001.Idx, "SkillData Idx = 7001 검증");
            Assert.AreEqual(7001u, skill7001.SkillId, "SkillData SkillId => Idx 호환 프로퍼티 검증");
            Assert.IsTrue(table.TryGetSkill(7001, out var skillInfo), "int skillId (7001) 하위 호환성 조회가 가능해야 합니다.");
            Assert.AreEqual(7001, skillInfo.Id, "SkillInfo Id = 7001 검증");
            Assert.AreEqual(string.Empty, skillInfo.Name, "누락 TextData idx는 빈 문자열로 격리해야 합니다.");
            Assert.IsTrue(skill7001.IsBasicAttack);
            Assert.AreEqual(0.05f, skill7001.HitWindowPre, 0.001f, "HitWindowPre 0.05s 검증");
            Assert.AreEqual(0.1f, skill7001.HitWindowPost, 0.001f, "HitWindowPost 0.1s 검증");
            Assert.AreEqual(0.2f, skill7001.AttackMotionTime, 0.001f);
            Assert.AreEqual(SkillMotionPhase.AttackMotion | SkillMotionPhase.Pre, skill7001.MotionPhaseMask);
            Assert.AreEqual(7, skill7001.AnimState, "AnimState 7 검증");
            Assert.IsFalse(table.GetById(7002).IsBasicAttack);
            Assert.Throws<CsvHelper.HeaderValidationException>(() => new SkillDataTable().LoadData(
                "idx,nametextidx,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,effectidx,animstate\n" +
                "7001,2101,1.5,0,0.5,5,1,1,1,0.15,0.05,0.1,8001,7"));
        }

        [TestCase("-0.1")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        public void SkillData_InvalidAttackMotionTimeFallsBackToZero(string value)
        {
            const string header = "idx,nametextidx,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,attackmotiontime,motionphasemask,effectidx,animstate\n";
            LogAssert.Expect(LogType.Warning,
                "[SkillDataTable] Skill idx 7001 has invalid attackmotiontime; using 0.");
            var table = new SkillDataTable();
            table.LoadData(header + $"7001,2101,1.5,0,0.5,5,1,1,1,0.15,0.05,0.1,{value},0,8001,7");
            Assert.IsTrue(table.TryGetSkillData(7001, out SkillData skill));
            Assert.AreEqual(0f, skill.AttackMotionTime);
        }

        [TestCase("16")]
        [TestCase("-1")]
        [TestCase("bad")]
        public void SkillData_MotionPhaseMaskRejectsInvalidBits(string value)
        {
            const string header = "idx,nametextidx,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,attackmotiontime,motionphasemask,effectidx,animstate\n";
            var ex = Assert.Catch<System.Exception>(() => new SkillDataTable().LoadData(header +
                $"7001,2101,1.5,0,0.5,5,1,1,1,0.15,0.05,0.1,0.2,{value},8001,7"));
            StringAssert.Contains("Skill idx 7001", ex.ToString());
        }

        [TestCase("true")]
        [TestCase("false")]
        [TestCase("2")]
        [TestCase("")]
        public void Test_SkillData_BooleanRejectsNonZeroOne(string value)
        {
            var table = new SkillDataTable();
            string csv = "idx,nametextidx,animationclip,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,attackmotiontime,motionphasemask,effectidx,animstate\n" +
                         $"7001,2101,Skill_01,1.5,0,0.5,5,1,{value},1,0.15,0.05,0.1,0.2,0,8001,7";
            Assert.Catch<System.Exception>(() => table.LoadData(csv));
        }

        [Test]
        public void Test_EffectData_TextIdxHeaderAndMissingTextFallback()
        {
            var table = new EffectDataTable();
            Assert.DoesNotThrow(() => table.LoadData(
                "idx,effectnametextidx,prefabidx,duration,scale,loopcount,spawnpivotx,spawnpivoty,activecenterx,activecentery,activesizex,activesizey,activeshape,unitidx,patternidx,skillidx,hittick\n" +
                "8001,2201,1020,0.3,1.0,1,0,0,0,0,0,0,0,0,0,0,0\n" +
                "8014,2201,1081,1,1,1,1,0,.16,-.11,.56,.82,0,3001,0,7001,0"));
            Assert.IsTrue(table.TryGetEffectData(8001, out EffectData effect));
            Assert.AreEqual(2201u, effect.EffectNameTextIdx);
            Assert.AreEqual(string.Empty, table.GetDisplayName(8001));
            Assert.IsTrue(table.TryGetEffectData(8014, out EffectData attackEffect));
            Assert.IsTrue(attackEffect.HasValidActiveBounds);
            Assert.AreEqual(new Vector2(.16f, -.11f),
                new Vector2(attackEffect.ActiveCenterX, attackEffect.ActiveCenterY));
            Assert.AreEqual(new Vector2(.56f, .82f),
                new Vector2(attackEffect.ActiveSizeX, attackEffect.ActiveSizeY));
            Assert.AreEqual(new Vector2(1f, 0f),
                new Vector2(attackEffect.SpawnPivotX, attackEffect.SpawnPivotY));
        }

        [Test]
        public void MonsterPatternChain_ValidatesAtomicallyAndExcludesChildren()
        {
            const string header = "idx,patternnametextidx,executiontype,triggertype,triggersubject,triggervalue,randomweight,predelay,postdelay,cooldown,damage,chasetimeout,skillidx,nextpatternidx,minstartdistance,maxstartdistance,projectileresourceidx,projectilespeed,projectilemaxdistance,attackmotionprofileidx\n";
            static string Row(uint idx, uint next) =>
                $"{idx},2010,1,0,0,0,100,0,0,0,10,1,7001,{next},0,2,0,0,0,0\n";

            var valid = new MonsterPatternDataTable();
            valid.LoadData(header + Row(6001, 6002) + Row(6002, 6003) + Row(6003, 0));
            var chain = new System.Collections.Generic.List<MonsterPatternData>();
            Assert.IsTrue(valid.TryBuildPatternChain(6001, chain));
            CollectionAssert.AreEqual(new uint[] { 6001, 6002, 6003 }, chain.ConvertAll(item => item.Idx));
            Assert.IsTrue(valid.IsChainChild(6002));
            Assert.IsFalse(valid.TryBuildPatternChain(6002, chain), "Linked children cannot be selector entries.");

            var self = new MonsterPatternDataTable();
            LogAssert.Expect(LogType.Error, "[MonsterPatternDataTable] Pattern chain rooted at 6001 has invalid next FK 6001; chain rejected.");
            self.LoadData(header + Row(6001, 6001));
            Assert.AreEqual(0, self.GetDataCount());

            var missing = new MonsterPatternDataTable();
            LogAssert.Expect(LogType.Error, "[MonsterPatternDataTable] Pattern chain rooted at 6001 has invalid next FK 6099; chain rejected.");
            missing.LoadData(header + Row(6001, 6099));
            Assert.AreEqual(0, missing.GetDataCount());

            var cycle = new MonsterPatternDataTable();
            LogAssert.Expect(LogType.Error, "[MonsterPatternDataTable] Pattern chain rooted at 6001 has a cycle or exceeds 16 steps; chain rejected.");
            LogAssert.Expect(LogType.Error, "[MonsterPatternDataTable] Pattern chain rooted at 6002 has a cycle or exceeds 16 steps; chain rejected.");
            cycle.LoadData(header + Row(6001, 6002) + Row(6002, 6001));
            Assert.AreEqual(0, cycle.GetDataCount());

            var tooLongCsv = new StringBuilder(header);
            for (uint idx = 6001; idx <= 6017; idx++) tooLongCsv.Append(Row(idx, idx == 6017 ? 0u : idx + 1u));
            var tooLong = new MonsterPatternDataTable();
            LogAssert.Expect(LogType.Error, "[MonsterPatternDataTable] Pattern chain rooted at 6001 has a cycle or exceeds 16 steps; chain rejected.");
            tooLong.LoadData(tooLongCsv.ToString());
            Assert.AreEqual(0, tooLong.GetDataCount());

            foreach (float fixedDelta in new[] { 1f / 15f, 1f / 30f, 1f / 60f })
            {
                Assert.AreEqual(.045f, Monster.CalculateEffectivePreDelay(.045f, .045f) + .045f, .0001f);
                Assert.AreEqual(.0375f, Monster.CalculateEffectivePreDelay(.0375f, .0375f) + .0375f, .0001f);
                Assert.AreEqual(.0625f, Monster.CalculateEffectivePreDelay(.0625f, .0625f) + .0625f, .0001f);
                Assert.AreEqual(.2f, Monster.CalculatePatternRecoverySeconds(.2f, 0f), .0001f,
                    $"Linked zero PostDelay follows complete recovery at fixedDelta {fixedDelta}.");
                Assert.AreEqual(.25f, Monster.CalculatePatternRecoverySeconds(.2f, .05f), .0001f,
                    $"Linked recovery and PostDelay must add exactly once at fixedDelta {fixedDelta}.");
                Assert.AreEqual(.2f, Monster.CalculatePatternRecoverySeconds(.2f, 0f), .0001f,
                    $"Final step retains animation recovery at fixedDelta {fixedDelta}.");
            }

            string monsterSource = File.ReadAllText("Assets/Scripts/Gameplay/Monster.cs");
            int sequenceStart = monsterSource.IndexOf("private async UniTask ExecutePatternAsync", System.StringComparison.Ordinal);
            int coreStart = monsterSource.IndexOf("private async UniTask ExecutePatternCoreAsync", sequenceStart, System.StringComparison.Ordinal);
            int coreEnd = monsterSource.IndexOf("protected virtual UniTask ExecuteMovementAiAsync", coreStart, System.StringComparison.Ordinal);
            string sequenceBody = monsterSource.Substring(sequenceStart, coreStart - sequenceStart);
            Assert.AreEqual(0, sequenceBody.Split(new[] { "BeginAttackTelegraph(" }, System.StringSplitOptions.None).Length - 1,
                "The pattern root must not emit or await a shared telegraph.");
            StringAssert.Contains("CurrentPatternState != PatternState.Recovery", sequenceBody,
                "A failed step must terminate the chain instead of advancing to its child.");
            StringAssert.Contains("CancelCurrentPattern(PatternCancelReason.TargetInvalid)", sequenceBody);
            StringAssert.Contains("BeginAttackTelegraph(", monsterSource.Substring(coreStart, coreEnd - coreStart),
                "Every skill step owns its non-blocking PRE telegraph.");
        }

        [Test]
        public void Test07_StageDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist\n" +
                               "9001,2001,1,1,1040,1042,1040_1041_1042\n" +
                               "9002,2001,2,2,1040,1042,1040_1042";

            StageDataTable table = new StageDataTable();
            table.LoadData(csvContent);

            Assert.AreEqual(2, table.GetDataCount(), "StageDataTable은 2개의 스테이지 마스터 데이터를 가지고 있어야 합니다.");
            
            bool found = table.TryGetStageData(9001, out StageBaseData stage1Data);
            Assert.IsTrue(found, "Type 9 규격(9001) 1스테이지 데이터 조회가 가능해야 합니다.");
            Assert.AreEqual(9001u, stage1Data.Idx, "Stage 1 Idx = 9001 규격 검증");
            Assert.AreEqual(1, stage1Data.Chapter, "Stage 1 Chapter = 1 규격 검증");
            Assert.AreEqual(1, stage1Data.ThemeType, "Stage 1 ThemeType = 1 (정수) 파싱 무결성 검증");
            Assert.AreEqual(1040u, stage1Data.StartRoomIdx, "Stage 1 StartRoomIdx 규격 검증");
            Assert.AreEqual(1042u, stage1Data.BossRoomIdx, "Stage 1 BossRoomIdx 규격 검증");
            Assert.AreEqual(3, stage1Data.RoomSequenceIdxList.Length, "RoomSequenceIdxList 3개 청크 정수 idx 목록 규격 검증");
        }

        [Test]
        public void Test_CSVDataPipeline_ExceptionHandling_Pass()
        {
            StageDataTable table = new StageDataTable();
            table.LoadData("idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist\n9001,2001,1,1,1040,1042,1040_1041_1042");

            Assert.Throws<HeaderValidationException>(() => table.LoadData(
                "IDX,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist\n9001,2001,1,1,1040,1042,1040_1041_1042"));
            Assert.Throws<InvalidKeyException>(() => table.LoadData(
                "idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist\n,2001,1,1,1040,1042,1040_1041_1042"));
            Assert.Throws<InvalidKeyException>(() => table.LoadData(
                "idx,nametextidx,chapter,themetype,startroomidx,bossroomidx,roomsequenceidxlist\n9001,2001,1,999,1040,1042,1040_1041_1042"));

            Assert.IsTrue(table.TryGetStageData(9001, out var fallback));
            Assert.AreEqual(1, fallback.ThemeType, "오염 입력 실패 후 마지막 정상 테이블을 fallback으로 유지해야 합니다.");
            QATestRunner.AppendExceptionResult(nameof(StageDataTable),
                "HeaderValidationException/InvalidKeyException handled; previous valid table retained");
        }

        [Test]
        public void Test_StageRunCsvTypes_RouteWithoutIdxCollisions()
        {
            var layout = new StageLayoutDataTable();
            var chunks = new ChunkResourceDataTable();
            var encounters = new MonsterEncounterDataTable();
            var resources = new ResourceDataTable();

            layout.LoadData("idx,stagedataidx,minrows,maxrows,mincolumns,maxcolumns,minactivechunks,maxactivechunks,bossroomresourceidx,nextstageidx\n12001,9001,3,4,3,4,9,11,1042,9002");
            chunks.LoadData("idx,resourceidx,chunktype,supportedconnectionmask,minstageidx,maxuseperrun,weight\n11050,1050,1,15,9001,2,100");
            encounters.LoadData("idx,stageidx,variant,unitidxlist,threatcost,weight\n13001,9001,1,3101_3104,4,100");
            resources.LoadData("idx,path\n1050,Room_11050");

            Assert.AreEqual(DataTableType.StageLayout, Util.GetDataTableType(12001));
            Assert.AreEqual(DataTableType.ChunkResource, Util.GetDataTableType(11050));
            Assert.AreEqual(DataTableType.MonsterEncounter, Util.GetDataTableType(13001));
            Assert.IsTrue(resources.TryGetResource(1050, out ResourceData room));
            Assert.AreEqual("Room_11050", room.Path);
            Assert.IsTrue(layout.TryGetByStage(9001, out StageLayoutData layoutData));

            StageRunData run = Stage1RunGenerator.Generate(2, layoutData,
                chunks.GetForStage(9001), encounters.GetForStage(9001));
            Assert.IsTrue(Stage1RunGenerator.Validate(run));
            Assert.IsTrue(System.Array.Exists(run.Slots, slot => slot.ChunkResourceIdx == 1050));
            Assert.IsTrue(System.Array.Exists(run.Slots, slot => slot.MonsterUnitIdxList.Length == 2));
        }

        [Test]
        public void Test_StageRunCsvWrongLegacyRanges_AreRejectedPerFile()
        {
            var layout = new StageLayoutDataTable();
            Assert.Throws<InvalidKeyException>(() => layout.LoadData(
                "idx,stagedataidx,minrows,maxrows,mincolumns,maxcolumns,minactivechunks,maxactivechunks,bossroomresourceidx,nextstageidx\n9101,9001,3,4,3,4,9,11,1042,9002"));
            Assert.AreEqual(0, layout.GetDataCount());

            var resources = new ResourceDataTable();
            Assert.DoesNotThrow(() => resources.LoadData("idx,path\n1050,Room_11050"));
            Assert.AreEqual(1, resources.GetDataCount());
        }

        [Test]
        public void SkillAttackSubject_StrictIntegerAndRoleMatrix()
        {
            const string header = "idx,nametextidx,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,hitwindowpre,hitwindowpost,attackmotiontime,motionphasemask,effectidx,animstate,attackmotionprofileidx,attacksubject,bodypart\n";
            var table = new SkillDataTable();

            table.LoadData(header + "7001,2001,2,0,1,0,1,1,1,0.1,0.01,0.01,0.2,0,0,7,10001,0,0");
            Assert.IsTrue(table.TryGetSkillData(7001, out SkillData weapon));
            Assert.AreEqual(AttackSubject.Weapon, weapon.AttackSubject);
            Assert.AreEqual(BodyPartRole.None, weapon.BodyPartRole);

            table.LoadData(header + "7007,2018,2,0,1,0,1,0,1,0.1,0.01,0.01,0.3,5,0,14,10002,1,1");
            Assert.IsTrue(table.TryGetSkillData(7007, out SkillData torso));
            Assert.AreEqual(AttackSubject.BodyPart, torso.AttackSubject);
            Assert.AreEqual(BodyPartRole.Torso, torso.BodyPartRole);

            Assert.Catch<System.Exception>(() => table.LoadData(header +
                "7007,2018,2,0,1,0,1,0,1,0.1,0.01,0.01,0.3,5,0,14,10002,2,1"));
            Assert.Throws<System.InvalidOperationException>(() => table.LoadData(header +
                "7007,2018,2,0,1,0,1,0,1,0.1,0.01,0.01,0.3,5,0,14,10002,0,1"));
            Assert.Throws<System.InvalidOperationException>(() => table.LoadData(header +
                "7007,2018,2,0,1,0,1,0,1,0.1,0.01,0.01,0.3,5,0,14,10002,1,0"));
        }
    }
}
