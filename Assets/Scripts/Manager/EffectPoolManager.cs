using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인게임 동적 생성 이펙트/파티클/스킬 비주얼 오브젝트 통합 오브젝트 풀링 매니저.
/// 룸/스테이지 전환 시 잔여 이펙트를 100% 비활성화 및 복원하여 메모리 낭비와 잔여물 버그를 차단합니다.
/// </summary>
public class EffectPoolManager : Singleton<EffectPoolManager>
{
    private readonly Dictionary<string, Queue<GameObject>> poolDict = new Dictionary<string, Queue<GameObject>>();
    private readonly HashSet<GameObject> activeEffects = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, uint> effectGenerations = new Dictionary<GameObject, uint>();
    private readonly Dictionary<GameObject, SkillEffect> skillEffects = new Dictionary<GameObject, SkillEffect>();

    public SkillEffect GetPooledSkillEffect(string key, Vector3 position)
    {
        var effectObj = GetPooledEffect(key, position);
        return effectObj != null && skillEffects.TryGetValue(effectObj, out var effect) ? effect : null;
    }

    public void TrackSkillEffect(SkillEffect effect, string key)
    {
        if (effect == null) return;
        skillEffects[effect.gameObject] = effect;
        TrackEffect(effect.gameObject, key);
    }

    public GameObject GetPooledEffect(string key, Vector3 position, Quaternion rotation = default)
    {
        if (string.IsNullOrEmpty(key) || !poolDict.TryGetValue(key, out var queue) || queue.Count == 0) return null;
        var effectObj = queue.Dequeue();
        if (effectObj == null) return null;
        effectObj.transform.SetPositionAndRotation(position, rotation);
        effectObj.SetActive(true);
        TrackEffect(effectObj, key);
        return effectObj;
    }

    public uint TrackEffect(GameObject effectObj, string key)
    {
        if (effectObj == null || string.IsNullOrEmpty(key)) return 0;
        effectObj.name = key;
        activeEffects.Add(effectObj);
        effectGenerations.TryGetValue(effectObj, out uint generation);
        effectGenerations[effectObj] = ++generation;
        return generation;
    }

    public async Cysharp.Threading.Tasks.UniTask<GameObject> SpawnEffect(string prefabKey, Vector3 position,
        Quaternion rotation = default, float duration = 1.0f, Transform parent = null)
    {
        if (string.IsNullOrEmpty(prefabKey)) return null;

        GameObject effectObj = null;

        if (poolDict.TryGetValue(prefabKey, out var queue) && queue.Count > 0)
        {
            effectObj = queue.Dequeue();
            if (effectObj != null)
            {
                effectObj.transform.position = position;
                effectObj.transform.rotation = rotation;
                if (parent != null) effectObj.transform.SetParent(parent);
            }
        }

        if (effectObj == null && ResourceManager.Instance != null)
        {
            effectObj = await ResourceManager.Instance.InstantiateAsyncTask(prefabKey, parent != null ? parent : transform, position, rotation);
            if (effectObj != null) effectObj.name = prefabKey;
        }

        if (effectObj != null)
        {
            effectObj.SetActive(true);
            uint generation = TrackEffect(effectObj, prefabKey);
            if (duration > 0f)
            {
                AutoDespawnEffectAsync(effectObj, generation, duration, this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        return effectObj;
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid AutoDespawnEffectAsync(GameObject effectObj, uint generation, float duration, System.Threading.CancellationToken cancellationToken)
    {
        await Cysharp.Threading.Tasks.UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: cancellationToken);
        if (effectObj != null && effectGenerations.TryGetValue(effectObj, out uint current) && current == generation)
            DespawnEffect(effectObj);
    }

    public GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        string key = prefab.name;
        GameObject effectObj = null;

        if (poolDict.TryGetValue(key, out var queue) && queue.Count > 0)
        {
            effectObj = queue.Dequeue();
            if (effectObj != null)
            {
                effectObj.transform.position = position;
                effectObj.transform.rotation = rotation;
                if (parent != null) effectObj.transform.SetParent(parent);
                effectObj.SetActive(true);
            }
        }

        if (effectObj == null)
        {
            effectObj = Instantiate(prefab, position, rotation, parent != null ? parent : transform);
            effectObj.name = key;
        }

        TrackEffect(effectObj, key);
        return effectObj;
    }

    public void DespawnEffect(GameObject effectObj)
    {
        if (effectObj == null || !activeEffects.Remove(effectObj)) return;

        string key = effectObj.name;
        if (skillEffects.TryGetValue(effectObj, out SkillEffect skillEffect)) skillEffect.ResetVisual();
        effectObj.SetActive(false);
        effectObj.transform.SetParent(transform);

        if (!poolDict.TryGetValue(key, out var queue))
        {
            queue = new Queue<GameObject>();
            poolDict[key] = queue;
        }

        queue.Enqueue(effectObj);
    }

    public void ClearAllActiveEffects()
    {
        var activeList = new List<GameObject>(activeEffects);
        foreach (var obj in activeList)
        {
            if (obj != null)
            {
                DespawnEffect(obj);
            }
        }
        activeEffects.Clear();
    }
}
