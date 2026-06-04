using Xunit;
using DocumentClassificationService.Utils;

namespace DocumentClassificationService.Tests
{
    public class JsonSanitizerTests
    {
        [Theory]
        [InlineData("{\"key\":\"value\"}", "{\"key\":\"value\"}")] // JSON limpo não deve ser alterado
        [InlineData("```json\n{\"key\":\"value\"}\n```", "{\"key\":\"value\"}")] // Bloco markdown específico de json
        [InlineData("```\n{\"key\":\"value\"}\n```", "{\"key\":\"value\"}")] // Bloco markdown genérico
        [InlineData("  ```json\n{\"key\":\"value\"}\n```  ", "{\"key\":\"value\"}")] // Com espaços em branco ao redor
        [InlineData("```JSON\n{\"key\":\"value\"}\n```", "{\"key\":\"value\"}")] // Case insensitive
        public void CleanMarkdown_ShouldRemoveMarkdownAndTrimSpaces(string input, string expected)
        {
            // Act
            var result = JsonSanitizer.CleanMarkdown(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void CleanMarkdown_ShouldReturnSameInput_WhenNullOrWhiteSpace(string input)
        {
            // Act
            var result = JsonSanitizer.CleanMarkdown(input);

            // Assert
            Assert.Equal(input, result);
        }
    }
}
