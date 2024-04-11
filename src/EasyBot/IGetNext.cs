namespace EasyBot;

internal interface IGetNext
{
    Task<UpdateInfo> NextUpdate(CancellationToken cancel);
}