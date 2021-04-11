using CollectSFData;
using CollectSFData.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CollectSFDataGui.Server.Controllers
{
    // https://docs.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-5.0
    [ApiController]
    [Route("[controller]")]
    [Produces("application/json")]
    public class ConfigurationController : ControllerBase
    {
        private static Collector _collector = new Collector(new string[0], false);

        private readonly ILogger<ConfigurationController> _logger;

        private static ConfigurationOptions _config => _collector.Config;// { get; set; }

        static ConfigurationController()
        {
        }

        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("/api")]
        public IEnumerable<JsonResult> Get()
        {
            ConfigurationOptions ConfigurationOptions = _config.Clone();
            string jsonString = JsonSerializer.Serialize(ConfigurationOptions, GetJsonSerializerOptions());

            _logger.LogInformation($"Get:enter:jsonString:{jsonString}");
            return new List<JsonResult>() { new JsonResult(new ConfigurationOptions()) }.AsEnumerable();
        }

        [HttpGet]
        [Route("/api/configuration")]
        public IEnumerable<JsonResult> GetConfiguration()//(string resource = null)
        {
            ConfigurationOptions ConfigurationOptions = _config.Clone();
            string jsonString = JsonSerializer.Serialize(ConfigurationOptions, GetJsonSerializerOptions());
            _logger.LogInformation($"Get:enter:jsonString:{jsonString}");

            JsonResult jsonResult = new JsonResult(ConfigurationOptions, GetJsonSerializerOptions());
            jsonResult.ContentType = "application/json;charset=utf-8";

            return new List<JsonResult>() { jsonResult }.AsEnumerable();
        }

        [HttpPost("/api/configuration/update")]
        public IActionResult ImportConfiguration([FromBody] string properties)
        {
            try
            {
                ConfigurationOptions configurationProperties = JsonSerializer.Deserialize<ConfigurationOptions>(properties);
                _config.MergeConfig(configurationProperties);

                ConfigurationOptions newConfigurationOptions = _config.Clone();

                JsonResult jsonResult = new JsonResult(newConfigurationOptions, GetJsonSerializerOptions());
                jsonResult.ContentType = "application/json;charset=utf-8";
                string jsonString = JsonSerializer.Serialize(newConfigurationOptions, GetJsonSerializerOptions());

                return Created($"/api/configuration/update", jsonString);
            }
            catch (Exception e)
            {
                _logger.LogError($"exception:{e}");
                return BadRequest($"update/{properties} error:{e}");
            }
        }

        [HttpGet]
        [Route("/api/index")]
        public IEnumerable<ConfigurationOptions> Index()
        {
            return new List<ConfigurationOptions>() { _config.Clone() }.AsEnumerable();
        }

        private JsonSerializerOptions GetJsonSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions(JsonSerializerDefaults.General);
            options.AllowTrailingCommas = true;
            options.MaxDepth = 10;
            options.PropertyNameCaseInsensitive = false;
            return options;
        }
    }
}