using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Polly;
using Polly.Extensions.Http;
using DocumentClassificationService.Interfaces;
using DocumentClassificationService.Services;
using DocumentClassificationService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddTransient<MockDocumentClassifier>();

IAsyncPolicy<HttpResponseMessage> fallbackPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => !msg.IsSuccessStatusCode)
    .FallbackAsync(
        fallbackAction: (delegateResult, context, cancellationToken) =>
        {
            var fallbackResponse = new DocumentClassificationResponse
            {
                Classification = "FALLBACK-MOCK",
                Confidence = 0.5,
                ModelVersion = "v1-fallback",
                Reasons = new List<string> { "IA indisponível. Fallback local acionado." }
            };

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(fallbackResponse)
            };

            return Task.FromResult(responseMessage);
        },
        onFallbackAsync: (delegateResult, context) =>
        {
            Console.WriteLine("IA falhou. Fallback acionado.");
            return Task.CompletedTask;
        });

builder.Services.AddHttpClient<IDocumentClassifier, CerebrasAiClassifier>()
    .AddPolicyHandler(fallbackPolicy)
    .AddTransientHttpErrorPolicy(policyBuilder => 
        policyBuilder.WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(1)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

// desabilitado local warning
//app.UseHttpsRedirection();

app.MapPost("/api/v1/classify", async ([FromBody] DocumentClassificationRequest request, [FromServices] IDocumentClassifier classifier) =>
{
    var result = await classifier.ClassifyAsync(request);
    return Results.Ok(result);
});

app.Run();
