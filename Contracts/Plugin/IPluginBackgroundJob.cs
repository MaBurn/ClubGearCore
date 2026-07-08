namespace ClubGear.Plugin.Contracts;

public interface IPluginBackgroundJob
{
    Task ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken);
}
