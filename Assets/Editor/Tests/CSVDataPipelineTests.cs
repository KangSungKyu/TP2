using NUnit.Framework;
using System.IO;
using UnityEngine;

namespace QA.Tests
{
    /// <summary>
    /// 7종 CSV 데이터 파이프라인 무결성 검증 NUnit 테스트 클래스
    /// </summary>
    public class CSVDataPipelineTests
    {
        private const string DataFolderPath = "Assets/datas";

        [Test]
        public void Test01_ResourceDataTable_ParsingAndKeyValidity()
        {
            string path = Path.Combine(DataFolderPath, "ResourceData.csv");
            Assert.IsTrue(File.Exists(path), $"ResourceData.csv 파일이 존재하지 않습니다: {path}");

            string csvText = File.ReadAllText(path);
            var table = new ResourceDataTable();
            table.LoadData(csvText);

            Assert.Greater(table.GetDataCount(), 0, "ResourceDataTable 데이터 개수가 0개입니다.");
            
            // Key 1001 (Player), 1002 (Garon) 등 확인
            Assert.IsTrue(table.TryGetResource(1001, out var pRes), "Resource 1001 (Player) 참조 실패");
            Assert.IsFalse(string.IsNullOrEmpty(pRes.Path), "Resource 1001 Path가 비어 있습니다.");

            Assert.IsTrue(table.TryGetResource(1002, out var bRes), "Resource 1002 (Boss) 참조 실패");
            Assert.IsFalse(string.IsNullOrEmpty(bRes.Path), "Resource 1002 Path가 비어 있습니다.");
        }

        [Test]
        public void Test02_TextDataTable_ParsingAndKeyValidity()
        {
            string path = Path.Combine(DataFolderPath, "TextData.csv");
            Assert.IsTrue(File.Exists(path), $"TextData.csv 파일이 존재하지 않습니다: {path}");

            string csvText = File.ReadAllText(path);
            var table = new TextDataTable();
            table.LoadData(csvText);

            Assert.Greater(table.GetDataCount(), 0, "TextDataTable 데이터 개수가 0개입니다.");

            string text2001 = table.GetText(2001);
            Assert.IsFalse(string.IsNullOrEmpty(text2001), "Text 2001 (Player 이름) 조회가 실패했습니다.");

            string text2002 = table.GetText(2002);
            Assert.IsFalse(string.IsNullOrEmpty(text2002), "Text 2002 (Boss 이름) 조회가 실패했습니다.");
        }

        [Test]
        public void Test03_UnitBaseDataTable_ParsingAndKeyValidity()
        {
            string path = Path.Combine(DataFolderPath, "UnitBaseData.csv");
            Assert.IsTrue(File.Exists(path), $"UnitBaseData.csv 파일이 존재하지 않습니다: {path}");

            string csvText = File.ReadAllText(path);
            var table = new UnitBaseDataTable();
            table.LoadData(csvText);

            Assert.GreaterOrEqual(table.GetDataCount(), 5, "UnitBaseDataTable 데이터 개수가 최소 5개 이상이어야 합니다.");

            // 3001 Player 검증
            Assert.IsTrue(table.TryGetUnitData(3001, out var playerUnit), "UnitBase 3001 (Player) 조회가 실패했습니다.");
            Assert.AreEqual(1u, playerUnit.UnitType, "3001 유닛 타입은 Player(1)이어야 합니다.");
            Assert.AreEqual(100, playerUnit.MaxHp, "3001 MaxHp는 100이어야 합니다.");
            Assert.AreEqual(0.6f, playerUnit.VisualOffsetY, 0.01f, "3001 VisualOffsetY는 0.6이어야 합니다.");

            // 3201 Boss 철위병 가론 검증
            Assert.IsTrue(table.TryGetUnitData(3201, out var bossUnit), "UnitBase 3201 (Boss Garon) 조회가 실패했습니다.");
            Assert.AreEqual(3u, bossUnit.UnitType, "3201 유닛 타입은 Boss(3)이어야 합니다.");
            Assert.AreEqual(1000, bossUnit.MaxHp, "3201 MaxHp는 1000이어야 합니다.");
            Assert.AreEqual(0.75f, bossUnit.VisualOffsetY, 0.01f, "3201 VisualOffsetY는 0.75이어야 합니다.");
        }

        [Test]
        public void Test04_MonsterBaseDataTable_ParsingAndKeyValidity()
        {
            string path = Path.Combine(DataFolderPath, "MonsterBaseData.csv");
            Assert.IsTrue(File.Exists(path), $"MonsterBaseData.csv 파일이 존재하지 않습니다: {path}");

            string csvText = File.ReadAllText(path);
            var table = new MonsterDataTable();
            table.LoadData(csvText);

            Assert.Greater(table.GetDataCount(), 0, "MonsterDataTable 데이터 개수가 0개입니다.");

            // 3201 Boss MonsterBaseData 검증
            Assert.IsTrue(table.TryGetMonsterData(3201, out var bossData), "MonsterBase 3201 조회가 실패했습니다.");
            Assert.AreEqual(10.0f, bossData.DetectRange, 0.01f, "3201 DetectRange가 10.0이어야 합니다.");
            Assert.AreEqual(3.0f, bossData.AttackRange, 0.01f, "3201 AttackRange가 3.0이어야 합니다.");
            Assert.IsNotNull(bossData.PatternIdxList, "3201 PatternIdxList 배열이 null입니다.");
            Assert.AreEqual(4, bossData.PatternIdxList.Length, "3201 보스 패턴 개수가 4개이어야 합니다 (6100_6101_6102_6103).");
        }

        [Test]
        public void Test05_MonsterAndBossPatternDataTable_ParsingAndKeyValidity()
        {
            string mPath = Path.Combine(DataFolderPath, "MonsterPatternData.csv");
            string bPath = Path.Combine(DataFolderPath, "BossPatternData.csv");

            Assert.IsTrue(File.Exists(mPath), $"MonsterPatternData.csv 미존재: {mPath}");
            Assert.IsTrue(File.Exists(bPath), $"BossPatternData.csv 미존재: {bPath}");

            var table = new MonsterPatternDataTable();
            table.LoadData(File.ReadAllText(mPath));
            int count1 = table.GetDataCount();
            
            table.LoadData(File.ReadAllText(bPath));
            int count2 = table.GetDataCount();

            Assert.Greater(count2, 0, "MonsterPatternDataTable 로드가 실패했습니다.");

            // 6100 또는 6201 보스 패턴 검증
            bool hasPattern = table.TryGetPatternData(6100, out var p6100) || table.TryGetPatternData(6201, out p6100);
            Assert.IsTrue(hasPattern, "보스 패턴 (6100 또는 6201) 조회가 실패했습니다.");
            Assert.IsFalse(string.IsNullOrEmpty(p6100.AnimClipName), "보스 패턴 애니메이션 클립 이름이 비어 있습니다.");
        }

        [Test]
        public void Test06_SkillDataTable_ParsingAndKeyValidity()
        {
            string path = Path.Combine(DataFolderPath, "SkillData.csv");
            Assert.IsTrue(File.Exists(path), $"SkillData.csv 파일이 존재하지 않습니다: {path}");

            string csvText = File.ReadAllText(path);
            var table = new SkillDataTable();
            table.LoadData(csvText);

            Assert.Greater(table.GetDataCount(), 0, "SkillDataTable 데이터 개수가 0개입니다.");

            Assert.IsTrue(table.TryGetSkill(1, out var skill1), "Skill 1 (BasicAttack) 조회가 실패했습니다.");
            Assert.AreEqual("BasicAttack", skill1.Name, "Skill 1 이름 불일치");
            Assert.IsTrue(skill1.IsBasicAttack, "Skill 1은 IsBasicAttack = true 이어야 합니다.");
        }
    }
}
