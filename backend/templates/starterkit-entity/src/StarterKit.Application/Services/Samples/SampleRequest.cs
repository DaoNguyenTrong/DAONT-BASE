using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Samples;

public sealed record SampleRequest([Required] string Name);
