using Microsoft.AspNetCore.Mvc;

namespace RaidAdminPanel.Controllers;

/// <summary>
/// Корень SPT (/) — встроенная Blazor-страница «Thank You». Админка — статика под /RaidAdminPanel/.
/// Эти маршруты дают короткие URL-алиасы.
/// </summary>
[ApiController]
public class RaidAdminWebController : ControllerBase
{
    private const string PanelPath = "/RaidAdminPanel/index.html";

    [HttpGet("/admin")]
    [HttpGet("/raidadmin")]
    public IActionResult OpenPanel() => Redirect(PanelPath);
}
