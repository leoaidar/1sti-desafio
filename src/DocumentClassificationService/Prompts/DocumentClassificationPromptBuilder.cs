using Microsoft.Extensions.Logging;
using DocumentClassificationService.Interfaces;

namespace DocumentClassificationService.Prompts
{
    /// <summary>
    /// Implementação padrão do prompt de classificação de documentos fiscais.
    /// Compatível com qualquer provider OpenAI-like (Cerebras, Groq, OpenRouter, etc.).
    /// Para criar um prompt especializado (ex: para um modelo fine-tuned),
    /// basta criar uma nova classe que implemente IPromptBuilder e registrá-la
    /// no Program.cs sem alterar nenhuma linha dos classifiers existentes (OCP).
    /// </summary>
    public class DocumentClassificationPromptBuilder : IPromptBuilder
    {
        private readonly ILogger<DocumentClassificationPromptBuilder> _logger;

        public DocumentClassificationPromptBuilder(ILogger<DocumentClassificationPromptBuilder> logger)
        {
            _logger = logger;
        }

        public string BuildSystemPrompt()
        {
            _logger.LogDebug("Construindo um prompt para classificação de documentos fiscais.");

            return @"Atue como um classificador especialista em documentos fiscais e tributários brasileiros.

Analise o conteúdo do documento fornecido e retorne APENAS um objeto JSON válido, sem nenhum texto adicional, comentário ou formatação markdown.

O JSON deve conter exatamente as seguintes propriedades:
{
  ""classification"": ""<tipo do documento: NFE, CTE, BOLETO, CONTRATO, RECIBO, INVOICE, DESCONHECIDO>"",
  ""confidence"": <número entre 0.0 e 1.0 representando sua certeza>,
  ""modelVersion"": ""<identificador do modelo utilizado>"",
  ""reasons"": [""<razão 1>"", ""<razão 2>""]
}

Regras:
- Retorne SOMENTE o JSON, sem blocos de código ou texto extra.
- Se o conteúdo for insuficiente para classificar, use ""DESCONHECIDO"" com baixa confidence.
- Seja preciso e explique as razões de forma objetiva.";
        }

        public string BuildUserMessage(string? documentContent)
        {
            var isEmpty = string.IsNullOrWhiteSpace(documentContent);

            if (isEmpty)
            {
                _logger.LogWarning("BuildUserMessage chamado com conteúdo vazio. O modelo receberá '(conteúdo não informado)'.");
            }
            else
            {
                _logger.LogDebug("Construindo user message com {CharCount} caracteres de conteúdo.", documentContent!.Length);
            }

            var content = isEmpty ? "(conteúdo não informado)" : documentContent!;

            return $"Classifique o seguinte documento:\n\n{content}";
        }
    }
}
