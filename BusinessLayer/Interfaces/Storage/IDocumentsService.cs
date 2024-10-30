using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Interfaces.Storage
{
    public interface IDocumentsService
    {
        DataLayer.Welding.Document AddDocument(System.IO.Stream stream, string contentType, string filename, int? userID);

        DataLayer.Welding.Document TryReadDocument(Guid GUID, out System.IO.FileStream stream);

        void Delete(Guid GUID);

        DataLayer.Welding.Document Copy(Guid GUID);

    }
}
