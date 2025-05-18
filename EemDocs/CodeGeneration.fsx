(*** hide ***)
#r "nuget: FSharp.Data"
#r "nuget: Deedle"
#r "nuget: XPlot.Plotly"
#r "../EemCore/bin/Debug/net8.0/EemCore.dll"

open System
open System.IO
open System.Text
open FSharp.Data
open Deedle
open XPlot.Plotly

(*** show ***)
(**
# Geração de Código C# a partir da Documentação F#

## Visão Geral

Este documento demonstra como a documentação executável em F# pode ser usada para gerar
código C# para a implementação do Εεm. Esta abordagem permite que você experimente com 
conceitos em F# e, em seguida, exporte o código correspondente em C# para a implementação 
de produção.

## Exemplo: Gerando um Agente MCP para Εεm

Abaixo, demonstramos como definir um agente em F# e gerar o código C# correspondente.
*)

/// <summary>
/// Módulo para definição de agentes do Εεm
/// </summary>
module EemAgentDesign =
    /// Definição de uma operação de agente
    type AgentOperation = {
        Name: string
        Description: string
        Parameters: (string * string * string) list // (nome, tipo, descrição)
        ReturnType: string
        ReturnDescription: string
        ImplementationTemplate: string
    }
    
    /// Definição de um agente Εεm
    type EemAgent = {
        Name: string
        Namespace: string
        Description: string
        Operations: AgentOperation list
        Dependencies: string list
    }
    
    /// Gera código C# para um agente
    let generateCSharpAgent (agent: EemAgent) =
        let sb = StringBuilder()
        
        // Adicionar cabeçalho e using statements
        sb.AppendLine("// Arquivo gerado automaticamente a partir da documentação executável F#")
            .AppendLine("// Εεm - Ευ-εnable-memory")
            .AppendLine()
            .AppendLine("using Microsoft.MCP;")
            .AppendLine("using Microsoft.SemanticKernel;")
            .AppendLine("using System.ComponentModel;") |> ignore
            
        // Adicionar dependências adicionais
        agent.Dependencies |> List.iter (fun dep -> 
            sb.AppendLine($"using {dep};") |> ignore
        )
        
        sb.AppendLine().AppendLine($"namespace {agent.Namespace}") 
            .AppendLine("{")
            .AppendLine($"    /// <summary>")
            .AppendLine($"    /// {agent.Description}")
            .AppendLine($"    /// </summary>")
            .AppendLine("    [McpToolType]")
            .AppendLine($"    public class {agent.Name}")
            .AppendLine("    {") |> ignore
            
        // Adicionar campo para logger
        sb.AppendLine($"        private readonly ILogger<{agent.Name}> _logger;")
            .AppendLine()
            .AppendLine($"        /// <summary>")
            .AppendLine($"        /// Construtor para {agent.Name}")
            .AppendLine($"        /// </summary>")
            .AppendLine($"        public {agent.Name}(ILogger<{agent.Name}> logger)")
            .AppendLine("        {")
            .AppendLine("            _logger = logger;")
            .AppendLine("        }") |> ignore
            
        // Adicionar operações
        for op in agent.Operations do
            sb.AppendLine()
                .AppendLine($"        /// <summary>")
                .AppendLine($"        /// {op.Description}")
                .AppendLine($"        /// </summary>")
                .AppendLine($"        /// <returns>{op.ReturnDescription}</returns>")
                .AppendLine($"        [McpTool(\"{op.Description}\")]")
                .Append($"        public {op.ReturnType} {op.Name}(") |> ignore
                
            // Adicionar parâmetros
            op.Parameters 
            |> List.mapi (fun i (name, typeName, desc) ->
                let paramText = $"[Description(\"{desc}\")] {typeName} {name}"
                if i < op.Parameters.Length - 1 then
                    paramText + ", "
                else
                    paramText
            )
            |> List.iter (fun paramText -> sb.Append(paramText) |> ignore)
                
            sb.AppendLine(")")
                .AppendLine("        {")
                .AppendLine($"            _logger.LogInformation($\"Executando {op.Name}\");")
                .AppendLine()
                .AppendLine("            // Implementação")
                .AppendLine(op.ImplementationTemplate)
                .AppendLine("        }") |> ignore
                
        // Fechar classe e namespace
        sb.AppendLine("    }")
            .AppendLine("}") |> ignore
            
        sb.ToString()

/// <summary>
/// Define e gera o código para um agente de histórico do Εεm
/// </summary>
let defineHistoryAgent() =
    let agent = {
        Name = "HistoryAgent"
        Namespace = "EemCore.Agents"
        Description = "Agente responsável por fornecer histórico de atividades e contextos passados para assistentes de IA"
        Dependencies = [
            "Microsoft.SemanticKernel.Memory"
            "System.Text.Json"
            "Azure.Storage.Blobs"
            "Microsoft.Azure.Cosmos"
            "System.Threading.Tasks"
        ]
        Operations = [
            {
                Name = "GetActivityHistoryAsync"
                Description = "Recupera o histórico de atividades com opções de filtragem"
                Parameters = [
                    "maxItems", "int", "Número máximo de itens a retornar"
                    "timeWindowHours", "int?", "Janela de tempo em horas (opcional)"
                    "activityType", "string?", "Tipo de atividade para filtrar (opcional)" 
                ]
                ReturnType = "Task<IList<ActivityHistoryItem>>"
                ReturnDescription = "Lista de itens de histórico de atividades"
                ImplementationTemplate = """
            var historyItems = new List<ActivityHistoryItem>();
            
            // Aplicar filtros de tempo se especificado
            DateTime? cutoffTime = null;
            if (timeWindowHours.HasValue)
            {
                cutoffTime = DateTime.UtcNow.AddHours(-timeWindowHours.Value);
            }
            
            // Recuperar atividades do armazenamento
            // Implementação real recuperaria do blob storage ou cosmos db
            
            // Filtrar por tipo se especificado
            if (!string.IsNullOrEmpty(activityType))
            {
                // Implementação de filtragem
            }
            
            // Limitar número de itens
            return historyItems.Take(maxItems).ToList();
                """
            }
            {
                Name = "GetContextHistoryAsync"
                Description = "Recupera contextos históricos para um determinado tópico ou entidade"
                Parameters = [
                    "topic", "string", "Tópico ou entidade para buscar contexto"
                    "relevanceThreshold", "float", "Limiar de relevância (0.0 a 1.0)"
                ]
                ReturnType = "Task<string>"
                ReturnDescription = "Resumo do contexto histórico para o tópico especificado"
                ImplementationTemplate = """
            // Usar memória semântica para recuperar contextos relevantes
            // Implementação real utilizaria embeddings e busca semântica
            
            // Exemplo simulado de resposta
            return Task.FromResult($"Contexto histórico para {topic} (relevância > {relevanceThreshold}):\\n" +
                "1. Atividade de codificação em 12/05/2025\\n" +
                "2. Revisão de documentação em 14/05/2025\\n" +
                "3. Discussão sobre design em 15/05/2025");
                """
            }
            {
                Name = "GenerateTimelineAsync"
                Description = "Gera uma linha do tempo de atividades relacionadas a um projeto ou tópico"
                Parameters = [
                    "projectName", "string", "Nome do projeto para gerar timeline"
                    "startDate", "DateTime?", "Data de início para a timeline (opcional)"
                    "endDate", "DateTime?", "Data de fim para a timeline (opcional)"
                ]
                ReturnType = "Task<ProjectTimeline>"
                ReturnDescription = "Linha do tempo do projeto com eventos organizados cronologicamente"
                ImplementationTemplate = """
            var timeline = new ProjectTimeline
            {
                ProjectName = projectName,
                StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
                EndDate = endDate ?? DateTime.UtcNow,
                Events = new List<TimelineEvent>()
            };
            
            // Implementação real buscaria eventos do projeto no armazenamento
            // e os organizaria em ordem cronológica
            
            // Exemplo simulado
            timeline.Events.Add(new TimelineEvent 
            { 
                Date = DateTime.UtcNow.AddDays(-20), 
                Type = "Commit", 
                Description = "Implementação inicial do projeto"
            });
            
            return Task.FromResult(timeline);
                """
            }
        ]
    }
    
    // Gerar código C#
    EemAgentDesign.generateCSharpAgent agent
    
/// <summary>
/// Função para salvar código gerado em arquivo
/// </summary>
let saveGeneratedCode (code: string) (filePath: string) =
    File.WriteAllText(filePath, code)
    printfn "Código gerado e salvo em: %s" filePath
    
// Gerar e exibir código para o agente de histórico
let historyAgentCode = defineHistoryAgent()
printfn "%s" historyAgentCode

// Exemplo de como salvar o código (comentado para evitar escrita de arquivo)
// saveGeneratedCode historyAgentCode "c:/Users/G/EEm/EemSolution/EemCore/Agents/HistoryAgent.cs"

(**
## Gerando Modelos de Dados a partir da Documentação

Além de gerar agentes, podemos definir e gerar modelos de dados para uso no Εεm.
Este exemplo mostra como definir modelos em F# e gerar classes C# correspondentes.
*)

/// <summary>
/// Módulo para definição e geração de modelos de dados
/// </summary>
module EemModelDesign =
    /// Definição de uma propriedade de modelo
    type ModelProperty = {
        Name: string
        Type: string
        Description: string
        IsNullable: bool
        DefaultValue: string option
    }
    
    /// Definição de um modelo de dados
    type DataModel = {
        Name: string
        Namespace: string
        Description: string
        Properties: ModelProperty list
        IsRecord: bool  // Se true, gera record; se false, gera classe
    }
    
    /// Gera código C# para um modelo de dados
    let generateCSharpModel (model: DataModel) =
        let sb = StringBuilder()
        
        // Adicionar cabeçalho
        sb.AppendLine("// Arquivo gerado automaticamente a partir da documentação executável F#")
            .AppendLine("// Εεm - Ευ-εnable-memory")
            .AppendLine()
            .AppendLine("using System;")
            .AppendLine("using System.Collections.Generic;")
            .AppendLine("using System.Text.Json.Serialization;")
            .AppendLine()
            .AppendLine($"namespace {model.Namespace}")
            .AppendLine("{")
            .AppendLine($"    /// <summary>")
            .AppendLine($"    /// {model.Description}")
            .AppendLine($"    /// </summary>") |> ignore
            
        if model.IsRecord then
            sb.AppendLine($"    public record {model.Name}")
        else
            sb.AppendLine($"    public class {model.Name}")
            
        sb.AppendLine("    {") |> ignore
        
        // Adicionar propriedades
        for prop in model.Properties do
            sb.AppendLine($"        /// <summary>")
                .AppendLine($"        /// {prop.Description}")
                .AppendLine($"        /// </summary>") |> ignore
                
            // Tipo da propriedade
            let typeName = 
                if prop.IsNullable && not (prop.Type.EndsWith("?")) && not (prop.Type.StartsWith("List<")) && not (prop.Type.StartsWith("Dictionary<")) then
                    prop.Type + "?"
                else
                    prop.Type
                    
            // Valor padrão, se aplicável
            match prop.DefaultValue with
            | Some value -> 
                sb.AppendLine($"        [JsonPropertyName(\"{prop.Name.ToLower()}\")]")
                    .AppendLine($"        public {typeName} {prop.Name} {{ get; set; }} = {value};") |> ignore
            | None ->
                sb.AppendLine($"        [JsonPropertyName(\"{prop.Name.ToLower()}\")]")
                    .AppendLine($"        public {typeName} {prop.Name} {{ get; set; }}{(if prop.Type = "string" && not prop.IsNullable then " = string.Empty;" else ";")}")
                    |> ignore
        
        // Fechar classe e namespace
        sb.AppendLine("    }")
            .AppendLine("}") |> ignore
            
        sb.ToString()

/// <summary>
/// Define e gera código para os modelos de dados do histórico de atividades
/// </summary>
let defineHistoryModels() =
    let activityHistoryItem = {
        Name = "ActivityHistoryItem"
        Namespace = "EemCore.Models"
        Description = "Representa um item no histórico de atividades do usuário"
        IsRecord = false
        Properties = [
            { Name = "Id"; Type = "string"; Description = "Identificador único da atividade"; IsNullable = false; DefaultValue = None }
            { Name = "Timestamp"; Type = "DateTime"; Description = "Data e hora da atividade"; IsNullable = false; DefaultValue = Some("DateTime.UtcNow") }
            { Name = "ActivityType"; Type = "string"; Description = "Tipo da atividade (Edição, Navegação, etc)"; IsNullable = false; DefaultValue = None }
            { Name = "Source"; Type = "string"; Description = "Fonte da atividade (VS, VS Code, etc)"; IsNullable = false; DefaultValue = None }
            { Name = "Description"; Type = "string"; Description = "Descrição textual da atividade"; IsNullable = false; DefaultValue = None }
            { Name = "Metadata"; Type = "Dictionary<string, string>"; Description = "Metadados adicionais da atividade"; IsNullable = false; DefaultValue = Some("new Dictionary<string, string>()") }
            { Name = "Relevance"; Type = "float"; Description = "Pontuação de relevância da atividade"; IsNullable = false; DefaultValue = Some("0.0f") }
        ]
    }
    
    let timelineEvent = {
        Name = "TimelineEvent"
        Namespace = "EemCore.Models"
        Description = "Representa um evento em uma linha do tempo de projeto"
        IsRecord = true
        Properties = [
            { Name = "Id"; Type = "string"; Description = "Identificador único do evento"; IsNullable = false; DefaultValue = Some("Guid.NewGuid().ToString()") }
            { Name = "Date"; Type = "DateTime"; Description = "Data e hora do evento"; IsNullable = false; DefaultValue = Some("DateTime.UtcNow") }
            { Name = "Type"; Type = "string"; Description = "Tipo do evento (Commit, Build, etc)"; IsNullable = false; DefaultValue = Some("string.Empty") }
            { Name = "Description"; Type = "string"; Description = "Descrição do evento"; IsNullable = false; DefaultValue = Some("string.Empty") }
            { Name = "RelatedItems"; Type = "List<string>"; Description = "Itens relacionados ao evento"; IsNullable = false; DefaultValue = Some("new List<string>()") }
        ]
    }
    
    let projectTimeline = {
        Name = "ProjectTimeline"
        Namespace = "EemCore.Models"
        Description = "Representa uma linha do tempo completa de um projeto"
        IsRecord = false
        Properties = [
            { Name = "ProjectName"; Type = "string"; Description = "Nome do projeto"; IsNullable = false; DefaultValue = None }
            { Name = "StartDate"; Type = "DateTime"; Description = "Data de início da linha do tempo"; IsNullable = false; DefaultValue = None }
            { Name = "EndDate"; Type = "DateTime"; Description = "Data de fim da linha do tempo"; IsNullable = false; DefaultValue = None }
            { Name = "Events"; Type = "List<TimelineEvent>"; Description = "Eventos na linha do tempo"; IsNullable = false; DefaultValue = Some("new List<TimelineEvent>()") }
            { Name = "TotalEvents"; Type = "int"; Description = "Número total de eventos"; IsNullable = false; DefaultValue = Some("0") }
        ]
    }
    
    // Gerar código C# para os modelos
    let activityHistoryItemCode = EemModelDesign.generateCSharpModel activityHistoryItem
    let timelineEventCode = EemModelDesign.generateCSharpModel timelineEvent
    let projectTimelineCode = EemModelDesign.generateCSharpModel projectTimeline
    
    (activityHistoryItemCode, timelineEventCode, projectTimelineCode)

// Gerar e exibir código para os modelos
let (activityHistoryItemCode, timelineEventCode, projectTimelineCode) = defineHistoryModels()
printfn "ActivityHistoryItem:\n%s\n" activityHistoryItemCode
printfn "TimelineEvent:\n%s\n" timelineEventCode
printfn "ProjectTimeline:\n%s\n" projectTimelineCode

(**
## Integração com o Servidor MCP

Para completar o exemplo, podemos gerar código de configuração para adicionar
o novo agente de histórico ao servidor MCP.
*)

/// <summary>
/// Gera código de configuração para o servidor MCP
/// </summary>
let generateMcpServerConfig() =
    let code = """
// Registrar os agentes do Εεm
builder.Services.AddTransient<CaptureAgent>();
builder.Services.AddTransient<ContextAgent>();
builder.Services.AddTransient<EulerianAgent>();
builder.Services.AddTransient<CorrelationAgent>();
builder.Services.AddTransient<HistoryAgent>();
builder.Services.AddTransient<GenAIScriptProcessor>();

// Configurar servidor MCP
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .AddMcpToolType<CaptureAgent>()
    .AddMcpToolType<ContextAgent>()
    .AddMcpToolType<EulerianAgent>()
    .AddMcpToolType<CorrelationAgent>()
    .AddMcpToolType<HistoryAgent>()
    .AddMcpToolType<GenAIScriptProcessor>();
"""
    code

// Exibir código de configuração MCP
printfn "Configuração do Servidor MCP:\n%s" (generateMcpServerConfig())

(**
## Conclusão

A geração de código C# a partir da documentação F# oferece um fluxo de trabalho poderoso 
para o desenvolvimento do Εεm:

1. **Prototipagem em F#**: Use F# para prototipagem rápida e experimentação interativa
2. **Documentação executável**: Crie documentação que também funciona como código de exemplo
3. **Geração de código C#**: Gere código C# para implementação de produção
4. **Integração completa**: Integre o código gerado com o resto do sistema

Esta abordagem proporciona o melhor dos dois mundos: a expressividade e concisão do F# para 
documentação e experimentação, e a ampla adoção e ecossistema do C# para a implementação 
final.
*)

/// <summary>
/// Função que resume os benefícios do fluxo de trabalho de geração de código
/// </summary>
let getCodeGenerationBenefits() =
    [
        "Documentação que gera código implementável"
        "Consistência entre documentação e implementação"
        "Experimentação rápida com feedback visual em F#"
        "Fácil adaptação para diferentes cenários de uso"
        "Redução de erros de implementação"
        "Maior produtividade do desenvolvedor"
    ]
    
// Exibir benefícios
printfn "Benefícios da Geração de Código a partir da Documentação F#:"
getCodeGenerationBenefits() |> List.iter (fun b -> printfn "- %s" b)
