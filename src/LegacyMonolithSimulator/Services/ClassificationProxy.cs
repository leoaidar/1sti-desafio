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
            bool isShadowModeEnabled = _configuration.GetValue<bool>("FeatureToggles:ShadowModeEnabled");
            bool useExternalService = _configuration.GetValue<bool>("FeatureToggles:UseExternalClassificationService");

            var allowedVerticals = _configuration.GetSection("FeatureToggles:AllowedVerticals").Get<List<string>>() ?? new List<string>();
            bool isVerticalAllowed = allowedVerticals.Any(v => string.Equals(v, request.SourceVertical, StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"[ClassificationProxy] ClassifyAsync chamado para o Documento ID: '{request.DocumentId}', Tipo: '{request.DocumentType}', Vertical: '{request.SourceVertical}'");
            Console.WriteLine($"[ClassificationProxy] Valores do appsettings -> ShadowModeEnabled: {isShadowModeEnabled}, UseExternalClassificationService: {useExternalService}, AllowedVerticals: [{string.Join(", ", allowedVerticals)}]");
            Console.WriteLine($"[ClassificationProxy] Vertical '{request.SourceVertical}' está liberada nos Feature Toggles? {isVerticalAllowed}");

            // 1. ROTEAMENTO DEFINITIVO (MIGRAÇÃO CONCLUÍDA PARA A VERTICAL)
            // Se não estamos em modo sombra, e a vertical está liberada -> Rota 100% Nova (Zero custo legado)
            if (!isShadowModeEnabled && useExternalService && isVerticalAllowed)
            {
              Console.WriteLine($"[ClassificationProxy] Roteamento DEFINITIVO ativado. Encaminhando transação diretamente para o serviço externo.");
              return await CallExternalServiceAsync(request, isShadowTraffic: false);
            }

            // ========================================================================
            // Se o código chegou até aqui, significa que a resposta oficial SERÁ do Legado.
            // Pode ser porque a vertical não está liberada, ou porque estamos em Shadow Mode.
            // AGORA SIM, faz sentido gastar processamento rodando o sistema antigo.
            // ========================================================================

            Console.WriteLine($"[ClassificationProxy] Rota oficial do Legado selecionada. Executando processamento legado.");

            var legacyResponse = new DocumentClassificationResponse
            {
              Classification = "LEGACY-SYSTEM",
              Confidence = 1.0,
              Reasons = new List<string> { "Processado pelo monolito de 12 anos..." }
            };

            // 2. SHADOW TRAFFIC (FASE DE TESTES SILENCIOSOS)
            if (isShadowModeEnabled)
            {
              try
              {
                var aiResponse = await CallExternalServiceAsync(request, isShadowTraffic: true);

                if (aiResponse.Classification != legacyResponse.Classification)
                {
                  Console.WriteLine($"[Shadow Traffic] Divergência detectada! Legado: {legacyResponse.Classification}, IA: {aiResponse.Classification}");
                }
              }
              catch (Exception ex)
              {
                Console.WriteLine($"[Shadow Traffic] Erro isolado (não afetará o legado): {ex.Message}");
              }
            }

            // 3. FALLBACK NATURAL (Retorna o legado oficial)
            return legacyResponse;
        }

        private async Task<DocumentClassificationResponse> CallExternalServiceAsync(DocumentClassificationRequest request, bool isShadowTraffic)
        {
            string? url = _configuration["ExternalServices:DocumentClassifierUrl"];
            if (string.IsNullOrEmpty(url))
            {
                throw new InvalidOperationException("A URL do serviço externo não está configurada.");
            }

            // 1. Gera o Correlation ID para rastrear a transação de ponta a ponta
            var correlationId = Guid.NewGuid().ToString();
            string logPrefix = isShadowTraffic ? "[Shadow Traffic]" : "[Trace]";
            Console.WriteLine($"{logPrefix} Iniciando transação [CorrID: [{correlationId}]] via IA externa.");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Content = JsonContent.Create(request);
            requestMessage.Headers.Add("X-Correlation-ID", correlationId);

            // Faz a requisição POST serializando o objeto request e já aguarda o retorno
            var response = await _httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            // Desserializa a resposta
            var classificationResponse = await response.Content.ReadFromJsonAsync<DocumentClassificationResponse>();
            
            return classificationResponse ?? new DocumentClassificationResponse 
            { 
                Classification = "UNKNOWN",
                Reasons = new List<string> { "Erro na desserialização da resposta do serviço." }
            };
        }
    }
}
