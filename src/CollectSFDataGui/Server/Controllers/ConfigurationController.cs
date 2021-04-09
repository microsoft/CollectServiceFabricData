using CollectSFDataGui.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CollectSFData;
using CollectSFData.Common;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text;

namespace CollectSFDataGui.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConfigurationController : ControllerBase
    {
        static ConfigurationController()
        {
            _config = _collector.Config;
        }
        private static Collector _collector = new Collector(new string[0], false);

        private static ConfigurationOptions _config { get; set; }

        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
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
        [Route("/GetConfiguration")]
        public IEnumerable<JsonResult> GetConfiguration()//(string resource = null)
        {
            //_logger.LogWarning($"Get:enter:request:{resource}");
            ConfigurationProperties var = _config.Clone();
            string jsonString = JsonSerializer.Serialize(var);
            _logger.LogWarning($"Get:enter:jsonString:{jsonString}");

            //    return new List<JsonResult>() { new JsonResult(_config.Clone()) }.AsEnumerable();
            return new List<JsonResult>() { new JsonResult(new ConfigurationProperties()) }.AsEnumerable();
        }

        [HttpGet]
        [Route("/Index")]
        public IEnumerable<ConfigurationProperties> Index()
        {
            return new List<ConfigurationProperties>() { _config.Clone() }.AsEnumerable();
        }

    }
}
