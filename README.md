# 🚀 Desafio 1sti - Arquitetura de Migração: Document Classification Challenge & Strangler Fig & Canary Release & Shadow Traffic & Circuit Breaker & Fallback & Observabilidade & Testes

Este repositório contém a prova de conceito para a modernização de um sistema legado de 12 anos. Implementamos uma arquitetura de transição segura que introduz uma Inteligência Artificial para classificação de documentos fiscais sem causar *downtime* ou afetar a experiência do usuário final.

> 💡 **Nota Arquitetural:** Para uma visão aprofundada sobre a estratégia de migração, diagramas de fluxo, padrões utilizados (Strangler Fig, Canary Release, Shadow Traffic) e o racional de negócios, consulte nosso [Documento de Arquitetura Oficial (ARCHITECTURE.md)](ARCHITECTURE.md).

---
## 📥 Obtendo o Código-Fonte

Para começar a explorar a arquitetura na sua máquina, o primeiro passo é clonar este repositório. Abra o seu terminal e execute:

```bash
git clone https://github.com/leoaidar/1sti-desafio.git
cd 1sti-desafio

```

> **Alternativa:** Se você não estiver usando o Git via linha de comando no momento, pode baixar o projeto completo em formato ZIP clicando em **[Code > Download ZIP](https://github.com/leoaidar/1sti-desafio/archive/refs/heads/main.zip)** no topo desta página.

---

## 🛠️ Como Executar o Projeto

A infraestrutura foi totalmente conteinerizada com Docker. Para subir o ecossistema completo (Monolito Simulado + Microsserviço de IA), execute na raiz do projeto:

### Pré-requisitos
*   [Docker Desktop](https://www.docker.com/products/docker-desktop) instalado e rodando.


### Passos

```bash
docker compose up --build

```

Os serviços estarão disponíveis nas seguintes portas:

* **Microsserviço de IA:** [http://localhost:5172/swagger](http://localhost:5172/swagger)
* **Monolito (Proxy):** [http://localhost:5235/swagger](http://localhost:5235/swagger)

---

## 🕹️ Brincando com a Aplicação (Testando Comportamentos)

O poder dessa arquitetura está no controle dinâmico do tráfego. Você pode simular diversos cenários de produção manipulando os arquivos `appsettings.json` de cada projeto.

### 🗺️ Onde encontrar os arquivos de configuração?

Antes de iniciar os testes dinâmicos, é importante saber onde estão as "chaves" do sistema. Todas as simulações de roteamento, feature flags e resiliência ocorrem através da alteração das variáveis nestes dois arquivos principais:

* **Configurações do Monolito (Proxy e Roteamento):**
    [src/LegacyMonolithSimulator/appsettings.json](src/LegacyMonolithSimulator/appsettings.json)

* **Configurações do Microsserviço de IA (Modelos e Resiliência):**
    [src/DocumentClassificationService/appsettings.json](src/DocumentClassificationService/appsettings.json)

⚠️ **Aviso:** Sempre que alterar os valores nos arquivos `appsettings.json`, reinicie os containers para garantir que as novas variáveis de ambiente sejam recarregadas:

```bash
docker compose down
docker compose up --build

```

### 1. Controlando o Roteamento (No Monolito)

No arquivo `src/LegacyMonolithSimulator/appsettings.json`:

* `UseExternalClassificationService`: Liga (`true`) ou desliga (`false`) a nova API. Se `false`, **todo** o tráfego é processado pelo legado.
* `AllowedVerticals`: Define quais verticais (ex: `"Varejo"`) estão homologadas para usar a IA (Canary Release).
* `ShadowModeEnabled`: Se `true`, o usuário sempre recebe a resposta do sistema antigo, mas a IA processa o documento silenciosamente em *background*. Se houver divergência nas respostas, um log de alerta é disparado.

### 2. Testando Resiliência e Fallback (No Microsserviço)

No arquivo `src/DocumentClassificationService/appsettings.json`:

* Troque o valor da chave `"Model"` de `"gpt-oss-120b"` (Modelo Bom) para `"llama3.1-8b"` (Modelo Ruim/Inexistente).
* Ao fazer isso, você forçará a falha da IA. O **Polly** fará as retentativas e, após 3 falhas, abrirá o **Circuit Breaker**. A aplicação então acionará o **Fallback** local automaticamente, retornando a classificação segura sem derrubar o sistema.

---

## 📡 Exemplos de Request e Response

Faça as requisições apontando para a porta do **Microsserviço (5172)** ou do **Monolito (5235)** para ver o roteador em ação.

### Cenário 1: Sucesso com a IA (Vertical Autorizada)

Quando enviamos uma requisição com a vertical `"Varejo"`, o proxy permite a passagem e a IA classifica o documento com alta confiança.

**Request:**

```bash
curl -X 'POST' \
  'http://localhost:5235/api/legacy/process-document' \
  -H 'accept: */*' \
  -H 'Content-Type: application/json' \
  -d '{
  "documentId": "123",
  "documentType": "unknown",
  "content": "NOTA FISCAL DE SERVIÇOS ELETRÔNICA - NFS-e. Prestador: Aidar Enterprises. CNPJ: 12.345.678/0001-90. Valor: R$ 5.000,00. Descrição: Consultoria em desenvolvimento de software.",
  "sourceVertical": "varejo"
}'

```

**Response (IA - tax-doc-classifier):**

```json
{
  "classification": "NFE",
  "confidence": 0.96,
  "modelVersion": "tax-doc-classifier-1.0",
  "reasons": [
    "O documento contém a expressão 'Nota Fiscal de Serviços Eletrônica - NFS-e', indicando um tipo de nota fiscal eletrônica",
    "Apresenta dados típicos de nota fiscal (CNPJ, valor, descrição de serviço)"
  ]
}

```

### Cenário 2: Roteamento para o Legado (Vertical Não Autorizada)

Se enviarmos o mesmo payload, mas com a vertical `"financeiro"` (que não está no array de `AllowedVerticals`), a requisição nem encosta na IA, poupando custos, e devolve a resposta do monolito de 12 anos.

**Response (Monolito):**

```json
{
  "classification": "LEGACY-SYSTEM",
  "confidence": 1.0,
  "modelVersion": "legacy-v1",
  "reasons": [
    "Processado pelo monolito de 12 anos..."
  ]
}

```

### Cenário 3: Falha da IA e Acionamento do Fallback

Se você configurou um modelo inválido no `appsettings` para simular uma queda da IA, o circuito é aberto e o Mock local assume instantaneamente.

**Response (Fallback Mock):**

```json
{
  "classification": "UNKNOWN",
  "confidence": 0,
  "modelVersion": "v1-mock",
  "reasons": [
    "Documento desconhecido!"
  ]
}

```

---

## 🛡️ Observabilidade e Logs Estruturados

Foi utilizado **Serilog** com injeção de **Correlation IDs**.
Se uma transação começa no Monolito e falha lá no Microsserviço, o cabeçalho `X-Correlation-ID` é propagado em toda a rede. Ao olhar o terminal do Docker, você verá logs unificados no formato:

`[14:30:05 INF] [CorrID: 550e8400-e29b-41d4-a716-446655440000] Iniciando transação via IA externa.`

Isso prepara a aplicação nativamente para ferramentas de rastreamento distribuído como OpenTelemetry, Datadog ou ELK Stack.

---

## 🧪 Qualidade: Bateria de Testes

O projeto conta com uma suíte robusta de testes focada nas regras vitais do negócio. Para rodar, use `dotnet test`.

* **Testes de Unidade:** * Garantia de higienização de Markdown da resposta da IA usando Regex testadas via `[Theory]`.
* Validação de resiliência HTTP utilizando `Mock<HttpMessageHandler>` (isolamento completo de rede).


* **Testes de Integração:** * Validação End-to-End. Utilização de `WebApplicationFactory` para levantar as duas APIs em memória e testar a ponte de comunicação e a propagação de *Tracing Headers* entre o Monolito e o Microsserviço sem a necessidade de expor portas físicas na máquina.
