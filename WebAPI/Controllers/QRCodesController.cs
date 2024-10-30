using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DataLayer.Welding;
using BusinessLayer.Accounts;
using WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using WebAPI.Communication;
using BusinessLayer;
using System.IO;
using Microsoft.AspNetCore.Http;
using BusinessLayer.Configuration;
using Microsoft.Extensions.Options;
using BusinessLayer.Interfaces.Storage;
using System.Drawing;
using System.Drawing.Imaging;
using QRCoder;
using BusinessLayer.Models;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class QRCodesController : ControllerBaseAuthenticated
    {
        IDocumentsService _documentsService;

        public QRCodesController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult CodeV1(string entity, string id)
        {
            var service = new ScancodesService();

            var outputStream = service.CodeV1(new ScancodeEntity { Entity = entity, ID = id });

            return File(outputStream, "image/jpeg");
        }

        // ==============================================================================================
        [AllowAnonymous]
        [HttpGet]
        public IActionResult CodeV1ByCode(string code)
        {
            var service = new ScancodesService();

            var outputStream = service.CodeV1(code);

            return File(outputStream, "image/jpeg");
        }

    }



    public class ScancodesService
    {
        // Barcode or QR?
        bool useQR = false;

        public Stream CodeV1(string code)
        {

            Bitmap codeImage = null;

            if (useQR)
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                codeImage = qrCode.GetGraphic(20);
            }
            else
            {
                // Barcode
                var ean13 = new BarcodeLib.Barcode(code, BarcodeLib.TYPE.EAN13);
                ean13.ImageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                ean13.BarWidth = 2;
                ean13.IncludeLabel = true;
                ean13.StandardizeLabel = false;
                ean13.LabelPosition = BarcodeLib.LabelPositions.BOTTOMCENTER;
                ean13.LabelFont = new Font(SystemFonts.DefaultFont.FontFamily, (float)18);

                codeImage = (Bitmap)ean13.Encode(BarcodeLib.TYPE.EAN13, code);
            }


            var outputStream = new MemoryStream();
            codeImage.Save(outputStream, ImageFormat.Jpeg);
            outputStream.Seek(0, SeekOrigin.Begin);

            return outputStream;
        }

        public Stream CodeV1(ScancodeEntity entity)
        {
            var code = BusinessLayer.Utils.ScancodesHelper.GenerateCode(useQR, entity);

            return CodeV1(code);
        }

    }

}
