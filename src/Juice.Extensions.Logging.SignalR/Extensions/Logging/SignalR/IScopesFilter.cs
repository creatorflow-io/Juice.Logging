namespace Juice.Extensions.Logging.SignalR
{
    public interface IScopesFilter
    {
        bool IsIncluded(string scope);
    }
}
