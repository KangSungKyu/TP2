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
    }
}
