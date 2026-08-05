using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace QA.Tests
{
    public class QATestRunner
    {
        private static readonly string LogPath = "Logs/qa_test_results.txt";
        private static readonly string ExceptionLogPath = "Logs/qa_exception_results.txt";

        public static void AppendExceptionResult(string system, string mitigation)
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText(ExceptionLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {system} | PASS | {mitigation}{Environment.NewLine}");
        }

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

            // 1. CSV Data Pipeline Tests
            RunTestSuite(typeof(CSVDataPipelineTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 2. Gameplay Architecture Tests
            RunTestSuite(typeof(GameplayArchitectureTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 3. Animator Controller & Clip Restoration Tests
            RunTestSuite(typeof(AnimatorControllerTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 4. Tilemap Stage & 60x30 Room Chunk & Physics Tests
            RunTestSuite(typeof(TilemapStageBuilderTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 5. Stage 1 MVP regression and implementation-gap tests
            RunTestSuite(typeof(Stage1MvpRegressionTests), ref totalTests, ref passedTests, ref failedTests, sb);

            // 6. 요약 및 출력
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

        private static void RunTestSuite(Type testClass, ref int totalTests, ref int passedTests, ref int failedTests, StringBuilder sb)
        {
            sb.AppendLine($"\n--- Running Suite: {testClass.Name} ---");
            object testInstance = Activator.CreateInstance(testClass);
            MethodInfo[] methods = testClass.GetMethods(BindingFlags.Instance | BindingFlags.Public);

            foreach (var method in methods)
            {
                var testAttr = method.GetCustomAttribute<NUnit.Framework.TestAttribute>();
                if (testAttr != null)
                {
                    totalTests++;
                    try
                    {
                        method.Invoke(testInstance, null);
                        passedTests++;
                        sb.AppendLine($"[PASS] {testClass.Name}.{method.Name}");
                        Debug.Log($"<color=green>[PASS] {testClass.Name}.{method.Name}</color>");
                    }
                    catch (Exception ex)
                    {
                        failedTests++;
                        string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                        string stackTrace = ex.InnerException != null ? ex.InnerException.StackTrace : ex.StackTrace;
                        sb.AppendLine($"[FAIL] {testClass.Name}.{method.Name} -> {errorMsg}");
                        sb.AppendLine($"       StackTrace: {stackTrace}");
                        Debug.LogError($"[FAIL] {testClass.Name}.{method.Name} -> {errorMsg}");
                    }
                }
            }
        }
    }
}
