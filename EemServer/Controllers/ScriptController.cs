using EemCore.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EemServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "EemApiScope")]
    public class ScriptController : ControllerBase
    {
        private readonly GenAIScriptProcessor _scriptProcessor;
        private readonly ILogger<ScriptController> _logger;

        public ScriptController(
            GenAIScriptProcessor scriptProcessor,
            ILogger<ScriptController> logger)
        {
            _scriptProcessor = scriptProcessor;
            _logger = logger;
        }

        [HttpGet("list")]
        public async Task<IActionResult> ListScripts()
        {
            try
            {
                string scripts = await _scriptProcessor.ListScriptsAsync();
                return Ok(new { success = true, scripts });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar scripts");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{name}")]
        public async Task<IActionResult> GetScript(string name)
        {
            try
            {
                string script = await _scriptProcessor.GetScriptAsync(name);
                return Ok(new { success = true, script });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter script");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("save")]
        public async Task<IActionResult> SaveScript(SaveScriptRequest request)
        {
            try
            {
                string result = await _scriptProcessor.SaveScriptAsync(
                    request.Content,
                    request.Name,
                    request.Description,
                    request.Tags);
                    
                return Ok(new { success = true, message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar script");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpDelete("{name}")]
        public async Task<IActionResult> DeleteScript(string name)
        {
            try
            {
                string result = await _scriptProcessor.DeleteScriptAsync(name);
                return Ok(new { success = true, message = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir script");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteScript(ExecuteScriptRequest request)
        {
            try
            {
                string result = await _scriptProcessor.ExecuteScriptAsync(
                    request.Name,
                    request.Input);
                    
                return Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar script");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class SaveScriptRequest
    {
        public string Content { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Tags { get; set; }
    }

    public class ExecuteScriptRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Input { get; set; } = "{}";
    }
}