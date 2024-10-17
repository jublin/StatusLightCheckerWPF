namespace TeamsStatusChecker.Services
{
    public interface IMicrosoftTeamsService
    {
        // in seconds
        int PoolingInterval { get; set; }

        Task GetCurrentStatus();
    }
}
