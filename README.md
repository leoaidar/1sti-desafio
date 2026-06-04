# 🚀 Arquitetura de Migração: Strangler Fig & AI Document Classification

Este repositório contém a solução para o desafio de modernização de um sistema legado de 12 anos. O objetivo principal é introduzir uma inteligência artificial (LLM) para classificação de documentos fiscais, garantindo zero *downtime*, resiliência arquitetural e total segurança na transição das regras de negócio.

A solução não se limita a conectar uma API externa; ela propõe uma arquitetura de transição segura utilizando os padrões **Strangler Fig**, **Canary Release** e **Shadow Traffic**.

## 🏗️ Decisões Arquiteturais e Padrões (The "Wow" Factor)

* **Roteamento Dinâmico (Canary Release):** Implementação de um proxy no monolito (`ClassificationProxy`) que intercepta o tráfego e decide, baseado em `Feature Flags` e no contexto da requisição (`SourceVertical`), se o documento deve ser processado pelo código legado ou pelo novo microsserviço de IA.
* **Shadow Traffic (Golden Master Testing):** Capacidade de espelhar silenciosamente o tráfego de produção para a nova API de IA, comparando as respostas em *background* sem afetar o cliente final. Isso garante a validação das regras de negócio (Golden Master) antes de virar a chave definitivamente.
* **Resiliência Padrão Enterprise (Polly):** O microsserviço possui um escudo duplo de proteção HTTP:
* **Retry Policy:** Lida com instabilidades transientes da IA.
* **Circuit Breaker:** Corta a comunicação após sucessivas falhas, evitando o esgotamento de *threads* e ativando um *Fallback* estático instantâneo validado por testes de unidade.


* **Rastreamento Distribuído (Distributed Tracing):** Geração e propagação de `X-Correlation-ID` entre o monolito e o microsserviço. Todos os logs (Serilog) são enriquecidos via `LogContext`, permitindo o rastreio da transação ponta a ponta em ferramentas como ELK ou Datadog.
* **SOLID & OCP na Engenharia de Prompts:** A construção do prompt da IA foi abstraída para uma interface `IPromptBuilder`. Isso permite plugar novos provedores (OpenAI, Groq, Anthropic) ou ajustar *System Prompts* sem modificar a classe `CerebrasAiClassifier`.

## 📂 Estrutura da Solução

* `src/LegacyMonolithSimulator/`: Simula a API antiga. Contém o `ClassificationProxy` responsável pelo roteamento inteligente e injeção do *Correlation ID*.
* `src/DocumentClassificationService/`: O novo microsserviço em .NET 8. Responsável por higienizar requisições e integrar com a API de IA (Cerebras) aplicando resiliência e *fallback*.
* `tests/`: Bateria de testes de unidade e integração. Destaque para os testes *End-to-End* que utilizam o `WebApplicationFactory` para criar uma ponte de rede em memória entre as duas aplicações, validando a preservação do cabeçalho de *Tracing* na velocidade da luz.

## 🚀 Como Executar (Docker)

A infraestrutura foi totalmente conteinerizada para garantir consistência entre ambientes. O `docker-compose` está configurado com mapeamento de volumes (Hot Reload de configurações) e *Service Discovery* interno.

1. Clone o repositório.
2. Na raiz do projeto, execute:
```bash
docker compose up --build

```


3. As aplicações estarão disponíveis nas seguintes portas (com Swagger):
* **Monolito Legado:** `http://localhost:5235/swagger`
* **Microsserviço IA:** `http://localhost:5172/swagger`



## 🧪 Como Testar Cenários Dinamicamente (Hot Reload)

A infraestrutura Docker escuta o arquivo `appsettings.json` do host em tempo real. Você não precisa reiniciar os containers para testar o roteamento!

Abra o arquivo `src/LegacyMonolithSimulator/appsettings.json` no seu editor e modifique os parâmetros em tempo real:

1. **Cenário Legado:** Mude `"UseExternalClassificationService": false`. Envie um POST para o monolito e veja a resposta estática imediata.
2. **Cenário Canary (Roteamento por Vertical):** Ligue o *External Service*. Envie um POST com `"sourceVertical": "financeiro"` (cairá no legado) e depois `"sourceVertical": "retail"` (cairá na IA nova).
3. **Cenário Shadow Traffic:** Ligue `"ShadowModeEnabled": true`. O usuário sempre receberá a resposta do monolito de forma segura, mas se você observar os logs do terminal, verá a IA operando nos bastidores e acusando `[Shadow Traffic] Divergência detectada!` caso haja diferença na classificação.

## ✔️ Qualidade e Testes

Execute a suíte completa de testes via terminal:

```bash
dotnet test

```

* **Testes de Unidade:** Cobertura de regras de negócio isoladas, higienização de Markdown (Regex) e Mock avançado de `HttpMessageHandler` para validar falhas de rede sem depender de conexões reais.
* **Testes de Integração:** Subida de ambiente isolado via `TestServer` garantindo a comunicação HTTP e a propagação de contextos de observabilidade.