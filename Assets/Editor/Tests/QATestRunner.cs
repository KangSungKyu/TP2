using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    /// <summary>
    /// QA 자동화 테스트 종합 실행 및 결과 검증 러너
    /// 메뉴 및 CLI 배치를 지원하며, 검증 결과를 파일로 기록합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class QATestRunner
    {
        private const string LogPath = "Logs/qa_test_results.txt";

        [MenuItem("QA/Run All QA Architecture Tests")]
        public static void RunAllTests()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[QA TEST RUNNER] Execution Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("================================================================================");

            int totalTests = 0;
            int passedTests = 0;
            int failedTests = 0;

            // 1. CSV Data Pipeline Tests 실행
            RunTestSuite(typeof(CSVDataPipelineTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 2. Gameplay Architecture Tests 실행
            RunTestSuite(typeof(GameplayArchitectureTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 3. Animator Controller & Clip Restoration Tests 실행
            RunTestSuite(typeof(AnimatorControllerTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 3. 종합 요약
            sb.AppendLine("--------------------------------------------------------------------------------");
            if (failedTests == 0)
            {
                sb.AppendLine($"[FINAL RESULT] SUCCESS: All {passedTests}/{totalTests} QA Tests Passed Successfully!");
                Debug.Log($"<color=green><b>[QATestRunner] 🎉 모든 QA 검증 성공! (총 {passedTests}/{totalTests} 패스)</b></color>");
            }
            else
            {
                sb.AppendLine($"[FINAL RESULT] FAILED: Passed: {passedTests}, Failed: {failedTests} / Total: {totalTests}");
                Debug.LogError($"[QATestRunner] ❌ QA 검증 실패 (성공: {passedTests}, 실패: {failedTests} / 총 {totalTests})");
            }
            sb.AppendLine("================================================================================");

            Directory.CreateDirectory("Logs");
            File.WriteAllText(LogPath, sb.ToString());
            Debug.Log($"[QATestRunner] 테스트 결과가 '{LogPath}' 파일로 저장되었습니다.");
        }

        public static void RunBatchTests()
        {
            RunAllTests();
            EditorApplication.Exit(0);
        }

        private static void RunTestSuite(Type testClassType, ref int total, ref int passed, ref int failed, StringBuilder sb)
        {
            sb.AppendLine($"\n--- Running Suite: {testClassType.Name} ---");
            object instance = Activator.CreateInstance(testClassType);
            MethodInfo setUpMethod = null;
            MethodInfo tearDownMethod = null;

            foreach (var m in testClassType.GetMethods())
            {
                if (m.GetCustomAttributes(typeof(NUnit.Framework.SetUpAttribute), false).Length > 0) setUpMethod = m;
                if (m.GetCustomAttributes(typeof(NUnit.Framework.TearDownAttribute), false).Length > 0) tearDownMethod = m;
            }

            foreach (var method in testClassType.GetMethods())
            {
                if (method.GetCustomAttributes(typeof(NUnit.Framework.TestAttribute), false).Length > 0)
                {
                    total++;
                    try
                    {
                        setUpMethod?.Invoke(instance, null);
                        method.Invoke(instance, null);
                        tearDownMethod?.Invoke(instance, null);

                        passed++;
                        sb.AppendLine($"[PASS] {testClassType.Name}.{method.Name}");
                        Debug.Log($"<color=green>[PASS] {testClassType.Name}.{method.Name}</color>");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Exception inner = ex.InnerException ?? ex;
                        sb.AppendLine($"[FAIL] {testClassType.Name}.{method.Name} -> {inner.Message}");
                        sb.AppendLine($"       StackTrace: {inner.StackTrace}");
                        Debug.LogError($"[FAIL] {testClassType.Name}.{method.Name} -> {inner.Message}\n{inner.StackTrace}");
                    }
                }
            }
        }
    }
}
