using System.Threading.Tasks;
using Xunit;
using DocumentClassificationService.Services;
using DocumentClassificationService.Models;

namespace DocumentClassificationService.Tests
{
    public class MockDocumentClassifierTests
    {
        [Fact]
        public async Task ClassifyAsync_ShouldReturnNFE_WhenDocumentTypeIsInvoice()
        {
            // Arrange
            var classifier = new MockDocumentClassifier();
            var request = new DocumentClassificationRequest
            {
                DocumentType = "invoice"
            };

            // Act
            var result = await classifier.ClassifyAsync(request);

            // Assert
            Assert.Equal("NFE", result.Classification);
            Assert.Equal(0.99, result.Confidence);
        }

        [Fact]
        public async Task ClassifyAsync_ShouldReturnUnknown_WhenDocumentTypeIsNotInvoice()
        {
            // Arrange
            var classifier = new MockDocumentClassifier();
            var request = new DocumentClassificationRequest
            {
                DocumentType = "receipt"
            };

            // Act
            var result = await classifier.ClassifyAsync(request);

            // Assert
            Assert.Equal("UNKNOWN", result.Classification);
        }
    }
}
