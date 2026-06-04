using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DocumentClassificationService.Models;

namespace DocumentClassification.IntegrationTests
{
    public class DistributedTracingTests : 
        IClassFixture<WebApplicationFactory<DocumentClassificationService.Program>>,
        IClassFixture<WebApplicationFactory<LegacyMonolithSimulator.Program>>
    {
        private readonly WebApplicationFactory<DocumentClassificationService.Program> _serviceFactory;
        private readonly WebApplicationFactory<LegacyMonolithSimulator.Program> _legacyFactory;

        public DistributedTracingTests(
            WebApplicationFactory<DocumentClassificationService.Program> serviceFactory,
            WebApplicationFactory<LegacyMonolithSimulator.Program> legacyFactory)
        {
            _serviceFactory = serviceFactory;
            _legacyFactory = legacyFactory;
        }

        [Fact]
        public async Task Microservice_ShouldGenerateCorrelationId_WhenNotProvided()
        {
            // Arrange
            var client = _serviceFactory.CreateClient();
            var requestPayload = new DocumentClassificationRequest { DocumentType = "invoice", DocumentId = "123" };

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/classify", requestPayload);

            // Assert
            Assert.True(response.Headers.Contains("X-Correlation-ID"), "O header X-Correlation-ID não foi retornado pelo microsserviço.");
            
            var correlationId = response.Headers.GetValues("X-Correlation-ID").First();
            Assert.True(Guid.TryParse(correlationId, out _), "O Correlation ID gerado não é um Guid válido.");
        }

        [Fact]
        public async Task Microservice_ShouldPreserveCorrelationId_WhenProvided()
        {
            // Arrange
            var client = _serviceFactory.CreateClient();
            var requestPayload = new DocumentClassificationRequest { DocumentType = "invoice", DocumentId = "123" };
            var expectedCorrelationId = Guid.NewGuid().ToString();
            
            client.DefaultRequestHeaders.Add("X-Correlation-ID", expectedCorrelationId);

            // Act
            var response = await client.PostAsJsonAsync("/api/v1/classify", requestPayload);

            // Assert
            Assert.True(response.Headers.Contains("X-Correlation-ID"));
            var returnedCorrelationId = response.Headers.GetValues("X-Correlation-ID").First();
            Assert.Equal(expectedCorrelationId, returnedCorrelationId);
        }

        [Fact]
        public async Task EndToEnd_LegacyToMicroservice_ShouldCommunicateWithTracing()
        {
            // Arrange
            // Vamos configurar o Legado para usar o TestServer do Microsserviço como sua "Internet"
            var serviceHandler = _serviceFactory.Server.CreateHandler();

            var configuredLegacyFactory = _legacyFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "FeatureToggles:UseExternalClassificationService", "true" },
                        { "ExternalServices:DocumentClassifierUrl", "http://localhost/api/v1/classify" }
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Força o proxy do legado a usar o handler do TestServer do Microsserviço,
                    // criando uma ponte de comunicação direta em memória entre as duas aplicações!
                    services.AddHttpClient<LegacyMonolithSimulator.Services.ILegacyClassificationProxy, LegacyMonolithSimulator.Services.ClassificationProxy>()
                            .ConfigurePrimaryHttpMessageHandler(() => serviceHandler);
                });
            });

            var legacyClient = configuredLegacyFactory.CreateClient();
            var requestPayload = new DocumentClassificationRequest { DocumentType = "invoice", DocumentId = "123" };

            // Act
            // Enviamos a requisição para a Minimal API do Legado
            var response = await legacyClient.PostAsJsonAsync("/api/legacy/process-document", requestPayload);

            // Assert
            response.EnsureSuccessStatusCode(); // Se deu 200 OK, significa que o Legado chamou o Microserviço com sucesso e desserializou a resposta!
            var result = await response.Content.ReadFromJsonAsync<DocumentClassificationResponse>();
            
            // O Microserviço deve ter respondido, seja com IA real ou fallback local
            Assert.NotNull(result);
            Assert.False(string.IsNullOrEmpty(result.Classification));
        }
    }
}
