using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace EemCore.Configuration
{
    /// <summary>
    /// Op��es de configura��o principal para o sistema ??m
    /// </summary>
    public class EemOptions
    {
        /// <summary>
        /// Intervalo de tempo em minutos para a captura autom�tica de atividades
        /// </summary>
        public int CaptureInterval { get; set; } = 15;
        
        /// <summary>
        /// Per�odo de reten��o de dados em dias
        /// </summary>
        public int RetentionPeriodDays { get; set; } = 90;
        
        /// <summary>
        /// Define se o processamento euleriano est� habilitado
        /// </summary>
        public bool EnableEulerianProcessing { get; set; } = true;
        
        /// <summary>
        /// Define se a an�lise de correla��o est� habilitada
        /// </summary>
        public bool EnableCorrelationAnalysis { get; set; } = true;
        
        /// <summary>
        /// N�mero m�ximo de eventos por atividade
        /// </summary>
        public int MaxEventsPerActivity { get; set; } = 1000;
        
        /// <summary>
        /// N�mero m�ximo de atividades por sess�o
        /// </summary>
        public int MaxActivitiesPerSession { get; set; } = 100;
        
        /// <summary>
        /// N�vel de enriquecimento de contexto (1-5)
        /// </summary>
        public int EnrichmentLevel { get; set; } = 3;
        
        /// <summary>
        /// Configura��es de privacidade
        /// </summary>
        public PrivacySettings PrivacySettings { get; set; } = new();
    }
    
    /// <summary>
    /// Configura��es de privacidade para filtragem de dados
    /// </summary>
    public class PrivacySettings
    {
        /// <summary>
        /// Indica se dados pessoais devem ser exclu�dos
        /// </summary>
        public bool ExcludePersonalData { get; set; } = true;
        
        /// <summary>
        /// Indica se segredos devem ser exclu�dos
        /// </summary>
        public bool ExcludeSecrets { get; set; } = true;
        
        /// <summary>
        /// Tipos de arquivo a serem considerados
        /// </summary>
        public string[] FilterFileTypes { get; set; } = new[] 
        { 
            ".cs", ".fs", ".md", ".json", ".xml", ".csproj", ".fsproj" 
        };
    }
    
    /// <summary>
    /// Op��es de configura��o para servi�os Azure
    /// </summary>
    public class AzureOptions
    {
        /// <summary>
        /// String de conex�o do Azure Storage
        /// </summary>
        public string StorageConnectionString { get; set; } = string.Empty;
        
        /// <summary>
        /// Endpoint do Cosmos DB
        /// </summary>
        public string CosmosEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Chave do Cosmos DB
        /// </summary>
        public string CosmosKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do banco de dados Cosmos
        /// </summary>
        public string CosmosDatabase { get; set; } = "EemDatabase";
        
        /// <summary>
        /// Endpoint do Azure OpenAI
        /// </summary>
        public string OpenAIEndpoint { get; set; } = string.Empty;
        
        /// <summary>
        /// Chave do Azure OpenAI
        /// </summary>
        public string OpenAIKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Nome do deployment do modelo de chat
        /// </summary>
        public string OpenAIDeployment { get; set; } = "gpt-4";
        
        /// <summary>
        /// Nome do modelo de embeddings
        /// </summary>
        public string OpenAIEmbeddingModel { get; set; } = "text-embedding-ada-002";
    }
    
    /// <summary>
    /// Op��es de configura��o para o servidor MCP
    /// </summary>
    public class McpOptions
    {
        /// <summary>
        /// Porta para o servidor MCP
        /// </summary>
        public int Port { get; set; } = 5100;
        
        /// <summary>
        /// Indica se a autentica��o est� habilitada
        /// </summary>
        public bool AuthEnabled { get; set; } = true;
        
        /// <summary>
        /// Chave de autentica��o para MCP
        /// </summary>
        public string AuthKey { get; set; } = string.Empty;
        
        /// <summary>
        /// N�mero m�ximo de requisi��es concorrentes
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 100;
        
        /// <summary>
        /// Timeout padr�o em segundos
        /// </summary>
        public int DefaultTimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Tamanho m�ximo de contexto em tokens
        /// </summary>
        public int MaxContextSize { get; set; } = 16384;
    }
    
    /// <summary>
    /// Inicializador de telemetria para Application Insights
    /// </summary>
    public class EemTelemetryInitializer : ITelemetryInitializer
    {
        private readonly EemOptions _options;
        
        public EemTelemetryInitializer(IOptions<EemOptions> options)
        {
            _options = options.Value;
        }
        
        public void Initialize(ITelemetry telemetry)
        {
            telemetry.Context.Cloud.RoleName = "EemMcpServer";
            telemetry.Context.Component.Version = GetType().Assembly.GetName().Version?.ToString();
            
            // Adicionar propriedades personalizadas
            telemetry.Context.GlobalProperties["CaptureInterval"] = _options.CaptureInterval.ToString();
            telemetry.Context.GlobalProperties["EulerianProcessingEnabled"] = _options.EnableEulerianProcessing.ToString();
            telemetry.Context.GlobalProperties["CorrelationAnalysisEnabled"] = _options.EnableCorrelationAnalysis.ToString();
        }
    }
}