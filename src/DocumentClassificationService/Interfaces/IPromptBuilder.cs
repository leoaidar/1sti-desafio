namespace DocumentClassificationService.Interfaces
{
    /// <summary>
    /// Define o contrato para construção dos prompts enviados a um modelo de IA.
    /// Cada implementação pode customizar o estilo e o contexto do prompt
    /// para um provider específico (Cerebras, Groq, OpenAI, etc.), sem
    /// alterar nenhuma classe classificadora existente.
    /// </summary>
    public interface IPromptBuilder
    {
        /// <summary>
        /// Constrói a mensagem de sistema (system prompt) que define o comportamento do modelo.
        /// </summary>
        string BuildSystemPrompt();

        /// <summary>
        /// Constrói a mensagem do usuário com o conteúdo do documento a ser classificado.
        /// </summary>
        /// <param name="documentContent">Conteúdo bruto do documento.</param>
        string BuildUserMessage(string? documentContent);
    }
}
