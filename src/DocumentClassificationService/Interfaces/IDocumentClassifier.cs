using System.Threading.Tasks;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Interfaces
{
    public interface IDocumentClassifier
    {
        Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request);
    }
}
