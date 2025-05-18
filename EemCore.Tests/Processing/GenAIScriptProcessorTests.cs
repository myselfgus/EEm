using EemCore.Data.Repositories;
using EemCore.Models;
using EemCore.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using System.ComponentModel;
using Xunit;

namespace EemCore.Tests.Processing
{
    public class GenAIScriptProcessorTests
    {
        private readonly Mock<IScriptRepository> _mockScriptRepository;
        private readonly Mock<IKernel> _mockKernel;
        private readonly Mock<ILogger<GenAIScriptProcessor>> _mockLogger;
        private readonly GenAIScriptProcessor _processor;

        public GenAIScriptProcessorTests()
        {
            _mockScriptRepository = new Mock<IScriptRepository>();
            _mockKernel = new Mock<IKernel>();
            _mockLogger = new Mock<ILogger<GenAIScriptProcessor>>();
            
            _processor = new GenAIScriptProcessor(
                _mockScriptRepository.Object,
                _mockKernel.Object,
                _mockLogger.Object);
        }
        
        [Fact]
        public async Task ListScriptsAsync_NoScripts_ReturnsEmptyMessage()
        {
            // Arrange
            _mockScriptRepository
                .Setup(x => x.ListScriptsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ScriptInfo>());
                
            // Act
            var result = await _processor.ListScriptsAsync();
            
            // Assert
            Assert.Contains("Nenhum script GenAIScript encontrado no sistema", result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.ListScriptsAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task ListScriptsAsync_WithScripts_ReturnsFormattedList()
        {
            // Arrange
            var scripts = new List<ScriptInfo>
            {
                new ScriptInfo 
                { 
                    Id = "script1", 
                    Name = "Script 1", 
                    Description = "First test script",
                    CreatedDateTime = new DateTime(2023, 1, 1),
                    Tags = new List<string> { "test", "first" }
                },
                new ScriptInfo 
                { 
                    Id = "script2", 
                    Name = "Script 2", 
                    Description = "Second test script",
                    CreatedDateTime = new DateTime(2023, 1, 2),
                    Tags = new List<string>()
                }
            };
            
            _mockScriptRepository
                .Setup(x => x.ListScriptsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(scripts);
                
            // Act
            var result = await _processor.ListScriptsAsync();
            
            // Assert
            Assert.Contains("Scripts GenAIScript Disponíveis", result);
            Assert.Contains("Script 1", result);
            Assert.Contains("First test script", result);
            Assert.Contains("Script 2", result);
            Assert.Contains("Second test script", result);
            Assert.Contains("test, first", result); // Tags for first script
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.ListScriptsAsync(It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task SaveScriptAsync_ValidScript_ReturnsSaveConfirmation()
        {
            // Arrange
            var content = "console.log('Test script content');";
            var name = "TestScript";
            var description = "A test script";
            var tags = "test,unit";
            var scriptId = "script123";
            
            _mockScriptRepository
                .Setup(x => x.SaveScriptAsync(
                    It.IsAny<ScriptInfo>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(scriptId);
                
            // Act
            var result = await _processor.SaveScriptAsync(content, name, description, tags);
            
            // Assert
            Assert.Contains($"Script '{name}' salvo com sucesso", result);
            Assert.Contains(scriptId, result);
            
            // Verify repository call with correct parameters
            _mockScriptRepository.Verify(
                x => x.SaveScriptAsync(
                    It.Is<ScriptInfo>(s => 
                        s.Name == name && 
                        s.Description == description &&
                        s.Tags.Count == 2 &&
                        s.Tags.Contains("test") &&
                        s.Tags.Contains("unit")),
                    content,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task SaveScriptAsync_EmptyContent_ReturnsError()
        {
            // Arrange
            var content = "";
            var name = "TestScript";
            var description = "A test script";
            
            // Act
            var result = await _processor.SaveScriptAsync(content, name, description);
            
            // Assert
            Assert.Contains("Erro: O conteúdo do script não pode estar vazio", result);
            
            // Verify repository call was not made
            _mockScriptRepository.Verify(
                x => x.SaveScriptAsync(
                    It.IsAny<ScriptInfo>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
        
        [Fact]
        public async Task GetScriptAsync_ExistingScript_ReturnsScriptDetails()
        {
            // Arrange
            var scriptName = "TestScript";
            var scriptInfo = new ScriptInfo
            {
                Id = "script123",
                Name = scriptName,
                Description = "A test script",
                CreatedDateTime = DateTime.UtcNow,
                ModifiedDateTime = DateTime.UtcNow,
                Tags = new List<string> { "test", "unit" }
            };
            var content = "console.log('Test script content');";
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((scriptInfo, content));
                
            // Act
            var result = await _processor.GetScriptAsync(scriptName);
            
            // Assert
            Assert.Contains($"Script: {scriptName}", result);
            Assert.Contains(scriptInfo.Description, result);
            Assert.Contains(scriptInfo.Id, result);
            Assert.Contains("test, unit", result); // Tags
            Assert.Contains(content, result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task GetScriptAsync_NonExistingScript_ReturnsNotFoundMessage()
        {
            // Arrange
            var scriptName = "NonExistingScript";
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((null as (ScriptInfo, string)?));
                
            // Act
            var result = await _processor.GetScriptAsync(scriptName);
            
            // Assert
            Assert.Contains($"Script '{scriptName}' não encontrado", result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task DeleteScriptAsync_ExistingScript_ReturnsDeleteConfirmation()
        {
            // Arrange
            var scriptName = "TestScript";
            var scriptInfo = new ScriptInfo
            {
                Id = "script123",
                Name = scriptName
            };
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((scriptInfo, "content"));
                
            _mockScriptRepository
                .Setup(x => x.DeleteScriptAsync(
                    scriptInfo.Id,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
            // Act
            var result = await _processor.DeleteScriptAsync(scriptName);
            
            // Assert
            Assert.Contains($"Script '{scriptName}' excluído com sucesso", result);
            
            // Verify repository calls
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
                
            _mockScriptRepository.Verify(
                x => x.DeleteScriptAsync(
                    scriptInfo.Id,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task DeleteScriptAsync_NonExistingScript_ReturnsNotFoundMessage()
        {
            // Arrange
            var scriptName = "NonExistingScript";
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((null as (ScriptInfo, string)?));
                
            // Act
            var result = await _processor.DeleteScriptAsync(scriptName);
            
            // Assert
            Assert.Contains($"Script '{scriptName}' não encontrado", result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
                
            _mockScriptRepository.Verify(
                x => x.DeleteScriptAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
        
        [Fact]
        public async Task ExecuteScriptAsync_ExistingScript_ReturnsExecutionResult()
        {
            // Arrange
            var scriptName = "TestScript";
            var scriptInfo = new ScriptInfo
            {
                Id = "script123",
                Name = scriptName
            };
            var content = "console.log('Test script content');";
            var input = "{\"param\": \"value\"}";
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((scriptInfo, content));
                
            // Act
            var result = await _processor.ExecuteScriptAsync(scriptName, input);
            
            // Assert
            Assert.Contains($"Simulação de execução do script '{scriptName}'", result);
            Assert.Contains(input, result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
        
        [Fact]
        public async Task ExecuteScriptAsync_NonExistingScript_ReturnsNotFoundMessage()
        {
            // Arrange
            var scriptName = "NonExistingScript";
            
            _mockScriptRepository
                .Setup(x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((null as (ScriptInfo, string)?));
                
            // Act
            var result = await _processor.ExecuteScriptAsync(scriptName);
            
            // Assert
            Assert.Contains($"Script '{scriptName}' não encontrado", result);
            
            // Verify repository call
            _mockScriptRepository.Verify(
                x => x.GetScriptByNameAsync(
                    scriptName,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}