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
        /// Executa a verifica��o de sa�de dos agentes ??m
        /// </summary>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verificando sa�de dos agentes ??m");
            
            var healthStatus = HealthStatus.Healthy;
            var data = new Dictionary<string, object>();
            
            try
            {
                // Verificar captura de atividades
                var captureStatus = await CheckCaptureAgentAsync(cancellationToken);
                data.Add("CaptureAgent", captureStatus);
                if (captureStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                
                // Verificar gera��o de contexto
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
                
                // Verificar an�lise de correla��o
                if (_options.EnableCorrelationAnalysis)
                {
                    var correlationStatus = await CheckCorrelationAgentAsync(cancellationToken);
                    data.Add("CorrelationAgent", correlationStatus);
                    if (correlationStatus.Status != "Healthy") healthStatus = HealthStatus.Degraded;
                }
                
                if (healthStatus == HealthStatus.Healthy)
                {
                    return HealthCheckResult.Healthy("Todos os agentes ??m est�o funcionando corretamente", data);
                }
                else
                {
                    return HealthCheckResult.Degraded("Um ou mais agentes ??m apresentam problemas", null, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar sa�de dos agentes ??m");
                return HealthCheckResult.Unhealthy("Erro ao verificar sa�de dos agentes ??m", ex, data);
            }
        }
        
        private async Task<AgentHealthStatus> CheckCaptureAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Testar funcionalidades b�sicas do agente de captura
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
                    return new AgentHealthStatus { Status = "Degraded", Message = "N�o foi poss�vel recuperar a atividade de teste" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar sa�de do agente de captura");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
        
        private async Task<AgentHealthStatus> CheckContextAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Testar gera��o de contexto simples
                string testSessionId = $"health-check-{Guid.NewGuid()}";
                string context = await _contextAgent.GenerateContextAsync(testSessionId, null, 5);
                
                if (!string.IsNullOrEmpty(context))
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "Gera��o de contexto funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "Contexto gerado est� vazio" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar sa�de do agente de contexto");
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
                    return new AgentHealthStatus { Status = "Degraded", Message = "Processamento euleriano est� inativo" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar sa�de do agente euleriano");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
        
        private async Task<AgentHealthStatus> CheckCorrelationAgentAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Verificar status do agente de correla��o
                var status = await _correlationAgent.GetStatusAsync();
                
                if (status.IsActive)
                {
                    return new AgentHealthStatus { Status = "Healthy", Message = "An�lise de correla��o funcionando normalmente" };
                }
                else
                {
                    return new AgentHealthStatus { Status = "Degraded", Message = "An�lise de correla��o est� inativa" };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar sa�de do agente de correla��o");
                return new AgentHealthStatus { Status = "Unhealthy", Message = $"Erro: {ex.Message}" };
            }
        }
    }
    
    /// <summary>
    /// Representa o status de sa�de de um agente
    /// </summary>
    public class AgentHealthStatus
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}