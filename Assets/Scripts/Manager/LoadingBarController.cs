using UnityEngine;

public class LoadingBarController : Singleton<LoadingBarController>
{
    private LoadingScene _activeScene;

    /// <summary>
    /// Register the currently active LoadingScene instance.
    /// </summary>
    public void Register(LoadingScene scene)
    {
        _activeScene = scene;
    }

    /// <summary>
    /// Unregister when the LoadingScene is disabled.
    /// </summary>
    public void Unregister()
    {
        _activeScene = null;
    }

    /// <summary>
    /// Called by GameSceneManager to forward the async load progress.
    /// </summary>
    public void SetProgress(float progress)
    {
        _activeScene?.SetProgress(progress);
    }
}
