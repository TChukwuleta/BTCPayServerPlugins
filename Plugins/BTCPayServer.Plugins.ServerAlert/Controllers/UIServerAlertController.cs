using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Plugins.ServerAlert.Services;
using BTCPayServer.Plugins.ServerAlert.ViewModels;
using BTCPayServer.Services.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.ServerAlert;

[Route("~/plugins/server-alerts/")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyServerSettings)]
public class UIServerAlertController(StoreRepository storeRepository, 
    ServerAlertService serverAlertService, 
    IHttpContextAccessor httpContextAccessor) : Controller
{
    private string GetServerName()
    {
        var ctx = httpContextAccessor.HttpContext;
        return ctx?.Request.Host.Host ?? "BTCPay Server";
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new HeraldIndexViewModel { Announcements = await serverAlertService.GetPublishedAnnouncement() });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
        => View("CreateEdit", new AnnouncementViewModel
        {
            EmailEnabled = await serverAlertService.IsEmailSettingsConfigured(),
            AllStores = (await storeRepository.GetStores()).ToList()
        });


    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AllStores = (await storeRepository.GetStores()).ToList();
            return View("CreateEdit", model);
        }
        var result = await serverAlertService.CreateAndSendAnnouncement(model.ToEntity(), GetServerName());
        TempData[WellKnownTempData.SuccessMessage] = BuildSendMessage(result);
        return RedirectToAction(nameof(Index));
    }


    [HttpGet("{id}/update")]
    public async Task<IActionResult> Update(string id)
    {
        var entity = await serverAlertService.GetAnnouncement(id);
        if (entity is null) return NotFound();

        var vm = AnnouncementViewModel.FromEntity(entity, (await storeRepository.GetStores()).ToList());
        vm.EmailEnabled = await serverAlertService.IsEmailSettingsConfigured();
        return View("CreateEdit", vm);
    }


    [HttpPost("{id}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string id, AnnouncementViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AllStores = (await storeRepository.GetStores()).ToList();
            return View("CreateEdit", model);
        }
        model.Id = id;
        if (!await serverAlertService.UpdateAnnouncement(model)) return NotFound();

        TempData[WellKnownTempData.SuccessMessage] = "Server alert updated";
        return RedirectToAction(nameof(Index));
    }


    [HttpPost("{id}/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(string id)
    {
        var result = await serverAlertService.RepublishAnnouncement(id, GetServerName());
        if (!result.Found) return NotFound();

        TempData[WellKnownTempData.SuccessMessage] = BuildSendMessage(result);
        return RedirectToAction(nameof(Index));
    }


    [HttpGet("{id}/delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var entity = await serverAlertService.GetAnnouncement(id);
        if (entity == null) return NotFound();

        return View("Confirm", new ConfirmModel($"Delete Server Alert", $"Server alert: '{entity.Title}' would also be deleted. Are you sure?", "Delete Alert"));
    }


    [HttpPost("{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(string id)
    {
        await serverAlertService.DeleteAnnouncement(id);
        TempData[WellKnownTempData.SuccessMessage] = "Server alert deleted";
        return RedirectToAction(nameof(Index));
    }

    private static string BuildSendMessage(PublishResult r)
    {
        var msg = $"{r.BellCount} bell notification(s) sent.";
        if (r.EmailCount > 0)
            msg += $" {r.EmailCount} email(s) dispatched.";
        else if (r.EmailScopeWasSet)
            msg += " No emails sent — SMTP not configured or no recipients matched.";
        return msg;
    }
}
