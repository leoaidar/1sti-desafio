using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;
using DocumentClassificationService.Utils;

namespace DocumentClassificationService.Services
{
    /// <summary>
    /// Classificador de documentos que utiliza a API Cerebras (compatível com OpenAI).
    /// Depende da abstração IPromptBuilder para construir o prompt,
    /// Agnóstico ao conteúdo das instruções enviadas ao modelo.
    /// </summary>
    public class CerebrasAiClassifier : IDocumentClassifier
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IPromptBuilder _promptBuilder;

        public CerebrasAiClassifier(
            HttpClient httpClient,
            IConfiguration configuration,
            IPromptBuilder promptBuilder)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        }

        public async Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            var baseUrl = _configuration["CerebrasAi:BaseUrl"];
            var apiKey = _configuration["CerebrasAi:ApiKey"];
            var model = _configuration["CerebrasAi:Model"];

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
            {
                throw new InvalidOperationException("Configurações do CerebrasAi incompletas no appsettings.");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Prompt construído pela abstração — o classifier não sabe o que está dentro (OCP)
            var systemPrompt = _promptBuilder.BuildSystemPrompt();
            var userMessage = _promptBuilder.BuildUserMessage(request.Content);

            var payload = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user",   content = userMessage }
                }
            };

            // Dispara a requisição (o Polly lida com as retentativas na camada de infraestrutura)
            var response = await _httpClient.PostAsJsonAsync(baseUrl, payload);
            response.EnsureSuccessStatusCode();

            var aiResponseJson = await response.Content.ReadAsStringAsync();

            using var jsonDoc = JsonDocument.Parse(aiResponseJson);
            var resultContent = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(resultContent))
            {
                throw new Exception("A resposta gerada pelo modelo está vazia.");
            }

            // Higienização delegada ao utilitário JsonSanitizer (SRP)
            resultContent = JsonSanitizer.CleanMarkdown(resultContent);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var finalResponse = JsonSerializer.Deserialize<DocumentClassificationResponse>(resultContent, options);

            if (finalResponse == null)
            {
                throw new Exception("Falha ao desserializar a resposta do modelo para DocumentClassificationResponse.");
            }

            return finalResponse;
        }
    }
}