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
using DocumentClassificationService.Utils;

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

      // Dispara a requisição (O Polly lida com as retentativas)
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

      // Limpeza abstraída para a classe utilitária JsonSanitizer (Regex Helper)
      resultContent = JsonSanitizer.CleanMarkdown(resultContent);

      var options = new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      };

      var finalResponse = JsonSerializer.Deserialize<DocumentClassificationResponse>(resultContent, options);

      if (finalResponse == null)
      {
        throw new Exception("Falha ao desserializar a resposta do modelo para DocumentClassificationResponse.");
      }

      return finalResponse;
    }
  }
}