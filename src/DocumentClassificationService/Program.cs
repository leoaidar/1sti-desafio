using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Services;
using DocumentClassificationService.Models;
using DocumentClassificationService.Prompts;

// 1. Inicializa o Serilog lendo do appsettings.json antes de subir o app
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
  Log.Information("Iniciando o DocumentClassificationService...");

  var builder = WebApplication.CreateBuilder(args);

  // 2. Substitui o logger padrão pelo Serilog
  builder.Host.UseSerilog();

  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();

  // Registra o Mock para ser injetado no Fallback
  builder.Services.AddTransient<MockDocumentClassifier>();

  // Registra o PromptBuilder padrão para classificação de documentos.
  builder.Services.AddSingleton<IPromptBuilder, DocumentClassificationPromptBuilder>();

  // 1. Resiliência de Infraestrutura (Retry + Circuit Breaker)
  var retryCountStr = builder.Configuration["CerebrasAi:RetryCount"];
  int retryCount = int.TryParse(retryCountStr, out var count) ? count : 2;
  
  var cbDurationStr = builder.Configuration["CerebrasAi:CircuitBreakerDurationSeconds"];
  int cbDurationSeconds = int.TryParse(cbDurationStr, out var dur) ? dur : 30;

  builder.Services.AddHttpClient<IDocumentClassifier, CerebrasAiClassifier>()
      // Política 1 (Externa): Retry. Tenta novamente em caso de QUALQUER falha HTTP (incluindo 401, 404, 500).
      .AddPolicyHandler(Policy<System.Net.Http.HttpResponseMessage>
            .Handle<System.Net.Http.HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(retryCount, _ => TimeSpan.FromSeconds(1)))
      // Política 2 (Interna): Circuit Breaker. Se falhar algumas vezes seguidas, abre o circuito pelo tempo configurado.
      .AddPolicyHandler(Policy<System.Net.Http.HttpResponseMessage>
            .Handle<System.Net.Http.HttpRequestException>()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                // Se um request faz (retryCount + 1) tentativas, abrimos o circuito após 2 ou 3 "requests completos" falharem.
                handledEventsAllowedBeforeBreaking: (retryCount + 1) * 2,
                durationOfBreak: TimeSpan.FromSeconds(cbDurationSeconds),
                onBreak: (outcome, timespan) =>
                {
                    Log.Warning("[Circuit Breaker] Circuito ABERTO por {TotalSeconds} segundos. A API da IA está instável.", timespan.TotalSeconds);
                },
                onReset: () =>
                {
                    Log.Information("[Circuit Breaker] Circuito FECHADO. Comunicação com a IA restabelecida.");
                },
                onHalfOpen: () =>
                {
                    Log.Information("[Circuit Breaker] Circuito MEIO ABERTO. Testando se a IA voltou a responder...");
                }
            ));

  var app = builder.Build();

  app.Use(async (context, next) =>
  {
      if (!context.Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId))
      {
          correlationId = Guid.NewGuid().ToString();
      }

      context.Response.Headers["X-Correlation-ID"] = correlationId;

      using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
      {
          await next();
      }
  });

  // 3. Adiciona o log automático de requisições HTTP do Serilog
  app.UseSerilogRequestLogging();

  if (app.Environment.IsDevelopment())
  {
    app.UseSwagger();
    app.UseSwaggerUI();
  }

  // app.UseHttpsRedirection();

  // 2. Fallback de Negócio (Camada de Aplicação)
  app.MapPost("/api/v1/classify", async (
      [FromBody] DocumentClassificationRequest request,
      [FromServices] IDocumentClassifier classifier,
      [FromServices] MockDocumentClassifier fallbackMock,
      [FromServices] ILogger<Program> logger) => 
  {
    try
    {
      // Tenta processar pela IA (que já possui os Retries do Polly embutidos)
      var result = await classifier.ClassifyAsync(request);

      logger.LogInformation("Documento {DocumentId} classificado com sucesso pela IA como {Classification}", request.DocumentId, result.Classification);

      return Results.Ok(result);
    }
    catch (Exception ex)
    {
      // Se a IA estiver fora do ar ou o modelo não existir (Erro 404, 500, etc)
      logger.LogError(ex, "Falha na IA ao processar documento {DocumentId}. Roteando para MockDocumentClassifier...", request.DocumentId);

      var fallbackResult = await fallbackMock.ClassifyAsync(request);

      logger.LogWarning("Fallback local acionado e concluído para documento {DocumentId}. Resultado: {Classification}", request.DocumentId, fallbackResult.Classification);

      return Results.Ok(fallbackResult);
    }
  });

  app.Run();
}
catch (Exception ex)

{
  Log.Fatal(ex, "Falha catastrófica ao iniciar a aplicação.");
}
finally
{
  Log.CloseAndFlush();
}

namespace DocumentClassificationService
{
    public partial class Program { }
}