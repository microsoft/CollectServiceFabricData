using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;

namespace CollectSFDataGui.Server.Controllers
{
    [DisableRequestSizeLimit]
    public partial class UploadController : Controller
    {
        private readonly IWebHostEnvironment environment;

        public UploadController(IWebHostEnvironment environment)
        {
            this.environment = environment;
        }

        //[HttpPost("upload/multiple")]
        //public IActionResult Multiple(IFormFile[] files)
        //{
        //    try
        //    {
        //        // Put your code here
        //        return StatusCode(200);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, ex.Message);
        //    }
        //}

        //[HttpPost("upload/{id}")]
        //public IActionResult Post(IFormFile[] files, int id)
        //{
        //    try
        //    {
        //        // Put your code here
        //        return StatusCode(200);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, ex.Message);
        //    }
        //}

        [HttpPost("upload/single")]
        public IActionResult Single(IFormFile file)
        {
            try
            {
                string jsonString = new StreamReader(file.OpenReadStream()).ReadToEnd();
                return Ok(jsonString);
                //return Ok(new { Completed = true, Json = jsonString });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}