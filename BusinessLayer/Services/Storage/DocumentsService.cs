using DataLayer.Welding;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services.Storage
{
    public class DocumentsService : Interfaces.Storage.IDocumentsService
    {
        WeldingContext _context;
        Configuration.StorageOptions _storageOptions;

        public DocumentsService(WeldingContext context, IOptions<Configuration.StorageOptions> storageOptionsAccessor)
        {
            _context = context;
            _storageOptions = storageOptionsAccessor.Value;
        }

        public DataLayer.Welding.Document Copy(Guid GUID)
        {
            var doc = _context.Documents.Find(GUID);
            if (doc == null)
                return null;


            var docNew = new Document
            {
                GUID = Guid.NewGuid(),
                DateCreated = DateTime.Now,
                AddedUserID = doc.AddedUserID,
                ContentType = doc.ContentType,
                Filename = doc.Filename,
                Length = doc.Length,
                ImageHeight = doc.ImageHeight,
                ImageWidth = doc.ImageWidth                
            };

            // Do copy
            string sourceFullFilename = Path.Combine(_storageOptions.StoragePath, doc.GUID.ToString());
            string destFullFilename = Path.Combine(_storageOptions.StoragePath, docNew.GUID.ToString());
            try
            {
                File.Copy(sourceFullFilename, destFullFilename);
            }
            catch { }


            // Save to database
            _context.Documents.Add(docNew);

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return docNew;
        }

        public DataLayer.Welding.Document AddDocument(Stream stream, string contentType, string filename, int? userID)
        {
            var doc = new Document
            {
                GUID = Guid.NewGuid(),
                DateCreated = DateTime.Now,
                AddedUserID = userID,
                ContentType = contentType,
                Filename = filename
            };

            // Save file
            string fullFilename = Path.Combine(_storageOptions.StoragePath, doc.GUID.ToString());
            using (var fileStream = new FileStream(fullFilename, FileMode.Create))
            {
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fileStream);
            }

            // Read file length
            var fileInfo = new FileInfo(fullFilename);
            doc.Length = fileInfo.Length;

            // Image?
            var (width, height) = getImageSizes(fullFilename);
            if (width > 0 && height > 0)
            {
                doc.ImageWidth = width;
                doc.ImageHeight = height;
            }

            // Save to database
            _context.Documents.Add(doc);

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return doc;
        }

        public Document TryReadDocument(Guid GUID, out FileStream stream)
        {
            stream = null;

            var doc = _context.Documents.Find(GUID);
            if (doc == null)
                return null;


            var fullFilename = Path.Combine(_storageOptions.StoragePath, doc.GUID.ToString());

            if (!File.Exists(fullFilename))
                return null;


            stream = File.OpenRead(fullFilename);

            return doc;
        }

        public void Delete(Guid GUID)
        {
            // Delete from storage
            var fullFilename = Path.Combine(_storageOptions.StoragePath, GUID.ToString());
            if (File.Exists(fullFilename))
            {
                try {
                    File.Delete(fullFilename);
                }
                catch { }
            }

            // Delete from db
            var doc = _context.Documents.Find(GUID);
            if (doc != null)
            {
                _context.Documents.Remove(doc);
                _context.SaveChanges();
            }
        }

        private (int Width, int Height) getImageSizes(string filename)
        {
            var sizes = (Width: 0, Height: 0);

            try
            {
                System.Drawing.Image img = System.Drawing.Image.FromFile(filename);
                sizes.Width = img.Width;
                sizes.Height = img.Height;
            }
            catch { }

            return sizes;
        }
    }
}
