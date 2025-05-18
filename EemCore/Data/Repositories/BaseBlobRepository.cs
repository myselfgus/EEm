using EemCore.Services.Resilient;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Memory;

namespace EemCore.Data.Repositories
{
    /// <summary>
    /// Classe base abstrata para repositórios que usam Azure Blob Storage
    /// </summary>
    public abstract class BaseBlobRepository
    {
        protected readonly ResilientBlobServiceClient _blobServiceClient;
        protected readonly ISemanticTextMemory _memory;
        protected readonly ILogger _logger;
        
        public BaseBlobRepository(
            ResilientBlobServiceClient blobServiceClient,
            ISemanticTextMemory memory,
            ILogger logger)
        {
            _blobServiceClient = blobServiceClient;
            _memory = memory;
            _logger = logger;
        }
        
        /// <summary>
        /// Garante que um container existe
        /// </summary>
        /// <param name="containerName">Nome do container</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        protected async Task EnsureContainerExistsAsync(string containerName, CancellationToken cancellationToken = default