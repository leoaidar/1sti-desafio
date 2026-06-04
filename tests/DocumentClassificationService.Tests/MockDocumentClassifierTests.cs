using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DocumentClassificationService.Services;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Tests
{
    public class MockDocumentClassifierTests
    {
        // descarta todos os logs e mantém o teste focado no comportamento de negócio.
        private readonly MockDocumentClassifier _classifier =
            new MockDocumentClassifier(NullLogger<MockDocumentClassifier>.Instance);

        [Fact]
        public async Task ClassifyAsync_ShouldReturnNFE_WhenDocumentTypeIsInvoice()
        {
            // Arrange
            var request = new DocumentClassificationRequest
            {
                DocumentType = "invoice"
            };

            // Act
            var result = await _classifier.ClassifyAsync(request);

            // Assert
            Assert.Equal("NFE", result.Classification);
            Assert.Equal(0.99, result.Confidence);
        }

        [Fact]
        public async Task ClassifyAsync_ShouldReturnUnknown_WhenDocumentTypeIsNotInvoice()
        {
            // Arrange
            var request = new DocumentClassificationRequest
            {
                DocumentType = "receipt"
            };

            // Act
            var result = await _classifier.ClassifyAsync(request);

            // Assert
            Assert.Equal("UNKNOWN", result.Classification);
        }
    }
}
