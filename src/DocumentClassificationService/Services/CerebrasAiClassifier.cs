using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Services
{
    public class CerebrasAiClassifier : IDocumentClassifier
    {
        private readonly HttpClient _httpClient;

        public CerebrasAiClassifier(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            try
            {
                // Estrutura básica simulando a classificação via IA
                return Task.FromResult(new DocumentClassificationResponse
                {
                    Classification = "IA-Analyzed",
                    Confidence = 0.95,
                    ModelVersion = "v1-cerebras",
                    Reasons = new List<string> { "processed by Cerebras AI mockup" }
                });
            }
            catch
            {
                // Lança novamente a exceção para que possa ser capturada pelo Polly posteriormente
                throw;
            }
        }
    }
}
