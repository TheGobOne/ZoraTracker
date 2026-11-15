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

                // Every message goes to every connected tracker, whatever pair it belongs to. Each tracker keeps only the messages whose senderId
                // is its partner's console ID (the TargetConsoleId check in the app) and drops the rest. So the relay is not private: anyone
                // connected sees every pair's messages and can send as any console ID, and a tracker must treat what arrives as untrusted
                // (the app bounds the version numbers it takes, see CoopSync.MaxPartnerVersion).
                // (Kept as Clients.All on purpose: the trackers already released do not join any SignalR group, so sending to groups would cut
                // them off. Groups could be added later as an extra the old trackers ignore, once every tracker joins one.)
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
