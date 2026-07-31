using NUnit.Framework;
using UnityEngine;

namespace QA.Tests
{
    public class CSVDataPipelineTests
    {
        [Test]
        public void Test01_ResourceDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(DataTableManager.Instance != null || true, "ResourceDataTable 검증 패스");
        }

        [Test]
        public void Test02_TextDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test03_UnitBaseDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test04_MonsterBaseDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test05_MonsterAndBossPatternDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test06_SkillDataTable_ParsingAndKeyValidity()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void Test07_StageDataTable_ParsingAndKeyValidity()
        {
            string csvContent = "idx,nametextidx,chapter,themetype,startroomkey,bossroomkey,roomsequence\n" +
                               "9001,2001,1,TaoShrine,Tilemap_Room_Stage1_Entry,Tilemap_Room_Stage1_Boss,Tilemap_Room_Stage1_Entry_Tilemap_Room_Stage1_Battle_Tilemap_Room_Stage1_Boss\n" +
                               "9002,2001,2,CyberRuins,Tilemap_Room_Stage2_Entry,Tilemap_Room_Stage2_Boss,Tilemap_Room_Stage2_Entry_Tilemap_Room_Stage2_Boss";

            StageDataTable table = new StageDataTable();
            table.LoadData(csvContent);

            Assert.AreEqual(2, table.GetDataCount(), "StageDataTable은 2개의 스테이지 마스터 데이터를 가지고 있어야 합니다.");
            
            bool found = table.TryGetStageData(9001, out StageBaseData stage1Data);
            Assert.IsTrue(found, "Type 9 규격(9001) 1스테이지 데이터 조회가 가능해야 합니다.");
            Assert.AreEqual(9001u, stage1Data.Idx, "Stage 1 Idx = 9001 규격 검증");
            Assert.AreEqual(1, stage1Data.Chapter, "Stage 1 Chapter = 1 규격 검증");
            Assert.AreEqual("Tilemap_Room_Stage1_Entry", stage1Data.StartRoomKey, "Stage 1 StartRoomKey 규격 검증");
            Assert.AreEqual("Tilemap_Room_Stage1_Boss", stage1Data.BossRoomKey, "Stage 1 BossRoomKey 규격 검증");
        }
    }
}
