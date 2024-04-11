namespace EasyBot;

internal class TaskInfo
{
    internal readonly SemaphoreSlim Semaphore = new(0);
    internal readonly Queue<UpdateInfo> Updates = new();
    internal Task Task = null!;
}