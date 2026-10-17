using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Z1R_SignalRHost.Models;

namespace Z1R_SignalRHost.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SyncUpdateController : ControllerBase
    {
        private readonly IHubContext<SyncHub> _hubContext;

        public SyncUpdateController(IHubContext<SyncHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SyncUpdatePayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.MessageType) ||
                string.IsNullOrWhiteSpace(payload.SenderId) ||
                payload.Payload == null)
            {
                Console.WriteLine("[SyncUpdate] Invalid payload: missing fields.");
                return BadRequest("Missing required fields");
            }

            var message = new
            {
                messageType = payload.MessageType,
                senderId = payload.SenderId,
                payload = payload.Payload,
                timeStamp = payload.TimeStamp
            };

            try
            {
                var jsonLength = JsonConvert.SerializeObject(message).Length;
                Console.WriteLine($"[SyncUpdate] Broadcasting {payload.MessageType} from {payload.SenderId} (size: {jsonLength} chars)");

                await _hubContext.Clients.All.SendAsync("ReceiveSyncMessage", message);

                Console.WriteLine("[SyncUpdate] Broadcast complete.");
                return Ok("Message sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SyncUpdate] ERROR broadcasting: {ex.Message}");
                return StatusCode(500, "Broadcast failed");
            }
        }

    }
}
