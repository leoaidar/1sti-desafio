using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Services
{
    public class CerebrasAiClassifier : IDocumentClassifier
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CerebrasAiClassifier(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            try
            {
                var baseUrl = _configuration["CerebrasAi:BaseUrl"];
                var apiKey = _configuration["CerebrasAi:ApiKey"];
                var model = _configuration["CerebrasAi:Model"];

                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
                {
                    throw new InvalidOperationException("Configurações do CerebrasAi incompletas no appsettings.");
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var prompt = @"Atue como um classificador de documentos fiscais.
Analise o seguinte conteúdo de um documento e retorne APENAS um JSON válido com as seguintes propriedades:
- classification (string)
- confidence (double)
- modelVersion (string)
- reasons (array de strings)

Não retorne NENHUM outro texto ou formatação além do JSON.";

                var payload = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = prompt },
                        new { role = "user", content = $"Conteúdo do documento:\n{request.Content}" }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(baseUrl, payload);
                response.EnsureSuccessStatusCode();

                var aiResponseJson = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                using var jsonDoc = JsonDocument.Parse(aiResponseJson);
                
                // Se a resposta vier do Fallback do Polly, ela não terá a raiz "choices"
                if (!jsonDoc.RootElement.TryGetProperty("choices", out var choicesElement))
                {
                    var fallbackResponse = JsonSerializer.Deserialize<DocumentClassificationResponse>(aiResponseJson, options);
                    if (fallbackResponse != null && fallbackResponse.Classification != null)
                    {
                        return fallbackResponse;
                    }
                    throw new Exception("Resposta inesperada do serviço ou do fallback.");
                }

                var resultContent = choicesElement[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(resultContent))
                {
                    throw new Exception("A resposta gerada pelo modelo está vazia.");
                }

                // Limpeza para evitar falha se o modelo retornar em formato markdown de código (```json ... ```)
                resultContent = resultContent.Trim();
                if (resultContent.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                {
                    resultContent = resultContent.Substring(7);
                    if (resultContent.EndsWith("```"))
                    {
                        resultContent = resultContent.Substring(0, resultContent.Length - 3);
                    }
                }
                else if (resultContent.StartsWith("```"))
                {
                    resultContent = resultContent.Substring(3);
                    if (resultContent.EndsWith("```"))
                    {
                        resultContent = resultContent.Substring(0, resultContent.Length - 3);
                    }
                }

                var finalResponse = JsonSerializer.Deserialize<DocumentClassificationResponse>(resultContent, options);

                if (finalResponse == null)
                {
                    throw new Exception("Falha ao desserializar a resposta do modelo para DocumentClassificationResponse.");
                }

                return finalResponse;
            }
            catch
            {
                throw;
            }
        }
    }
}
