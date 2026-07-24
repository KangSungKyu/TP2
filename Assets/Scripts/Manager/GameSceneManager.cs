using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using System.Threading;
using Cysharp.Threading.Tasks;

public class GameSceneManager : Singleton<GameSceneManager>
{
    [SerializeField]
    private readonly string loadingScene = "LoadingScene";

    public enum SceneName
    {
        Init,
        Hub,
        Main
    }

    /// <summary>
    /// Transitions to the specified scene via the LoadingScene.
    /// Handles fade‑in/out already defined in LoadingScene.
    /// </summary>
    public async UniTask TransitionTo(SceneName target)
    {
        // Map enum to scene addressable names (assumes scenes are addressable by these keys)
        string targetSceneKey = target switch
        {
            SceneName.Init => "InitScene",
            SceneName.Hub => "HubScene",
            SceneName.Main => "MainScene",
            _ => "HubScene"
        };

        // Load loading scene first
        await Addressables.LoadSceneAsync(loadingScene);
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3)); // allow fade‑in

        // Load target scene using Addressables (fallback to SceneManager if needed)
        var handle = Addressables.LoadSceneAsync(targetSceneKey, LoadSceneMode.Single);
        await handle.Task;

        // Wait for fade‑out in LoadingScene before finishing
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.2));
    }

    public async UniTask LoadSceneAsync(AssetReference sceneRef, CancellationToken cancellationToken = default)
    {
        // 1. 로딩 씬으로 먼저 이동
        SceneManager.LoadScene(loadingScene);

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.5), cancellationToken: cancellationToken);

        try
        {
            var handle = Addressables.LoadSceneAsync(sceneRef, LoadSceneMode.Single);

            await handle.ToUniTask(
                progress: Progress.Create<float>(p =>
                {
                    //load progress
                }),
                cancellationToken: cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("scene loading is cancel from system");
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }

}