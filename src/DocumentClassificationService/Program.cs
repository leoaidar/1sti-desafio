using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Polly;
using Polly.Extensions.Http;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Services;
using DocumentClassificationService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registra o Mock para ser injetado no Fallback
builder.Services.AddTransient<MockDocumentClassifier>();

// 1. Resiliência de Infraestrutura (Apenas Retry)

var retryCountStr = builder.Configuration["CerebrasAi:RetryCount"];
int retryCount = int.TryParse(retryCountStr, out var count) ? count : 2;

builder.Services.AddHttpClient<IDocumentClassifier, CerebrasAiClassifier>()
    .AddTransientHttpErrorPolicy(policyBuilder =>
          policyBuilder.WaitAndRetryAsync(retryCount, _ => TimeSpan.FromSeconds(1)));

var app = builder.Build();

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
    [FromServices] MockDocumentClassifier fallbackMock) =>
{
  try
  {
    // Tenta processar pela IA (que já possui os Retries do Polly embutidos)
    var result = await classifier.ClassifyAsync(request);
    return Results.Ok(result);
  }
  catch (Exception ex)
  {
    // Se a IA estiver fora do ar ou o modelo não existir (Erro 404, 500, etc)
    // Aciona o Fallback Oficial validado pelos testes unitários!
    Console.WriteLine($"[AVISO] Falha na IA ({ex.Message}). Roteando para MockDocumentClassifier...");
    var fallbackResult = await fallbackMock.ClassifyAsync(request);
    return Results.Ok(fallbackResult);
  }
});

app.Run();