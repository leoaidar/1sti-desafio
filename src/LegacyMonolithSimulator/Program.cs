using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LegacyMonolithSimulator.Services;
using LegacyMonolithSimulator.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<ILegacyClassificationProxy, ClassificationProxy>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/legacy/process-document", async ([FromBody] DocumentClassificationRequest request, [FromServices] ILegacyClassificationProxy proxy) =>
{
    var result = await proxy.ClassifyAsync(request);
    return Results.Ok(result);
});

app.Run();