using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamsStatusChecker.Services
{
    public interface IMicrosoftTeamsService
    {
        // in seconds
        int PoolingInterval { get; set; }

        Task<MicrosoftTeamsStatus> GetCurrentStatus();
    }
}
