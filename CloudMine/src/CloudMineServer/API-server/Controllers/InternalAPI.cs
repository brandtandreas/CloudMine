﻿using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CloudMineServer.Data;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using CloudMineServer.Models;
using CloudMineServer.Interface;
using System;
using System.Linq;

namespace CloudMineServer.Controllers
{
    [Route("api/filechunk/")]
    public class InternalAPI : Controller
    {
        private ApplicationDbContext context;
        public InternalAPI(ApplicationDbContext context)
        {
            this.context = context;
        }

        //[HttpPost]
        //public async Task<IActionResult> UploadMetaData()
        //{
        //    return null;
        //}

        [HttpGet]
        public async Task<IActionResult> MergeAllAndReturn()
        {
            var fileitems = context.dbFileItem.ToList();
            var fis = new FileItemSet();

            var sorted = fileitems.OrderBy(f => f.FileName);

            List<byte> merged = new List<byte>();

            foreach (var item in sorted)
            {
                merged.AddRange(item.FileData);
            }

            var buffer = merged.ToArray();
            var stream = new MemoryStream(buffer);

            return new FileStreamResult(stream, "application/octet-stream");
        }

        [HttpPost]
        public async Task<IActionResult> UploadFileChunk(string fileId)
        {
            for (int i = 0; i < Request.Form.Files.Count; i++)
            {
                var myFile = Request.Form.Files[i];
                if (myFile != null && myFile.Length != 0)
                {
                    var length = myFile.Length;
                    var size = myFile.FileName;

                    var fitem = new FileItem() {
                        FileName = myFile.FileName,
                        FileData = StreamToArray(myFile.OpenReadStream()),
                        FileChunkIndex = i
                    };

                    context.dbFileItem.Add(fitem);
                    context.SaveChanges();
                }
            }


            return new ObjectResult("hejsan");
        }

        private static byte[] StreamToArray(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        private void MergeFileChunks(List<FileItem> chunkedfile)
        {

        }
    }
}
