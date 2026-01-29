using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Plugins.StoreBridge.Data;
using BTCPayServer.Plugins.StoreBridge.ViewModels;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.StoreBridge.Services;

public class TemplateService
{
    private readonly StoreImportExportService _bridgeService;
    private readonly StoreBridgeDbContextFactory _dbContextFactory;

    public TemplateService(StoreBridgeDbContextFactory dbContextFactory, StoreImportExportService bridgeService)
    {
        _bridgeService = bridgeService;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<(bool success, string message)> UploadTemplate(TemplateDataViewModel vm)
    {
        var exportData = _bridgeService.ParseExport(vm.FileData);
        if (exportData == null)
            return (false, "Invalid template file");

        await using var ctx = _dbContextFactory.CreateContext();
        var includedOptions = _bridgeService.GetAvailableImportOptions(vm.FileData);
        var template = new TemplateData
        {
            Name = vm.Name,
            Description = vm.Description,
            Category = vm.Category,
            Tags = vm.Tags,
            UploadedBy = vm.UploadedBy,
            UploadedAt = DateTimeOffset.UtcNow,
            FileData = vm.FileData,
            Version = exportData.Version.ToString(),
            IncludedOptions = JsonConvert.SerializeObject(includedOptions),
            DownloadCount = 0
        };
        ctx.StoreBridgeTemplates.Add(template);
        await ctx.SaveChangesAsync();
        return (true, template.Id);
    }

    public async Task<List<TemplateData>> GetAllTemplates()
    {
        await using var ctx = _dbContextFactory.CreateContext();
        return ctx.StoreBridgeTemplates.OrderByDescending(t => t.UploadedAt).ToList();
    }

    public async Task<TemplateData?> GetTemplate(string id)
    {
        await using var ctx = _dbContextFactory.CreateContext();
        return ctx.StoreBridgeTemplates.FirstOrDefault(t => t.Id == id);
    }


    public async Task DeleteTemplate(string id)
    {
        var template = await GetTemplate(id);
        if (template != null)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            ctx.StoreBridgeTemplates.Remove(template);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task IncrementDownloadCount(string id)
    {
        var template = await GetTemplate(id);
        if (template != null)
        {
            await using var ctx = _dbContextFactory.CreateContext();
            template.DownloadCount++;
            await ctx.SaveChangesAsync();
        }
    }
}