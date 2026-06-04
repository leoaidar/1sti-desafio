using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using LegacyMonolithSimulator.Models;

namespace LegacyMonolithSimulator.Services
{
    public class ClassificationProxy : ILegacyClassificationProxy
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ClassificationProxy(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            bool useExternalService = _configuration.GetValue<bool>("FeatureToggles:UseExternalClassificationService");

            if (useExternalService)
            {
                string? url = _configuration["ExternalServices:DocumentClassifierUrl"];
                if (string.IsNullOrEmpty(url))
                {
                    throw new InvalidOperationException("A URL do serviço externo não está configurada.");
                }

                // Faz a requisição POST serializando o objeto request e já aguarda o retorno
                var response = await _httpClient.PostAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();

                // Desserializa a resposta
                var classificationResponse = await response.Content.ReadFromJsonAsync<DocumentClassificationResponse>();
                
                return classificationResponse ?? new DocumentClassificationResponse 
                { 
                    Classification = "UNKNOWN",
                    Reasons = new List<string> { "Erro na desserialização da resposta do serviço." }
                };
            }

            // Simulação da lógica antiga do monolito
            return new DocumentClassificationResponse
            {
                Classification = "LEGACY-SYSTEM",
                Confidence = 1.0,
                Reasons = new List<string> { "Processao pelo monolito de 12 anos..." }
            };
        }
    }
}
