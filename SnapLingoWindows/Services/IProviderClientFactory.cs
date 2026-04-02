namespace SnapLingoWindows.Services;

public interface IProviderClientFactory
{
    IProviderClient CurrentClient();
}
