using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace AIModelIntegration.KnowledgeBaseAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIAgentController() : ControllerBase
    {
        private readonly ChatClient _chatClient = new(
                model: "gpt-4o-mini",
                apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            );

        [HttpPost("ask")]
        public async Task<IActionResult> AskAgent([FromBody] UserPormtRequest userPormtRequest)
        {
            if (string.IsNullOrWhiteSpace(userPormtRequest.Prompt))
                return BadRequest("Prompt cannot be empty.");

            string result = "";

            await foreach (var message in _chatClient.CompleteChatStreamingAsync(userPormtRequest.Prompt))
            {
                foreach (var item in message.ContentUpdate)
                {
                    result += item.Text;
                }
            }

            return Ok(new { AgentResponse = result });
        }

        public class UserPormtRequest
        {
            public string? Prompt { get; set; }
        }
    }
}
