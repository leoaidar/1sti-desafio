using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Services
{
    public class MockDocumentClassifier : IDocumentClassifier
    {
        public Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            if (request.DocumentType?.ToLower() == "invoice")
            {
                return Task.FromResult(new DocumentClassificationResponse
                {
                    Classification = "NFE",
                    Confidence = 0.99,
                    ModelVersion = "v1-mock",
                    Reasons = new List<string> { "matches invoice layout" }
                });
            }

            return Task.FromResult(new DocumentClassificationResponse
            {
                Classification = "UNKNOWN",
                Confidence = 0.0,
                ModelVersion = "v1-mock",
                Reasons = new List<string> { "unrecognized document type" }
            });
        }
    }
}
