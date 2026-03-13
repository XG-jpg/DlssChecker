using System.Threading.Tasks;
using DlssChecker.Models;

namespace DlssChecker.Services;

public interface IVersionRepository
{
    Task<DlssVersionInfo?> GetLatestAsync();
}
