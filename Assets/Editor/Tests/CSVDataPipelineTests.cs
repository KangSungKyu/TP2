using NUnit.Framework;
using System.IO;
using System.Text;
using UnityEngine;

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
            string csvContent = "idx,text\n2001,플레이어";
            TextDataTable table = new TextDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "TextDataTable 파싱 검증");
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
            var csv = new StringBuilder("idx,patternnametextidx,animclipname,executiontype,triggertype,triggervalue,randomweight,predelay,postdelay,cooldown,damage,chasetimeout,skillidx\n");
            for (uint idx = 6001; idx <= 6008; idx++)
                csv.AppendLine($"{idx},2010,Monster_{idx},1,3,2.0,100,0.2,0.5,3.0,15,1.0,7001");
            for (uint idx = 6100; idx <= 6103; idx++)
                csv.AppendLine($"{idx},2010,Garon_{idx},1,3,2.0,100,0.2,0.5,3.0,15,1.0,7001");
            MonsterPatternDataTable table = new MonsterPatternDataTable();
            table.LoadData(csv.ToString());
            Assert.AreEqual(12, table.GetDataCount(), "일반/가론 패턴 통합 파싱 검증");
            for (uint idx = 6001; idx <= 6008; idx++) Assert.IsTrue(table.TryGetPatternData(idx, out _));
            for (uint idx = 6100; idx <= 6103; idx++) Assert.IsTrue(table.TryGetPatternData(idx, out _));
        }

        [Test]
        public void Test06_SkillDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,nametextidx,animationclip,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,activeduration,effectidx,animstate\n" +
                               "7001,2101,Skill_01,1.5,0.0,0.5,5,1.0,1,1,0.15,0.3,8001,7\n" +
                               "7002,2102,Skill_Fireball,5.0,0.2,3.0,20,2.5,0,1,0.12,0.8,8002,7";

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
            Assert.IsFalse(table.GetById(7002).IsBasicAttack);
        }

        [TestCase("true")]
        [TestCase("false")]
        [TestCase("2")]
        [TestCase("")]
        public void Test_SkillData_BooleanRejectsNonZeroOne(string value)
        {
            var table = new SkillDataTable();
            string csv = "idx,nametextidx,animationclip,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,activeduration,effectidx,animstate\n" +
                         $"7001,2101,Skill_01,1.5,0,0.5,5,1,{value},1,0.15,0.3,8001,7";
            Assert.Catch<System.Exception>(() => table.LoadData(csv));
        }

        [Test]
        public void Test_EffectData_TextIdxHeaderAndMissingTextFallback()
        {
            var table = new EffectDataTable();
            Assert.DoesNotThrow(() => table.LoadData(
                "idx,effectnametextidx,prefabidx,duration,scale,loopcount\n8001,2201,1020,0.3,1.0,1"));
            Assert.IsTrue(table.TryGetEffectData(8001, out EffectData effect));
            Assert.AreEqual(2201u, effect.EffectNameTextIdx);
            Assert.AreEqual(string.Empty, table.GetDisplayName(8001));
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
    }
}
