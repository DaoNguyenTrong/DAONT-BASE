using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace StarterKit.API.Json;

public sealed class ConfigureNewtonsoftJsonOptions(
    IHttpContextAccessor httpContextAccessor) : IConfigureOptions<MvcNewtonsoftJsonOptions>
{
    public void Configure(MvcNewtonsoftJsonOptions options)
    {
        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.DateParseHandling = DateParseHandling.None;
        options.SerializerSettings.Converters.Add(new StringEnumConverter
        {
            AllowIntegerValues = false
        });
        options.SerializerSettings.Converters.Add(new TimeZoneDateTimeConverter(httpContextAccessor));
    }
}
