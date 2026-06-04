using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Models;
using DocumentClassificationService.Utils;

namespace DocumentClassificationService.Services
{
    /// <summary>
    /// Classificador de documentos que utiliza a API Cerebras (compatível com OpenAI).
    /// Depende da abstração IPromptBuilder para construir o prompt,
    /// Agnóstico ao conteúdo das instruções enviadas ao modelo. (OCP)
    /// </summary>
    public class CerebrasAiClassifier : IDocumentClassifier
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IPromptBuilder _promptBuilder;
        private readonly ILogger<CerebrasAiClassifier> _logger;

        public CerebrasAiClassifier(
            HttpClient httpClient,
            IConfiguration configuration,
            IPromptBuilder promptBuilder,
            ILogger<CerebrasAiClassifier> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DocumentClassificationResponse> ClassifyAsync(DocumentClassificationRequest request)
        {
            var baseUrl = _configuration["CerebrasAi:BaseUrl"];
            var apiKey = _configuration["CerebrasAi:ApiKey"];
            var model = _configuration["CerebrasAi:Model"];

            if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
            {
                _logger.LogCritical("Configurações do CerebrasAi incompletas. Verifique BaseUrl, ApiKey e Model no appsettings.");
                throw new InvalidOperationException("Configurações do CerebrasAi incompletas no appsettings.");
            }

            _logger.LogDebug("Configurações carregadas. BaseUrl={BaseUrl}, Model={Model}", baseUrl, model);

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
            _logger.LogInformation(
                "Enviando documento {DocumentId} (Tipo: {DocumentType}) para a IA. Modelo: {Model}, Endpoint: {BaseUrl}",
                request.DocumentId, request.DocumentType, model, baseUrl);

            var stopwatch = Stopwatch.StartNew();

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(baseUrl, payload);
                stopwatch.Stop();

                _logger.LogInformation(
                    "Resposta da IA recebida para documento {DocumentId}. StatusCode: {StatusCode}, Latência: {ElapsedMs}ms",
                    request.DocumentId, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex,
                    "Falha HTTP ao chamar a IA para documento {DocumentId}. Latência: {ElapsedMs}ms. StatusCode: {StatusCode}",
                    request.DocumentId, stopwatch.ElapsedMilliseconds, ex.StatusCode);
                throw;
            }

            var aiResponseJson = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("JSON bruto recebido da IA para documento {DocumentId}: {RawJson}", request.DocumentId, aiResponseJson);

            using var jsonDoc = JsonDocument.Parse(aiResponseJson);
            var resultContent = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(resultContent))
            {
                _logger.LogError("A IA retornou uma resposta vazia para o documento {DocumentId}.", request.DocumentId);
                throw new Exception("A resposta gerada pelo modelo está vazia.");
            }

            // Higienização delegada ao utilitário JsonSanitizer (SRP)
            var sanitized = JsonSanitizer.CleanMarkdown(resultContent);
            if (sanitized != resultContent)
            {
                _logger.LogDebug("Markdown removido da resposta da IA para documento {DocumentId}.", request.DocumentId);
            }
            resultContent = sanitized;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            DocumentClassificationResponse? finalResponse;
            try
            {
                finalResponse = JsonSerializer.Deserialize<DocumentClassificationResponse>(resultContent, options);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Falha ao desserializar o JSON da IA para documento {DocumentId}. Conteúdo recebido: {Content}",
                    request.DocumentId, resultContent);
                throw;
            }

            if (finalResponse == null)
            {
                _logger.LogError("Desserialização retornou null para documento {DocumentId}. Conteúdo: {Content}", request.DocumentId, resultContent);
                throw new Exception("Falha ao desserializar a resposta do modelo para DocumentClassificationResponse.");
            }

            _logger.LogInformation(
                "Classificação concluída para documento {DocumentId}. Resultado: {Classification}, Confiança: {Confidence:P0}, ModelVersion: {ModelVersion}",
                request.DocumentId, finalResponse.Classification, finalResponse.Confidence, finalResponse.ModelVersion);

            return finalResponse;
        }
    }
}