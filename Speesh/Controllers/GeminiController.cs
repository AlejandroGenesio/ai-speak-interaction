using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using Google.GenAI;
using Google.GenAI.Types;
using Speesh.Services;
using Google.Protobuf.Collections;

namespace Speesh.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GeminiController : ControllerBase
    {
        private readonly ConversationHistoryService _historyService;

        public GeminiController(ConversationHistoryService historyService)
        {
            _historyService = historyService;
        }

        // GET /Gemini?input=your question
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return BadRequest(new { error = "Query parameter 'input' is required." });
            }

            var client = new Client();
            string model = "gemini-3.6-flash";

            // Use the singleton history so we preserve the session across requests.
            var history = _historyService.GetHistory();

            // System instruction to give Gemini a personality and set the language
            var systemInstruction = new Content
            {
                Role = "system",
                Parts = new List<Part>
                {
                    new Part { Text = "Tu sei Ash di Pokemon, e parli solo italiano.Non usare mai asterischi o azioni sceniche. Rispondi solo con il testo dialogato" }
                }
            };

            // Do not place the system instruction in the message contents (it is set in the config below).
            // Keep the singleton history for user/assistant messages only.

            // Add user input to multi-turn conversation history
            var userContent = new Content
            {
                Role = "user",
                Parts = new List<Part>
                {
                    new Part { Text = input }
                }
            };
            _historyService.AddContent(userContent);

            try
            {
                var response = await CallWithRetryAsync(client, model, history, systemInstruction);

                if (response is not null && !string.IsNullOrEmpty(response.Text))
            {
                // Append model response to history to preserve the session
                var assistantContent = new Content
                {
                    Role = "assistant",
                    Parts = new List<Part>
                    {
                        new Part { Text = response.Text }
                    }
                };

                _historyService.AddContent(assistantContent);

                    return Ok(new { input = input, response = response.Text });
            }
                return StatusCode(502, new { error = "Empty response from Gemini API." });
            }
            catch (System.Exception ex)
            {
                // Return the exception message to the caller to aid debugging (do not expose sensitive info in production).
                return StatusCode(502, new { error = ex.Message });
            }
        }

        private async Task<GenerateContentResponse?> CallWithRetryAsync(Client client, string modelName, List<Content> contents, Content systemInstruction)
        {
            var rnd = new System.Random();
            const int maxAttempts = 5;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var config = new GenerateContentConfig
                    {
                        SystemInstruction = systemInstruction
                    };

                    return await client.Models.GenerateContentAsync(
                        model: modelName,
                        contents: contents,
                        config: config
                    );
                }
                catch (Google.GenAI.ClientError ex)
                {
                    var message = ex.Message ?? string.Empty;

                    var isTransient = message.IndexOf("high demand", System.StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("rate limit", System.StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("try again later", System.StringComparison.OrdinalIgnoreCase) >= 0
                                      || message.IndexOf("temporarily", System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isTransient || attempt == maxAttempts)
                    {
                        // Final failure: surface the API error to the caller
                        throw new System.InvalidOperationException($"Gemini API client error: {ex.Message}", ex);
                    }

                    var backoffMs = (int)(System.Math.Pow(2, attempt) * 1000) + rnd.Next(0, 500);
                    await Task.Delay(backoffMs);
                    continue;
                }
                catch (System.Exception ex)
                {
                    if (attempt == maxAttempts)
                    {
                        // Final failure: rethrow to surface to caller
                        throw;
                    }

                    var backoffMs = (int)(System.Math.Pow(2, attempt) * 1000) + rnd.Next(0, 500);
                    await Task.Delay(backoffMs);
                    continue;
                }
            }

            throw new System.InvalidOperationException("Failed to get a response from Gemini API after retries.");
        }
    }
}
