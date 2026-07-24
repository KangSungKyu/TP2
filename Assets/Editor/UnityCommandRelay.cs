#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 유니티 에디터(GUI)가 열려있는 동안에도 백그라운드 소켓 명령을 받아 
/// Addressable 전수 등록 및 번들 빌드/배포를 메인 스레드에서 100% 자동 즉시 실행해 주는 에디터 릴레이 서버.
/// (BeginAcceptTcpClient 기반 비동기 소켓 - GUI 블로킹 완벽 해결)
/// </summary>
[InitializeOnLoad]
public static class UnityCommandRelay
{
    private static TcpListener tcpListener;
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
    private static int port = 8080;

    static UnityCommandRelay()
    {
        EditorApplication.update += UpdateQueue;
        StartServer();
    }

    private static void StartServer()
    {
        try
        {
            if (tcpListener != null) return;

            tcpListener = new TcpListener(IPAddress.Loopback, port);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(OnAcceptTcpClient, null);

            Debug.Log($"<color=cyan><b>[UnityCommandRelay] 유니티 에디터 소켓 릴레이 준비 완료 (127.0.0.1:{port})</b></color>");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityCommandRelay] 소켓 바인딩 경고 (포트 {port}): {ex.Message}");
        }
    }

    private static void OnAcceptTcpClient(IAsyncResult ar)
    {
        if (tcpListener == null) return;
        try
        {
            TcpClient client = tcpListener.EndAcceptTcpClient(ar);
            tcpListener.BeginAcceptTcpClient(OnAcceptTcpClient, null);
            ThreadPool.QueueUserWorkItem((_) => HandleClient(client));
        }
        catch { }
    }

    private static void HandleClient(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
        {
            string requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine)) return;

            string rawUrl = requestLine.Split(' ')[1].ToLower();
            string responseMessage = "";
            AutoResetEvent handle = new AutoResetEvent(false);

            if (rawUrl.Contains("register"))
            {
                EnqueueAction(() =>
                {
                    try
                    {
                        AddressableAutoRegister.RegisterAllAddressables();
                        responseMessage = "SUCCESS: Addressable Auto Register Completed!";
                    }
                    catch (Exception e)
                    {
                        responseMessage = $"ERROR: {e.Message}";
                    }
                    handle.Set();
                });
                handle.WaitOne(15000);
            }
            else if (rawUrl.Contains("build") || rawUrl.Contains("deploy") || rawUrl.Contains("all"))
            {
                EnqueueAction(() =>
                {
                    try
                    {
                        AddressableAutoRegister.RegisterAllAddressables();
                        AddressablesDeployer.BuildAndDeploy();
                        responseMessage = "SUCCESS: Full Addressables Auto Register + Build + Deploy Completed!";
                    }
                    catch (Exception e)
                    {
                        responseMessage = $"ERROR: {e.Message}";
                    }
                    handle.Set();
                });
                handle.WaitOne(15000);
            }
            else
            {
                responseMessage = "UnityCommandRelay Active (127.0.0.1:8080).";
            }

            byte[] bodyBytes = Encoding.UTF8.GetBytes(responseMessage);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: text/plain; charset=utf-8");
            writer.WriteLine($"Content-Length: {bodyBytes.Length}");
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Flush();
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }
    }

    private static void EnqueueAction(Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private static void UpdateQueue()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                Action act = executionQueue.Dequeue();
                try
                {
                    act?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityCommandRelay] Execution Error: {ex.Message}");
                }
            }
        }
    }
}
#endif
