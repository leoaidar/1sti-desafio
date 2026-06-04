using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using DocumentClassificationService.Prompts;

namespace DocumentClassificationService.Tests
{
    public class DocumentClassificationPromptBuilderTests
    {
        private readonly DocumentClassificationPromptBuilder _builder = 
            new DocumentClassificationPromptBuilder(NullLogger<DocumentClassificationPromptBuilder>.Instance);

        [Fact]
        public void BuildSystemPrompt_ShouldContainExpectedJsonFormatInstructions()
        {
            // Act
            var prompt = _builder.BuildSystemPrompt();

            // Assert
            Assert.Contains("APENAS um objeto JSON válido", prompt);
            Assert.Contains("\"classification\":", prompt);
            Assert.Contains("\"confidence\":", prompt);
            Assert.Contains("\"modelVersion\":", prompt);
            Assert.Contains("\"reasons\":", prompt);
        }

        [Fact]
        public void BuildUserMessage_ShouldIncludeDocumentContent_WhenContentIsValid()
        {
            // Arrange
            var validContent = "NF-e 12345 Valor R$ 1500,00";

            // Act
            var message = _builder.BuildUserMessage(validContent);

            // Assert
            Assert.Contains("Classifique o seguinte documento:", message);
            Assert.Contains(validContent, message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void BuildUserMessage_ShouldIncludeFallbackMessage_WhenContentIsNullOrWhiteSpace(string content)
        {
            // Act
            var message = _builder.BuildUserMessage(content);

            // Assert
            Assert.Contains("(conteúdo não informado)", message);
        }
    }
}
