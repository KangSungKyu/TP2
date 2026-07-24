using System.Collections;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected void Awake()
    {
        if (Instance == null)
        {
            Instance = this as T;

            DontDestroyOnLoad(gameObject);
            OnSingletonAwake();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected void OnDestroy()
    {
        if (Instance == this)
        {
            OnSingletonDestroyed();

            Instance = null;
        }
    }

    // 파생 클래스는 이 훅들만 오버라이드하면 됨
    protected virtual void OnSingletonAwake() { }
    protected virtual void OnSingletonDestroyed() { }
}