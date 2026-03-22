using AkilliKutuphane.Business.Models;

namespace AkilliKutuphane.Business.Services.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}
