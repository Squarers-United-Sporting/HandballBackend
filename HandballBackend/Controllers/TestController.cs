using HandballBackend.Authentication;
using HandballBackend.EndpointHelpers;
using HandballBackend.ErrorTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HandballBackend.Controllers;

[ApiController]
[Authorize(Policy = Policies.IsAdmin)]
[Route("/api/[controller]")]
public class TestController : ControllerBase {
    [HttpPost("backup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> CreateBackup() {
        await PostgresBackup.MakeTimestampedBackup("requested", force: true);
        return Ok();
    }

    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult UpdateFromGit() {
        if (!Config.CHECKING_GIT) {
            return UnprocessableEntity(new ActionNotAllowed("The server does not have updating enabled"));
        }

        _ = Task.Run(async () => {
            await Task.Delay(200);
            ServerManagementHelper.UpdateServer();
        });
        return Ok();
    }

    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult RestartServer() {
        if (!Config.CHECKING_GIT) {
            return UnprocessableEntity(new ActionNotAllowed("The server does not have updating enabled"));
        }

        _ = Task.Run(async () => {
            await Task.Delay(200);
            ServerManagementHelper.RestartServer();
        });
        return Ok();
    }

    [HttpPost("rebuild")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult RebuildServer() {
        if (!Config.CHECKING_GIT) {
            return UnprocessableEntity(new ActionNotAllowed("The server does not have updating enabled"));
        }

        _ = Task.Run(async () => {
            await Task.Delay(200);
            ServerManagementHelper.RebuildServer();
        });
        return Ok();
    }

    [HttpPost("exit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult QuitServer() {
        _ = Task.Run(async () => {
            await Task.Delay(200);
            ServerManagementHelper.QuitServer();
        });
        return Ok();
    }

    public class GetLogResponse {
        public required string Log { get; set; }
    }

    [HttpGet("log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GetLogResponse>> GetLog() {
        return new GetLogResponse {
            Log = await ExceptionLoggingHelper.Read()
        };
    }

    [HttpPost("log/clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ClearLog() {
        await ExceptionLoggingHelper.Clear();
        return Ok();
    }
}