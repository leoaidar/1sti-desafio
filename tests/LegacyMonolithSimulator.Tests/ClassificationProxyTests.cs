using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;
using LegacyMonolithSimulator.Models;
using LegacyMonolithSimulator.Services;

namespace LegacyMonolithSimulator.Tests
{
    public class ClassificationProxyTests
    {
        [Fact]
        public async Task ClassifyAsync_ShouldReturnLegacyMock_WhenFeatureToggleIsFalse()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string?> {
                {"FeatureToggles:UseExternalClassificationService", "false"}
            };
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            // Não precisamos nos preocupar com o HttpClient pois ele nem deve ser chamado
            var httpClient = new HttpClient();
            var proxy = new ClassificationProxy(configuration, httpClient);
            var request = new DocumentClassificationRequest();

            // Act
            var result = await proxy.ClassifyAsync(request);

            // Assert
            Assert.Equal("LEGACY-SYSTEM", result.Classification);
            Assert.Equal(1.0, result.Confidence);
        }

        [Fact]
        public async Task ClassifyAsync_ShouldCallExternalApi_WhenFeatureToggleIsTrue()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string?> {
                {"FeatureToggles:UseExternalClassificationService", "true"},
                {"FeatureToggles:AllowedVerticals:0", "retail"},
                {"ExternalServices:DocumentClassifierUrl", "http://fake-api.com/classify"}
            };
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var expectedResponse = new DocumentClassificationResponse
            {
                Classification = "NFE",
                Confidence = 0.95
            };

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
                });

            var httpClient = new HttpClient(mockHttpMessageHandler.Object);
            var proxy = new ClassificationProxy(configuration, httpClient);
            var request = new DocumentClassificationRequest { SourceVertical = "retail" };

            // Act
            var result = await proxy.ClassifyAsync(request);

            // Assert
            Assert.Equal("NFE", result.Classification);
            Assert.Equal(0.95, result.Confidence);
        }

        [Fact]
        public async Task ClassifyAsync_ShouldThrowException_WhenUrlIsNotConfigured()
        {
            // Arrange
            var inMemorySettings = new Dictionary<string, string?> {
                {"FeatureToggles:UseExternalClassificationService", "true"},
                {"FeatureToggles:AllowedVerticals:0", "retail"},
                {"ExternalServices:DocumentClassifierUrl", null}
            };
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

            var proxy = new ClassificationProxy(configuration, new HttpClient());
            var request = new DocumentClassificationRequest { SourceVertical = "retail" };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.ClassifyAsync(request));
        }
    }
}
