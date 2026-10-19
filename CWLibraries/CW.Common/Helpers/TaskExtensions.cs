namespace CW.Common.Helpers;

public static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenAll2<T1, T2>(
        this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }

    public static async Task<(T1, T2, T3)> WhenAll3<T1, T2, T3>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3);
        return (tasks.t1.Result, tasks.t2.Result, tasks.t3.Result);
    }
}
