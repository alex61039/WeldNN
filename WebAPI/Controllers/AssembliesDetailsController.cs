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
using BusinessLayer.Models;
using Microsoft.Extensions.Options;
using BusinessLayer.Interfaces.Storage;
using System.Data.Entity;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class AssembliesDetailsController : ControllerBaseAuthenticated
    {
        public AssembliesDetailsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<DetailPartType>> DetailPartTypeList()
        {
            //if (!HasAccess("DetailPartTypes", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailPartType>>(403, "No access");

            return new APIResponse2<ICollection<DetailPartType>>(_context.DetailPartTypes.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }

        [HttpGet]
        public APIResponse2<ICollection<DetailAssemblyType>> DetailAssemblyTypeList()
        {
            //if (!HasAccess("DetailAssemblyTypes", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailAssemblyType>>(403, "No access");

            return new APIResponse2<ICollection<DetailAssemblyType>>(_context.DetailAssemblyTypes.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }

        [HttpGet]
        public APIResponse2<ICollection<DetailPart>> DetailPartList()
        {
            //if (!HasAccess("DetailParts", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailPart>>(403, "No access");

            return new APIResponse2<ICollection<DetailPart>>(_context.DetailParts.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }

        [HttpGet]
        public APIResponse2<ICollection<DetailPart>> DetailPartListNotInUse(int DetailAssemblyID)
        {
            //if (!HasAccess("DetailParts", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailPart>>(403, "No access");

            // все детали, не участвующие в Сборочных единицах, кроме текущей DetailAssemblyID
            var allActiveDetailAssemblies_ExcludeCurrent = _context.DetailAssemblies
                .Where(m => m.Status == (int)GeneralStatus.Active && m.ID != DetailAssemblyID)
                .Include(da => da.DetailParts);

            var currentDetailAssembly = _context.DetailAssemblies.Where(da => da.ID == DetailAssemblyID)
                .Include(da => da.DetailParts);

            var query = _context.DetailParts
                .Where(dp => dp.Status == (int)GeneralStatus.Active
                    && (
                        !allActiveDetailAssemblies_ExcludeCurrent.Any(da => da.DetailParts.Any(dadp => dadp.ID == dp.ID))
                        || currentDetailAssembly.Any(da => da.DetailParts.Any(dadp => dadp.ID == dp.ID))
                        )
                    );

            var list = query.ToList();

            return new APIResponse2<ICollection<DetailPart>>(list);
        }

        [HttpGet]
        public APIResponse2<ICollection<DetailAssembly>> DetailAssemblyList()
        {
            //if (!HasAccess("DetailAssemblies", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailAssembly>>(403, "No access");

            return new APIResponse2<ICollection<DetailAssembly>>(_context.DetailAssemblies.Where(m => m.Status == (int)GeneralStatus.Active).ToList());
        }

        [HttpGet]
        public APIResponse2<ICollection<DetailAssembly>> DetailAssemblyListByStatus(int DetailAssemblyStatus)
        {
            //if (!HasAccess("DetailAssemblies", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<DetailAssembly>>(403, "No access");

            return new APIResponse2<ICollection<DetailAssembly>>(
                _context.DetailAssemblies
                .Where(m => m.Status == (int)GeneralStatus.Active && m.DetailAssemblyStatus == DetailAssemblyStatus)
                .ToList()
                );
        }


        // ==============================================================================================
        [HttpGet]
        public APIResponse2<DetailPartType> DetailPartTypeGet(int id)
        {
            //if (!HasAccess("DetailPartTypes", PermissionAccess.Read))
            //    return new APIResponse2<DetailPartType>(403, "No access");

            var item = _context.DetailPartTypes.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<DetailPartType>(404, "Not found");

            return new APIResponse2<DetailPartType>(item);
        }

        [HttpGet]
        public APIResponse2<DetailAssemblyType> DetailAssemblyTypeGet(int id)
        {
            //if (!HasAccess("DetailAssemblyTypes", PermissionAccess.Read))
            //    return new APIResponse2<DetailAssemblyType>(403, "No access");

            var item = _context.DetailAssemblyTypes
                .Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active)
                .Include(dat => dat.DetailPartTypes)
                .FirstOrDefault();
            if (item == null)
                return new APIResponse2<DetailAssemblyType>(404, "Not found");

            // item.DetailPartTypes = _context.

            return new APIResponse2<DetailAssemblyType>(item);
        }

        [HttpGet]
        public APIResponse2<DetailPart> DetailPartGet(int id)
        {
            //if (!HasAccess("DetailParts", PermissionAccess.Read))
            //    return new APIResponse2<DetailPart>(403, "No access");

            var item = _context.DetailParts.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<DetailPart>(404, "Not found");

            return new APIResponse2<DetailPart>(item);
        }

        [HttpGet]
        public APIResponse2<DetailAssembly> DetailAssemblyGet(int id)
        {
            //if (!HasAccess("DetailAssemblies", PermissionAccess.Read))
            //    return new APIResponse2<DetailAssembly>(403, "No access");

            var item = _context.DetailAssemblies
                .Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active)
                .Include(da => da.DetailParts)
                .FirstOrDefault();
            if (item == null)
                return new APIResponse2<DetailAssembly>(404, "Not found");

            return new APIResponse2<DetailAssembly>(item);
        }

        // ==============================================================================================
        [HttpPost]
        public APIResponse2<DetailPartType> DetailPartTypeSave([FromBody] DetailPartType item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("DetailPartTypes", UserPermissionAccess.Write))
                return new APIResponse2<DetailPartType>(403, "No access");

            // Validate

            // Check name
            if (_context.DetailPartTypes.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<DetailPartType>(2101, "Name already exists");
            }


            // Load or create new
            DetailPartType _item;
            if (item.ID > 0)
            {
                _item = _context.DetailPartTypes.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<DetailPartType>(404, "Not found");
            }
            else
            {
                _item = new DetailPartType
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.DetailPartTypes.Add(_item);
            }

            // Update properties
            _item.Name = item.Name;
            _item.Label = item.Label;
            _item.Description = item.Description;

            // Image
            if (item.Image != _item.Image)
            {
                // Delete previous
                if (_item.Image.HasValue)
                {
                    documentsService.Delete(_item.Image.Value);
                }

                _item.Image = item.Image;
            }

            // Scheme Image
            if (item.SchemeImage != _item.SchemeImage)
            {
                // Delete previous
                if (_item.SchemeImage.HasValue)
                {
                    documentsService.Delete(_item.SchemeImage.Value);
                }

                _item.SchemeImage = item.SchemeImage;
            }


            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<DetailPartType>(_item);
        }

        [HttpPost]
        public APIResponse2<DetailAssemblyType> DetailAssemblyTypeSave([FromBody] DetailAssemblyType item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("DetailAssemblyTypes", UserPermissionAccess.Write))
                return new APIResponse2<DetailAssemblyType>(403, "No access");

            // Validate

            // Check name
            if (_context.DetailAssemblyTypes.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<DetailAssemblyType>(2101, "Name already exists");
            }


            // Load or create new
            DetailAssemblyType _item;
            if (item.ID > 0)
            {
                _item = _context.DetailAssemblyTypes
                    .Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active)
                    .Include(dat => dat.DetailPartTypes)
                    .FirstOrDefault();

                if (_item == null)
                    return new APIResponse2<DetailAssemblyType>(404, "Not found");
            }
            else
            {
                _item = new DetailAssemblyType
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.DetailAssemblyTypes.Add(_item);
            }

            // Update properties
            _item.Name = item.Name;
            _item.Label = item.Label;
            _item.Description = item.Description;

            // DetailPartTypes
            var ids_arr = item.DetailPartTypes.Select(dpt => dpt.ID).ToArray();
            _item.DetailPartTypes = _context.DetailPartTypes.Where(dpt => ids_arr.Contains(dpt.ID)).ToList();

            // Image
            if (item.Image != _item.Image)
            {
                // Delete previous
                if (_item.Image.HasValue)
                {
                    documentsService.Delete(_item.Image.Value);
                }

                _item.Image = item.Image;
            }

            // Scheme Image
            if (item.SchemeImage != _item.SchemeImage)
            {
                // Delete previous
                if (_item.SchemeImage.HasValue)
                {
                    documentsService.Delete(_item.SchemeImage.Value);
                }

                _item.SchemeImage = item.SchemeImage;
            }

            // Specification Image
            if (item.SpecsImage != _item.SpecsImage)
            {
                // Delete previous
                if (_item.SpecsImage.HasValue)
                {
                    documentsService.Delete(_item.SpecsImage.Value);
                }

                _item.SpecsImage = item.SpecsImage;
            }

            // Route Image
            if (item.RouteImage != _item.RouteImage)
            {
                // Delete previous
                if (_item.RouteImage.HasValue)
                {
                    documentsService.Delete(_item.RouteImage.Value);
                }

                _item.RouteImage = item.RouteImage;
            }


            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<DetailAssemblyType>(_item);
        }

        [HttpPost]
        public APIResponse2<DetailPart> DetailPartSave([FromBody] DetailPart item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("DetailParts", UserPermissionAccess.Write))
                return new APIResponse2<DetailPart>(403, "No access");

            // Validate

            // Check Serial number
            if (_context.DetailParts.Any(m => m.Status == (int)GeneralStatus.Active && m.SerialNumber == item.SerialNumber && m.ID != item.ID))
            {
                return new APIResponse2<DetailPart>(2104, "Serial number already exists");
            }


            // Load or create new
            DetailPart _item;
            if (item.ID > 0)
            {
                _item = _context.DetailParts.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<DetailPart>(404, "Not found");
            }
            else
            {
                _item = new DetailPart
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.DetailParts.Add(_item);
            }

            // Update properties
            _item.DetailPartTypeID = item.DetailPartTypeID;
            _item.SerialNumber = item.SerialNumber;
            _item.Description = item.Description;


            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<DetailPart>(_item);
        }

        [HttpPost]
        public APIResponse2<DetailAssembly> DetailAssemblySave([FromBody] DetailAssembly item, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("DetailAssemblies", UserPermissionAccess.Write))
                return new APIResponse2<DetailAssembly>(403, "No access");

            // Validate

            // Check Serial number
            if (_context.DetailAssemblies.Any(m => m.Status == (int)GeneralStatus.Active && m.SerialNumber == item.SerialNumber && m.ID != item.ID))
            {
                return new APIResponse2<DetailAssembly>(2104, "Serial number already exists");
            }


            // Load or create new
            DetailAssembly _item;
            if (item.ID > 0)
            {
                _item = _context.DetailAssemblies
                    .Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active)
                    .Include(da => da.DetailParts)
                    .FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<DetailAssembly>(404, "Not found");
            }
            else
            {
                _item = new DetailAssembly
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    DetailAssemblyStatus = (int)DetailAssemblyStatus.InProcess
                };

                _context.DetailAssemblies.Add(_item);
            }

            // Update properties
            _item.DetailAssemblyTypeID = item.DetailAssemblyTypeID;
            _item.SerialNumber = item.SerialNumber;
            _item.Description = item.Description;
            _item.DetailAssemblyStatus = item.DetailAssemblyStatus.GetValueOrDefault(0); // 0 by default

            // DetailParts
            var ids_arr = item.DetailParts.Select(dp => dp.ID).ToArray();
            _item.DetailParts = _context.DetailParts.Where(dp => ids_arr.Contains(dp.ID)).ToList();


            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<DetailAssembly>(_item);
        }

        // ==============================================================================================
        [HttpDelete]
        public APIResponse DetailPartTypeDelete(int ID)
        {
            if (!HasAccess("DetailPartTypes", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.DetailPartTypes.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked objects?
            if (_context.DetailParts.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailPartTypeID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }
            else if (_context.DetailAssemblyTypes.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailPartTypes.Any(dpt => dpt.ID == ID)))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }

        [HttpDelete]
        public APIResponse DetailAssemblyTypeDelete(int ID)
        {
            if (!HasAccess("DetailAssemblyTypes", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.DetailAssemblyTypes.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked objects?
            if (_context.DetailAssemblies.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailAssemblyTypeID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }

        [HttpDelete]
        public APIResponse DetailPartDelete(int ID)
        {
            if (!HasAccess("DetailParts", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.DetailParts.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked objects?
            if (_context.DetailAssemblies.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailParts.Any(dp => dp.ID == ID)))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }
            else if (_context.WeldingAssemblyInstructions.Any(m => m.Status == (int)GeneralStatus.Active && m.WeldingDetailAssemblyTypeID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }

        [HttpDelete]
        public APIResponse DetailAssemblyDelete(int ID)
        {
            if (!HasAccess("DetailAssemblies", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.DetailAssemblies.Find(ID);
            if (wmt == null)
            {
                return new APIResponse(404, "Not found");
            }

            // Has linked objects?
            if (_context.DetailAssemblies.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailAssemblyTypeID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }
            if (_context.WeldingDetailAssemblyTasks.Any(m => m.Status == (int)GeneralStatus.Active && m.DetailAssemblyID == ID))
            {
                return new APIResponse(2100, "Cannot delete, objects attached.");
            }

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse(null);
        }

    }
}
