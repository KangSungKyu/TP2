using NUnit.Framework;
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
            string csvContent = "idx,nametextidx,unittype,prefabpath,maxhp,maxmp,movespeed,jumpforce,attackpower,defense,maxposture,posturerecoverrate,groggyduration\n" +
                               "3001,2001,1,Prefabs/Units/Player,100,50,5.0,8.0,10,2,100,10,3.0";
            UnitBaseDataTable table = new UnitBaseDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "UnitBaseDataTable 파싱 검증");
        }

        [Test]
        public void Test04_MonsterBaseDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,unitbaseidx,monstertype,detectrange,attackrange,patrolrange,phasecount,patternlist\n" +
                               "4001,3001,1,10.0,2.0,5.0,1,5001_5002";
            MonsterDataTable table = new MonsterDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "MonsterDataTable 파싱 검증");
        }

        [Test]
        public void Test05_MonsterAndBossPatternDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,patternname,cooldown,casttime,range,damagemultiplier,hitcount,hittimings,animationclip\n" +
                               "5001,Smash,3.0,0.2,2.0,1.5,1,0.1,Anim_Smash";
            MonsterPatternDataTable table = new MonsterPatternDataTable();
            table.LoadData(csvContent);
            Assert.AreEqual(1, table.GetDataCount(), "MonsterPatternDataTable 파싱 검증");
        }

        [Test]
        public void Test06_SkillDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,name,animationclip,range,casttime,cooldownsec,mpcost,damagemultiplier,isbasicattack,hitcount,hittimings,activeduration,effectidx,animstate\n" +
                               "7001,BasicAttack,Skill_01,1.5,0.0,0.5,5,1.0,true,1,0.15,0.3,8001,7\n" +
                               "7002,Fireball,Skill_Fireball,5.0,0.2,3.0,20,2.5,false,1,0.12,0.8,8002,7";

            SkillDataTable table = new SkillDataTable();
            table.LoadData(csvContent);

            Assert.AreEqual(2, table.GetDataCount(), "SkillDataTable은 2개의 스킬 마스터 데이터를 로드해야 합니다.");
            Assert.IsTrue(table.TryGetSkillData(7001, out var skill7001), "uint idx (7001) 스킬 데이터 조회가 가능해야 합니다.");
            Assert.AreEqual(7001u, skill7001.Idx, "SkillData Idx = 7001 검증");
            Assert.AreEqual(7001u, skill7001.SkillId, "SkillData SkillId => Idx 호환 프로퍼티 검증");
            Assert.IsTrue(table.TryGetSkill(7001, out var skillInfo), "int skillId (7001) 하위 호환성 조회가 가능해야 합니다.");
            Assert.AreEqual(7001, skillInfo.Id, "SkillInfo Id = 7001 검증");
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
    }
}
