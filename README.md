# Εεm: Documentação Executável

Este projeto combina documentação com código executável para o sistema Εεm (Ευ-εnable-memory), permitindo que você entenda os conceitos e implemente o sistema simultaneamente.

## Estrutura do Projeto

A solução está estruturada da seguinte forma:

```
EemSolution/
├── EemCore/               # Biblioteca principal do Εεm
│   ├── Agents/            # Implementação dos agentes funcionais
│   └── Processing/        # Processadores de dados e scripts
├── EemDocs/               # Documentação F# executável
│   ├── Introduction.fsx   # Visão geral e conceitos básicos
│   ├── Architecture.fsx   # Arquitetura do sistema
│   ├── GenAIScriptAndDSL.fsx  # DSLs para processamento euleriano
│   ├── VisualStudioIntegration.fsx  # Integração com VS
│   ├── EemDocsToCSharp.fsx  # Mapeamento de F# para C#
│   └── ...                # Outros documentos
├── EemServer/             # Servidor MCP para Εεm
└── EemVsExtension/        # Extensão para Visual Studio
```

## Benefícios da Documentação Executável

A abordagem de documentação executável do Εεm oferece várias vantagens:

1. **Código como documentação**: Os conceitos são apresentados como código F# executável
2. **Experimentação interativa**: Execute e modifique os exemplos diretamente no F# Interactive
3. **Visualizações integradas**: Gráficos e diagramas são gerados a partir do código real
4. **Validação automática**: A documentação é executável e testável, evitando desatualização
5. **Mapeamento claro para C#**: Cada conceito F# tem correspondência na implementação C#
6. **Aprendizagem prática**: Entenda o sistema através da experimentação direta

Esta abordagem é particularmente valiosa para conceitos complexos como processamento euleriano
e correlação contextual que se beneficiam da expressividade do F#, enquanto a implementação
se beneficia do vasto ecossistema C#.

## Começando

Para explorar e executar a documentação:

1. Abra a solução `EemSolution.sln` no Visual Studio Enterprise
2. Abra um dos arquivos `.fsx` da pasta `EemDocs`
3. Selecione o código e envie para o F# Interactive (Alt+Enter)
4. Observe os resultados e explore os conceitos interativamente

Para executar a implementação de referência:

1. Configure as chaves de API do Azure no arquivo `appsettings.json` ou Secrets do usuário
2. Execute o projeto `EemServer` para iniciar o servidor MCP
3. Instale a extensão VS Code/Visual Studio para integração com a IDE

## Guias de Implementação

A documentação executável foi organizada para guiá-lo através da implementação do Εεm:

1. **Introdução**: Conceitos básicos e visão geral do sistema
2. **Arquitetura**: Componentes e fluxos de dados
3. **Componentes**: Detalhes de cada tipo de arquivo e agente
4. **Integração Azure**: Configuração dos serviços cloud
5. **MCP**: Implementação do protocolo Model Context Protocol
6. **GenAIScript e DSLs**: Linguagens para processamento euleriano
7. **Integração Visual Studio**: Detalhes da integração com IDE
8. **Mapeamento F# para C#**: Como a documentação se conecta ao código
9. **Instalação**: Guia passo-a-passo para configuração

## Tecnologias Utilizadas

- **F# Interactive**: Para documentação executável e literate programming
- **Semantic Kernel**: Orquestração de IA e agentes
- **MCP (Model Context Protocol)**: Comunicação entre agentes e clientes
- **Azure OpenAI**: Processamento semântico e embeddings
- **Azure Services**: Blob Storage, Cosmos DB, Functions
- **Visual Studio SDK**: Extensões para captura de contexto
- **GenAIScript**: DSL para definição de fluxos eulerianos

## Requisitos

- Visual Studio Enterprise 2022 ou posterior
- .NET 8.0 SDK
- Assinatura Azure (para serviços cloud)
- F# 8.0 (incluído no Visual Studio)

## Notas de Uso com GitHub Copilot

Esta documentação foi projetada para funcionar bem com GitHub Copilot Enterprise, permitindo:

1. Continuar a implementação a partir dos exemplos fornecidos
2. Gerar código para componentes adicionais
3. Adaptar a implementação para necessidades específicas

Ao trabalhar com Copilot, você pode referenciar a documentação nos prompts para obter sugestões mais alinhadas com a arquitetura Εεm.

## Próximos Passos

Após explorar a documentação executável, você pode:

1. Implementar um servidor MCP personalizado
2. Estender os agentes com funcionalidades adicionais
3. Integrar com outras ferramentas da sua stack
4. Contribuir para o projeto com melhorias

## Licença

Este projeto está disponível sob licença MIT.
