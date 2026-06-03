# Arquitetura de Modernização: Extração Módulo de Classificação de Documentos 1.0

# 1. Contexto
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

# 2. Missão
Sua missão é propor e demonstrar uma estratégia técnica para extrair esse módulo de classificação
de documentos para um serviço independente, reduzindo risco de regressão e permitindo adoção
progressiva pelas três verticais.

# 3 Proposta técnica

## 3.1. Visão Geral
Detalha a estratégia de extração do módulo de classificação de documentos do monolito legado pra um serviço independente (.NET 8+), visando modernização progressiva e com capacidades de IA. 

Pra facilitar estruturei o documento respondendo diretamente similar aos pontos levantados no desafio:

### 1. Como você analisaria o módulo atual dentro do monolito?
Pra começar, eu não sairia alterando o código de cara. Faria uma análise estática (usando ferramentas da IDE tipo CoPilot, IA pra analise e melhoria ou algo como NDepend que é muito bom até SonarQube se tiver) pra mapear o tamanho do problema. Objetivo é entender os inputs e outputs do módulo de classificação atual, descobrir quem chama esse legado e os retornos exatos que o monolito espera.

### 2. Como identificaria dependências, pontos de acoplamento e riscos?
Gerar um grafo de dependências usando própio Visual Studio ou libs que fazem isso pra mim via cmd, daí olhando pro grafo de dependências, eu focaria em caçar os "efeitos colaterais". O maior risco num legado de 12 anos é o módulo fazer mais do que deve tipo (ex: persistir dados no banco no meio da classificação ou depender de variáveis globais). Eu identificaria os acoplamentos pra conseguir isolar o classificador e transformar ele num processador puro: entra documento e sai classificação.

### 3. Qual estratégia usaria para extrair o serviço progressivamente?
Vou adotar uma estratégia faseada pra evitar o risco de migração "big bang" citado no desafio:
- **Adaptador de Interface:** Introdução da interface `IDocumentClassifier` no monolito.
- **Proxy Híbrido:** O monolito vai invocar um adapter que decide entre a lógica interna (legado) e a chamada externa (novo serviço) chamando um *endpoint* configurado via *Feature Flag*.
- **Migração Gradual:** Ativação em ambiente de produção específicas (ex: Varejo inicialmente).

### 4. Como definiria o contrato entre o monolito e o novo serviço?
Vou usar algo muito comum que é ter uma API versionada desde o dia zero (ex: `/api/v1/classify`) pra garantir evolução sem quebrar o consumo das três verticais. O contrato via REST (JSON) seguirá o padrão recebendo o request e response proposto no desafio.

### 5. Como garantiria compatibilidade com os três pontos de chamada existentes?
Através da interface `IDocumentClassifier` que mencionei. As verticais de varejo, logística e financeiro não vão nem saber que o serviço mudou. Elas continuam injetando essa interface, e o meu *Proxy* quem faz o roteamento via HTTP e a tradução do retorno do novo serviço pro formato que o monolito já está acostumado.

### 6. Como lidaria com testes em um cenário de baixa cobertura?
#### Garantia de Qualidade
- **Teste de caracterização:** Vou capturar amostra reais de requisições e respostas do sistema legado. Depois, passo essa mesma massa de dados no novo serviço daí faço uma comparação bit-a-bit das saídas. Se bater, fico confiante de que não quebrei as regras de negócio antes da virada definitiva. (*"https://blog.triadworks.com.br/golden-master-testing-testando-codigo-legado"*). 

### 7. Como trataria logs, métricas, tracing, erros e timeouts?
A comunicação com o novo serviço e APIs externas vai seguir padrões de alta disponibilidade:
- **Resiliência:** Vou usar o *Polly* configurando políticas de retentativas *Retry Policies* e *Circuit Breaker* se der *timeout*, o Polly corta a chamada.
- **Fallback Automático:** Caso o microsserviço ou a IA falhem, o sistema volta automaticamente pro classificador do legado (lógica interna).
- **Observabilidade:** Vou usar um `Correlation ID` pra propagar um ID único entre o monolito e o serviço pra *tracing* (OpenTelemetry), além de botar logs estruturados via Serilog pra monitorar erros e etc...

### 8. Como planejaria deploy, rollback e migração gradual?
Vou usar algo que gosto muito *Feature Flags*. Faria o deploy do novo serviço rodando em paralelo, mas inativo. Depois, ativaria a *flag* somente pra uma vertical específica (ex: Varejo inicialmente). O rollback é simples: se os logs apontarem gargalos ou erros, a *flag* é destativada e o tráfego volta todo pro processamento anterior na mesma hora.

### 9. Quais trade-offs você considerou e por quê?
- **REST vs gRPC:** Vou usar REST pela simplicidade de integração com o ambiente legado. Como o monolito é antigo, configurar um cliente HTTP/REST é menos arriscado e mais rápido do que configurar buffers de gRPC.

- **IA Real vs Mock:** Decidi fazer uma implementação híbrida. Integrei uma API de IA real (Cerebras) que já utilizei pra mostrar valor real ao negócio, mas vou manter heurísticas locais prontas como *fallback*. O *trade-off* é adicionar uma dependência de rede externa para ter classificações melhores, mas tipo mitigando o risco de queda com o *fallback*, nesse caso garanto que a resiliência não dependa de terceiros.