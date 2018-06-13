using System.Threading.Tasks;

namespace BinanceBalancer.Models
{
    public interface IBinanceRepository
    {
        Task<bool> IsServerOnline();
        Task<long> GetServerTimeStampInMilliseconds();

    }
}
