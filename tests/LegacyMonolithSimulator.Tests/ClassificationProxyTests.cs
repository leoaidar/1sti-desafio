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
            var mockConfig = new Mock<IConfiguration>();
            var mockSection = new Mock<IConfigurationSection>();
            mockSection.Setup(s => s.Value).Returns("false");
            mockConfig.Setup(c => c.GetSection("FeatureToggles:UseExternalClassificationService")).Returns(mockSection.Object);

            // Não precisamos nos preocupar com o HttpClient pois ele nem deve ser chamado
            var httpClient = new HttpClient();
            var proxy = new ClassificationProxy(mockConfig.Object, httpClient);
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
            var mockConfig = new Mock<IConfiguration>();
            
            var mockToggleSection = new Mock<IConfigurationSection>();
            mockToggleSection.Setup(s => s.Value).Returns("true");
            mockConfig.Setup(c => c.GetSection("FeatureToggles:UseExternalClassificationService")).Returns(mockToggleSection.Object);
            
            mockConfig.Setup(c => c["ExternalServices:DocumentClassifierUrl"]).Returns("http://fake-api.com/classify");

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
            var proxy = new ClassificationProxy(mockConfig.Object, httpClient);
            var request = new DocumentClassificationRequest();

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
            var mockConfig = new Mock<IConfiguration>();
            
            var mockToggleSection = new Mock<IConfigurationSection>();
            mockToggleSection.Setup(s => s.Value).Returns("true");
            mockConfig.Setup(c => c.GetSection("FeatureToggles:UseExternalClassificationService")).Returns(mockToggleSection.Object);
            
            mockConfig.Setup(c => c["ExternalServices:DocumentClassifierUrl"]).Returns((string?)null); // URL nula

            var proxy = new ClassificationProxy(mockConfig.Object, new HttpClient());
            var request = new DocumentClassificationRequest();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.ClassifyAsync(request));
        }
    }
}
