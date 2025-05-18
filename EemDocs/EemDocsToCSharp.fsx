(*** hide ***)
#r "nuget: FSharp.Data"
#r "nuget: Deedle"
#r "nuget: XPlot.Plotly"
#r "../EemCore/bin/Debug/net8.0/EemCore.dll"

open System
open System.IO
open FSharp.Data
open Deedle
open XPlot.Plotly

(*** show ***)
(**
# Integração da Documentação Executável F# com Código C#

## Visão Geral

Esta documentação executável demonstra como a documentação em F# e o código de implementação 
em C# são integrados no sistema Εεm. Isto oferece uma abordagem mista única:

1. **Documentação literate F#** - Conceitos, arquitetura e experimentação interativa
2. **Implementação C# robusta** - Código de produção com integração completa ao ecossistema .NET

A abordagem mista aproveita o melhor dos dois mundos: a expressividade e capacidade de 
documentação executável do F# com a ampla adoção e ecossistema do C#.

## Mapeamento Conceitual

Cada componente documentado em F# possui um equivalente em C# na implementação concreta.
O diagrama a seguir ilustra este mapeamento:
*)

/// <summary>
/// Módulo que demonstra a correspondência entre documentação F# e código C#
/// </summary>
module DocToCSharpMapping =
    /// Representa um mapeamento entre conceito F# e implementação C#
    type ComponentMapping = {
        DocName: string
        DocType: string
        ImplName: string
        ImplType: string
        FilePath: string
    }
    
    /// Lista de mapeamentos entre documentação e implementação
    let mappings = [
        { DocName = "EemSystemOverview"; DocType = "F# Type"; ImplName = "N/A"; ImplType = "N/A"; FilePath = "Introduction.fsx" }
        { DocName = "ActivityCapture"; DocType = "F# Module"; ImplName = "CaptureAgent"; ImplType = "C# Class"; FilePath = "EemCore/Agents/CaptureAgent.cs" }
        { DocName = "ContextProvider"; DocType = "F# Module"; ImplName = "ContextAgent"; ImplType = "C# Class"; FilePath = "EemCore/Agents/ContextAgent.cs" }
        { DocName = "RelationProcessor"; DocType = "F# Module"; ImplName = "CorrelationAgent"; ImplType = "C# Class"; FilePath = "EemCore/Agents/CorrelationAgent.cs" }
        { DocName = "EulerianFlow"; DocType = "F# Module"; ImplName = "EulerianAgent"; ImplType = "C# Class"; FilePath = "EemCore/Agents/EulerianAgent.cs" }
        { DocName = "GenAIScriptExamples"; DocType = "F# Module"; ImplName = "GenAIScriptProcessor"; ImplType = "C# Class"; FilePath = "EemCore/Processing/GenAIScriptProcessor.cs" }
        { DocName = "VSExtensionDemo"; DocType = "F# Module"; ImplName = "VSExtension"; ImplType = "C# Project"; FilePath = "EemVsExtension/VSCommandPackage.cs" }
        { DocName = "MCPIntegration"; DocType = "F# Module"; ImplName = "MCPServer"; ImplType = "C# Class"; FilePath = "EemServer/MCPServer.cs" }
    ]
    
    /// Exibe o mapeamento entre documentação e implementação
    let displayMappings() =
        printfn "| Documentação F# | Tipo F# | Implementação C# | Tipo C# | Arquivo |"
        printfn "|----------------|---------|-----------------|---------|---------|"
        
        mappings |> List.iter (fun m ->
            printfn "| %s | %s | %s | %s | %s |" m.DocName m.DocType m.ImplName m.ImplType m.FilePath
        )

// Exibir mapeamento
DocToCSharpMapping.displayMappings()

(**
## Extração de Exemplos para Testes

Um dos benefícios da documentação executável em F# é a capacidade de extrair
exemplos para usar como casos de teste em unidades de teste C#.

O exemplo a seguir demonstra como converter exemplos F# em casos de teste C#:
*)

/// <summary>
/// Módulo que demonstra como extrair exemplos F# para testes C#
/// </summary>
module ExampleExtraction =
    /// Exemplo F# de um fluxo euleriano
    let eulerianFlowExample = """
    let flow = EulerianFlowProcessor()
        .AddSource("vs_events", vsEvents)
        .AddSource("git_events", gitEvents)
        .AddSink("activities")
        .AddSink("correlations")
        .Process(fun data -> 
            // Processamento
            data |> List.map enrichData
        )
    """
    
    /// Converte o exemplo F# em teste C#
    let convertToCSTest (fsExample: string) =
        // Em uma implementação real, isso seria mais sofisticado
        let csTest = """
        [Fact]
        public void TestEulerianFlow()
        {
            // Arrange
            var vsEvents = new List<ActivityData>
            {
                new ActivityData { Type = "FileEdit", File = "Program.cs", Time = DateTime.Now },
                new ActivityData { Type = "Build", Result = "Success", Time = DateTime.Now.AddMinutes(-5) },
                new ActivityData { Type = "Debug", Time = DateTime.Now.AddMinutes(-10) },
            };
            
            var gitEvents = new List<ActivityData>
            {
                new ActivityData { Type = "Commit", Message = "Fix bug in login", Time = DateTime.Now.AddHours(-1) },
                new ActivityData { Type = "Push", Branch = "main", Time = DateTime.Now.AddHours(-0.5) },
            };
            
            var processor = new EulerianAgent();
            
            // Act
            var result = processor
                .AddSource("vs_events", vsEvents)
                .AddSource("git_events", gitEvents)
                .AddSink("activities")
                .AddSink("correlations")
                .Process(data => data.Select(d => EnrichData(d)).ToList());
                
            // Assert
            Assert.NotNull(result.GetResults("activities"));
            Assert.True(result.GetResults("activities").Count > 0);
        }
        """
        
        csTest
        
    /// Exibe o exemplo F# e o teste C# gerado
    let displayConversion() =
        printfn "Exemplo F# original:"
        printfn "```fsharp"
        printfn "%s" eulerianFlowExample
        printfn "```\n"
        
        printfn "Teste C# gerado:"
        printfn "```csharp"
        printfn "%s" (convertToCSTest eulerianFlowExample)
        printfn "```"

// Demonstrar conversão
ExampleExtraction.displayConversion()

(**
## Geração de Documentação HTML/PDF

A documentação executável F# pode ser facilmente convertida para HTML ou PDF,
o que possibilita gerar documentação completa do sistema que inclui tanto os
conceitos quanto exemplos executáveis.

O exemplo a seguir demonstra como converter esta documentação F# para HTML:
*)

/// <summary>
/// Demonstra como a documentação F# pode ser convertida para HTML/PDF
/// </summary>
module DocumentationGeneration =
    /// Simula a conversão de F# para HTML
    let convertToHtml (fsxFile: string) =
        printfn "Convertendo %s para HTML..." fsxFile
        
        // Em uma implementação real, seria usado o FSharp.Formatting
        // Aqui apenas simulamos o processo
        printfn "  Analisando código F#..."
        printfn "  Extraindo comentários markdown..."
        printfn "  Gerando código HTML com syntax highlighting..."
        printfn "  Incluindo gráficos e visualizações..."
        printfn "  Compilando arquivo HTML final..."
        
        let htmlFileName = Path.ChangeExtension(fsxFile, ".html")
        printfn "Documentação HTML gerada em %s\n" htmlFileName
        
    /// Simula a geração de um site completo de documentação
    let generateDocSite (fsxFiles: string list) =
        printfn "Gerando site de documentação para o Εεm..."
        
        printfn "1. Preparando estrutura de diretórios..."
        printfn "2. Processando %d arquivos de documentação F#..." fsxFiles.Length
        fsxFiles |> List.iter convertToHtml
        
        printfn "3. Gerando índice e navegação..."
        printfn "4. Compilando site estático final..."
        printfn "5. Site de documentação gerado com sucesso em /docs"
        
    // Lista de arquivos de documentação F#
    let docFiles = [
        "Introduction.fsx"
        "Architecture.fsx"
        "Components.fsx"
        "AzureIntegration.fsx"
        "MCP.fsx"
        "GenAIScriptAndDSL.fsx"
        "VisualStudioIntegration.fsx"
        "Installation.fsx"
        "EemDocsToCSharp.fsx"
    ]
    
    // Demonstrar geração de documentação
    let demonstrateDocGeneration() =
        generateDocSite docFiles

// Demonstrar geração de documentação
DocumentationGeneration.demonstrateDocGeneration()

(**
## Benefícios da Abordagem Mista F#/C#

A abordagem mista de documentação F# executável com implementação C# oferece
diversos benefícios:

1. **Documentação Viva** - A documentação F# é executável e sempre atualizada
2. **Facilidade de Experimentação** - F# REPL permite experimentar conceitos rapidamente
3. **Implementação Robusta** - C# oferece amplo suporte a ecossistemas empresariais
4. **Visualização de Dados** - F# excel em processamento de dados e visualização
5. **Aderência ao Ecossistema .NET** - Total compatibilidade com ferramentas e frameworks

Esta abordagem é particularmente valiosa para o Εεm, onde conceitos complexos
como processamento euleriano e correlação contextual se beneficiam da expressividade
do F#, enquanto a implementação se beneficia do vasto ecossistema C#.
*)

/// <summary>
/// Demonstração de como a abordagem mista F#/C# funciona na prática
/// </summary>
let demonstrateMixedApproach() =
    // Exemplo fictício de um fluxo euleriano
    let eulerianFlow data =
        data
        |> Seq.groupBy (fun d -> d?category)
        |> Seq.map (fun (category, items) -> 
            let processed = items |> Seq.map (fun i -> i?value) |> Seq.average
            (category, processed)
        )
        |> Map.ofSeq
    
    // Dados fictícios
    let testData = [
        {| category = "code"; value = 0.92 |}
        {| category = "docs"; value = 0.85 |}
        {| category = "code"; value = 0.78 |}
        {| category = "test"; value = 0.65 |}
        {| category = "docs"; value = 0.81 |}
    ]
    
    // Processar dados com o fluxo euleriano
    let results = eulerianFlow testData
    
    // Visualizar resultados
    printfn "Resultados do Processamento Euleriano:"
    results |> Map.iter (fun k v -> printfn "  - %s: %.2f" k v)
    
    // Visualizar como gráfico
    let categories = results |> Map.toList |> List.map fst
    let values = results |> Map.toList |> List.map snd
    
    let trace = 
        Bar(
            x = categories,
            y = values,
            marker = Marker(color = "#1E90FF")
        )
        
    let layout = 
        Layout(
            title = "Processamento Euleriano por Categoria",
            xaxis = Xaxis(title = "Categoria"),
            yaxis = Yaxis(title = "Valor Médio")
        )
        
    [trace]
    |> Chart.Plot
    |> Chart.WithLayout layout
    |> Chart.WithWidth 600
    |> Chart.WithHeight 400

// Demonstrar abordagem mista
demonstrateMixedApproach()

(**
## Conclusão

A integração entre documentação executável F# e código C# no Εεm demonstra
como uma abordagem mista pode oferecer o melhor dos dois mundos:

- **F# para Documentação e Experimentação** - Expressividade, concisão e interatividade
- **C# para Implementação de Produção** - Amplo ecossistema e adoção corporativa

Esta abordagem não apenas melhora a qualidade da documentação, mas também
facilita a experimentação, prototipagem e testabilidade do sistema.

Para desenvolvedores que desejam entender e contribuir com o Εεm, esta abordagem
oferece múltiplas formas de interagir com o sistema, desde a experimentação
conceitual até a implementação de novas funcionalidades.
*)

/// <summary>
/// Retorna os principais pontos sobre a abordagem mista
/// </summary>
let getMixedApproachKeyPoints() =
    [
        "Documentação executável em F# que é interativa e sempre atualizada"
        "Código de produção em C# com total integração ao ecossistema .NET"
        "Experimentação rápida de conceitos com F# REPL"
        "Visualização avançada de dados e algoritmos"
        "Geração automática de testes a partir de exemplos documentados"
        "Facilidade de manutenção com conceitos bem documentados"
        "Melhor compreensão de algoritmos complexos como processamento euleriano"
    ]
    
// Exibir pontos-chave
printfn "Pontos-Chave da Abordagem Mista F#/C#:"
getMixedApproachKeyPoints() |> List.iter (fun p -> printfn "- %s" p)
