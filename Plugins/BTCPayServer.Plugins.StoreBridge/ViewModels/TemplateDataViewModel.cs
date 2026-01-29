using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.StoreBridge.Data;
using Microsoft.AspNetCore.Http;

namespace BTCPayServer.Plugins.StoreBridge.ViewModels;

public class TemplateDataViewModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Tags { get; set; }
    public string UploadedBy { get; set; }
    public byte[] FileData { get; set; }
}

public class TemplateGalleryViewModel
{
    public List<TemplateViewModel> Templates { get; set; } = new();
}

public class TemplateViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int DownloadCount { get; set; }
    public List<string> IncludedOptions { get; set; } = new();
}

public class TemplateDetailsViewModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public string UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public int DownloadCount { get; set; }
    public List<IncludedOptionViewModel> IncludedOptions { get; set; } = new();
}

public class IncludedOptionViewModel
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

public class ManageTemplatesViewModel
{
    public List<TemplateData> Templates { get; set; } = new();
}

public class UploadTemplateViewModel
{
    [Required]
    [Display(Name = "Template Name")]
    public string Name { get; set; }

    [StringLength(100, ErrorMessage = "Description cannot exceed 100 characters")]
    public string Description { get; set; }

    [Display(Name = "Tags")]
    public string Tags { get; set; }

    [Required]
    [Display(Name = "Template File")]
    public IFormFile TemplateFile { get; set; }
}