1. Contexto
Você acabou de entrar em uma empresa de software que possui um monolito .NET legado com 12
anos de história. Esse sistema atende três verticais de negócio dentro do mesmo codebase: varejo,
logística e financeiro.
O sistema está em produção 24/7, possui baixa cobertura de testes automatizados, múltiplos times
alterando o mesmo código e alta criticidade operacional.
A empresa decidiu iniciar uma jornada de modernização progressiva, extraindo capacidades do
monolito para serviços independentes. Além disso, deseja criar uma camada de serviços de IA
reutilizáveis por todas as verticais.
O primeiro serviço escolhido para extração é um módulo de classificação de documentos fiscais,
atualmente embutido no monolito e chamado por três fluxos diferentes do sistema legado.
2. Missão
Sua missão é propor e demonstrar uma estratégia técnica para extrair esse módulo de classificação
de documentos para um serviço independente, reduzindo risco de regressão e permitindo adoção
progressiva pelas três verticais.

3.1 Proposta técnica
