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

builder.Services.AddTransient<MockDocumentClassifier>();

builder.Services.AddHttpClient<IDocumentClassifier, CerebrasAiClassifier>()
    .AddTransientHttpErrorPolicy(policyBuilder => 
        policyBuilder.WaitAndRetryAsync(2, _ => TimeSpan.FromSeconds(1)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.MapPost("/api/v1/classify", async ([FromBody] DocumentClassificationRequest request, [FromServices] IDocumentClassifier classifier) =>
{
    var resultado = await classifier.ClassifyAsync(request);
    return Results.Ok(resultado);
});

app.Run();
