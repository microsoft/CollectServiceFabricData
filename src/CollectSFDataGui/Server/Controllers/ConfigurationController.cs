using CollectSFData;
using CollectSFData.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CollectSFDataGui.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private static Collector _collector = new Collector(new string[0], false);

        private readonly ILogger<ConfigurationController> _logger;

        private static ConfigurationOptions _config { get; set; }

        static ConfigurationController()
        {
            _config = _collector.Config;
        }

        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [Route("/api")]
        public IEnumerable<JsonResult> Get()
        {
            //_logger.LogWarning($"Get:enter:request:{resource}");
            ConfigurationProperties var = _config.Clone();
            string jsonString = JsonSerializer.Serialize(var);
            _logger.LogWarning($"Get:enter:jsonString:{jsonString}");

            //    return new List<JsonResult>() { new JsonResult(_config.Clone()) }.AsEnumerable();
            return new List<JsonResult>() { new JsonResult(new ConfigurationProperties()) }.AsEnumerable();
        }

        [HttpGet]
        [Route("/api/configuration")]
        public IEnumerable<JsonResult> GetConfiguration()//(string resource = null)
        {
            //_logger.LogWarning($"Get:enter:request:{resource}");
            ConfigurationProperties var = _config.Clone();
            string jsonString = JsonSerializer.Serialize(var);
            _logger.LogWarning($"Get:enter:jsonString:{jsonString}");

            //    return new List<JsonResult>() { new JsonResult(_config.Clone()) }.AsEnumerable();
            return new List<JsonResult>() { new JsonResult(_config.Clone()) }.AsEnumerable();
        }

        [HttpPost("/api/configuration/update")]
        public IActionResult ImportConfiguration([FromBody] ConfigurationProperties properties)
        {
            // todo:implement
            return Created($"update/{properties.GatherType}", properties);
        }

        [HttpGet]
        [Route("/api/index")]
        public IEnumerable<ConfigurationProperties> Index()
        {
            return new List<ConfigurationProperties>() { _config.Clone() }.AsEnumerable();
        }
    }
}