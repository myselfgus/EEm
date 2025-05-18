using EemCore.Agents;
using EemCore.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EemCore.Services
{
    /// <summary>
    /// Health check para verificar o status dos agentes do ??m
    /// </summary>
    public class EemAgentsHealthCheck : IHealthCheck
    {
        private readonly CaptureAgent _captureAgent;
        private readonly ContextAgent _contextAgent;
        private readonly EulerianAgent _eulerianAgent;
        private readonly CorrelationAgent _correlationAgent;
        private readonly ILogger<EemAgentsHealthCheck> _logger;
        private readonly EemOptions _options;
        
        public EemAgentsHealthCheck(
            CaptureAgent captureAgent,
            ContextAgent contextAgent,
            EulerianAgent eulerianAgent,
            CorrelationAgent correlationAgent,
            IOptions<EemOptions> options,
            ILogger<EemAgentsHealthCheck> logger)
        {
            _captureAgent = captureAgent;
            _contextAgent = contextAgent;
            _eulerianAgent = eulerianAgent;
            _correlationAgent = correlationAgent;
            _options = options.Value;
            _logger = logger;
        }
        
        /// <summary>
        /// Executa a verificação de saúde dos agentes ??m
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verificando saúde dos agentes ??m");
            
            var healthStatus = HealthStatus.Healthy;
            var data = new Dictionary<string, object>();
            
            try
            {
                // Verificar captura de atividades
                var captureStatus = await CheckCaptureAgentAsync(cancellationToken);
                data.Add("CaptureAgent", captureStatus);
                if (captureStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                
                // Verificar geração de contexto
                var contextStatus = await CheckContextAgentAsync(cancellationToken);
                data.Add("ContextAgent", contextStatus);
                if (contextStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                
                // Verificar processamento euleriano
                if (_options.EnableEulerianProcessing)
                {
                    var eulerianStatus = await CheckEulerianAgentAsync(cancellationToken);
                    data.Add("EulerianAgent", eulerianStatus);
                    if (eulerianStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                }
                
                // Verificar análise de correlação
                if (_options.EnableCorrelationAnalysis)
                {
                    var correlationStatus = await CheckCorrelationAgentAsync(cancellationToken);
                    data.Add("CorrelationAgent", correlationStatus);
                    if (correlationStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                }
                
                if (healthStatus == HealthStatus.Healthy)
                {
                    return HealthCheckResult.Healthy("Todos os agentes ??m estão funcionando corretamente", data);
                }
                else
                {
                    return HealthCheckResult.Degraded("Um ou mais agentes ??m apresentam problemas", null, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde dos agentes ??m");
                return HealthCheckResult.Unhealthy("Erro ao verificar saúde dos agentes ??m", ex, data);
            }
        }
        
        private async Task<AgentHealthStatus> CheckCaptureAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Testar funcionalidades básicas do agente de captura
                string testSessionId = $"health-check-{Guid.NewGuid()}";
                await _captureAgent.CaptureActivityAsync(
                    "health_check",
                    "Health check test content",
                    "HealthCheck",
                    testSessionId);
                
                var activities = await _captureAgent.GetRecentActivitiesAsync(testSessionId, 1);
                if (activities.Any())
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "Captura funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "Não foi possível recuperar a atividade de teste" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde do agente de captura");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
        
        private async Task<AgentHealthStatus> CheckContextAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Testar geração de contexto simples
                string testSessionId = $"health-check-{Guid.NewGuid()}";
                string context = await _contextAgent.GenerateContextAsync(testSessionId, null, 5);
                
                if (!string.IsNullOrEmpty(context))
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "Geração de contexto funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "Contexto gerado está vazio" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde do agente de contexto");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
        
        private async Task<AgentHealthStatus> CheckEulerianAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Verificar status do agente euleriano
                var status = await _eulerianAgent.GetStatusAsync();
                
                if (status.IsActive)
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "Processamento euleriano funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "Processamento euleriano está inativo" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde do agente euleriano");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
        
        private async Task<AgentHealthStatus> CheckCorrelationAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Verificar status do agente de correlação
                var status = await _correlationAgent.GetStatusAsync();
                
                if (status.IsActive)
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "Análise de correlação funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "Análise de correlação está inativa" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar saúde do agente de correlação");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
    }
    
    /// <summary>
    /// Representa o status de saúde de um agente
    /// </summary>
    public class AgentHealthStatus
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}