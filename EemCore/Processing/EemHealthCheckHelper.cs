using EemCore.Models;
using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.MCP;
using System.ComponentModel;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace EemCore.Processing
{
    /// <summary>
    /// Helper para padronizar respostas de health checks no sistema ??m
    /// </summary>
    public static class EemHealthCheckHelper
    {
        /// <summary>
        /// Gera uma resposta JSON padronizada para health checks
        /// </summary>
        public static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";
            
            var response = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                timestamp = DateTime.UtcNow,
                version = typeof(EemHealthCheckHelper).Assembly.GetName().Version?.ToString(),
                checks = report.Entries.Select(e => new 
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration.TotalMilliseconds,
                    description = e.Value.Description,
                    data = e.Value.Data.ToDictionary(
                        d => d.Key,
                        d => SerializeHealthData(d.Value))
                })
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            return context.Response.WriteAsync(
                JsonSerializer.Serialize(response, options));
        }
        
        /// <summary>
        /// Serializa propriedades de health data para objetos que podem ser representados em JSON
        /// </summary>
        private static object SerializeHealthData(object value)
        {
            if (value is string[] strArray)
            {
                return strArray;
            }
            
            if (value is Exception ex)
            {
                return new
                {
                    message = ex.Message,
                    type = ex.GetType().Name,
                    stackTrace = ex.Stack