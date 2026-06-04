using System.Threading.Tasks;
using LegacyMonolithSimulator.Models;

namespace LegacyMonolithSimulator.Services
{
    public interface ILegacyClassificationProxy
    {
        Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request);
    }
}
