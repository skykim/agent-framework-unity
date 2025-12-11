using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<MainThreadDispatcher>();
                if (_instance == null)
                {
                    Debug.LogWarning("MainThreadDispatcher not found. Creating new instance.");
                    var go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public static void EnsureInitialized()
    {
        if (_instance != null) return;

        _instance = FindFirstObjectByType<MainThreadDispatcher>();

        if (_instance == null)
        {
            var go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    public void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static void Enqueue(Action action)
    {
        Instance.EnqueueInternal(action);
    }

    public static Task EnqueueAsync(Action action)
    {
        return Instance.EnqueueAsyncInternal(action);
    }

    public static Task<T> EnqueueAsync<T>(Func<T> action)
    {
        return Instance.EnqueueAsyncInternal(action);
    }

    public void EnqueueInternal(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    public Task EnqueueAsyncInternal(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
        }
        return tcs.Task;
    }

    public Task<T> EnqueueAsyncInternal<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>();
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(() =>
            {
                try
                {
                    var result = action();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
        }
        return tcs.Task;
    }
}