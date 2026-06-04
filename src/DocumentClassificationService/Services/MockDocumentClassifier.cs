using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Services
{
    /// <summary>
    /// Classificador simulado baseado em heurísticas simples.
    /// Utilizado em testes unitários e como fallback de negócio no endpoint.
    /// </summary>
    public class MockDocumentClassifier : IDocumentClassifier
    {
        private readonly ILogger<MockDocumentClassifier> _logger;

        public MockDocumentClassifier(ILogger<MockDocumentClassifier> logger)
        {
            _logger = logger;
        }

        public Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            _logger.LogInformation(
                "MockDocumentClassifier acionado para documento {DocumentId} (Tipo: {DocumentType}).",
                request.DocumentId, request.DocumentType);

            if (request.DocumentType?.ToLower() == "invoice")
            {
                _logger.LogInformation(
                    "Heurística detectou DocumentType=invoice. Classificando documento {DocumentId} como NFE (Confiança: 99%).",
                    request.DocumentId);

                return Task.FromResult(new DocumentClassificationResponse
                {
                    Classification = "NFE",
                    Confidence = 0.99,
                    ModelVersion = "v1-mock",
                    Reasons = new List<string> { "NFE nota fiscal" }
                });
            }

            _logger.LogWarning(
                "DocumentType '{DocumentType}' desconhecido para documento {DocumentId}. Retornando UNKNOWN.",
                request.DocumentType, request.DocumentId);

            return Task.FromResult(new DocumentClassificationResponse
            {
                Classification = "UNKNOWN",
                Confidence = 0.0,
                ModelVersion = "v1-mock",
                Reasons = new List<string> { "Documento desconhecido!" }
            });
        }
    }
}
