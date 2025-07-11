using System;
using System.Collections.Generic;

public static class UnityMCPMainThread
{
    // 主线程队列
    private static readonly Queue<Action> mainThreadActions = new Queue<Action>();
    
    // 添加到主线程队列
    public static void AddToMainThread(Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }
    
    // 执行主线程队列中的所有操作
    public static void ExecuteMainThreadActions()
    {
        while (true)
        {
            Action action;
            lock (mainThreadActions)
            {
                if (mainThreadActions.Count == 0) return;
                action = mainThreadActions.Dequeue();
            }
            action?.Invoke();
        }
    }
}