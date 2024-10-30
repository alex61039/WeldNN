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
using BusinessLayer.Welding.Configuration;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class WeldingMachineTypesController : ControllerBaseAuthenticated
    {
        public WeldingMachineTypesController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<WeldingMachineType>> List()
        {
            var list = _context.WeldingMachineTypes
                .Where(m => m.Status == (int)GeneralStatus.Active)
                .OrderBy(m => m.Name)
                .ToList();

            // Убрать лишнее, кто не имеет доступа
            if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Read))
            {
                list.ForEach(t => {
                    t.ConfigurationJSON = null;
                });
            }

            return new APIResponse2<ICollection<WeldingMachineType>>(list);
        }


        [HttpGet]
        public APIResponse2<WeldingMachineType> Get(int id)
        {
            // if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Read))
            //    return new APIResponse2<WeldingMachineType>(403, "No access");

            var item = _context.WeldingMachineTypes.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingMachineType>(404, "Not found");

            // Убрать лишнее, кто не имеет доступа
            if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Read))
            {
                item.ConfigurationJSON = null;
            }

            return new APIResponse2<WeldingMachineType>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingMachineType> CopyFrom([FromQuery] int WeldingMachineTypeID, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Write))
                return new APIResponse2<WeldingMachineType>(403, "No access");

            var item = _context.WeldingMachineTypes.Where(m => m.ID == WeldingMachineTypeID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingMachineType>(404, "Not found");

            // Reset ID, Name
            item.ID = 0;
            item.Name = "";

            // Copy images
            if (item.Photo.HasValue)
            {
                try
                {
                    item.Photo = documentsService.Copy(item.Photo.Value).GUID;
                }
                catch { }
            }

            if (item.PanelPhoto.HasValue)
            {
                try
                {
                    item.PanelPhoto = documentsService.Copy(item.PanelPhoto.Value).GUID;
                }
                catch { }
            }

            return new APIResponse2<WeldingMachineType>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingMachineType> Save([FromBody] WeldingMachineType wmt, [FromServices] IDocumentsService documentsService)
        {
            if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Write))
                return new APIResponse2<WeldingMachineType>(403, "No access");

            // Validate

            // Check name
            if (_context.WeldingMachineTypes.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == wmt.Name && m.ID != wmt.ID))
            {
                return new APIResponse2<WeldingMachineType>(2101, "Name already exists");
            }

            // Check JSON
            var validJSON = BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader.ValidateJSON(wmt.ConfigurationJSON, true);
            if (!validJSON)
            {
                return new APIResponse2<WeldingMachineType>(3101, "Invalid configuration");
            }


            // Load or create new
            WeldingMachineType _wmt;
            if (wmt.ID > 0)
            {
                _wmt = _context.WeldingMachineTypes.Where(m => m.ID == wmt.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_wmt == null)
                    return new APIResponse2<WeldingMachineType>(404, "Not found");
            }
            else
            {
                _wmt = new WeldingMachineType {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.WeldingMachineTypes.Add(_wmt);
            }

            // var documentsManager = new DocumentsManager(_context, _storageOptions);

            // Update proerties
            _wmt.Name = wmt.Name;
            _wmt.ConfigurationJSON = wmt.ConfigurationJSON;

            // Photo
            if (wmt.Photo != _wmt.Photo)
            {
                // Delete previous
                if (_wmt.Photo.HasValue)
                {
                    documentsService.Delete(_wmt.Photo.Value);
                }

                _wmt.Photo = wmt.Photo;
            }

            // Panel
            if (wmt.PanelPhoto != _wmt.PanelPhoto)
            {
                // Delete previous
                if (_wmt.PanelPhoto.HasValue)
                {
                    documentsService.Delete(_wmt.PanelPhoto.Value);
                }

                _wmt.PanelPhoto = wmt.PanelPhoto;
            }

            // Save
            _context.SaveChanges();

            // Save update
            ObjectUpdaterService.ObjectUpdated(_context, ObjectUpdaterService.ALL);

            return new APIResponse2<WeldingMachineType>(_wmt);
        }

        public class ValidateConfigurationRequest
        {
            public string json { get; set; }
        }

        [HttpPost]
        public APIResponse ValidateConfiguration([FromBody] ValidateConfigurationRequest req)
        {
            var valid = BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader.ValidateJSON(req.json, true);

            if (!valid)
                return new APIResponse(400, "Bad request");

            return new APIResponse(null);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingMachineTypes", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingMachineTypes.Find(ID);
            if (wmt == null) {
                return new APIResponse(404, "Not found");
            }

            // Has linked Machines?
            if (_context.WeldingMachines.Any(m=> m.Status == (int)GeneralStatus.Active && m.WeldingMachineTypeID == ID))
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

        // ===============================================================================
        public class VisibleProperties : Dictionary<int, VisiblePropertiesSet> { }
        public class VisiblePropertiesSet : Dictionary<string, string> { }
        [HttpGet]
        public APIResponse2<VisibleProperties> ListVisibleProperties(bool? includeNotShowInSummary = false)
        {
            var result = new VisibleProperties();

            var confLoader = new WeldingMachineTypeConfigurationLoader(_context);

            // iterate by all machine types
            var types = _context.WeldingMachineTypes
                .Where(m => m.Status == (int)GeneralStatus.Active);

            foreach (var type in types)
            {
                // Load config
                var conf = confLoader.LoadByType(type.ID);
                if (conf == null)
                    continue;

                // Build list of visible Properties (with ShowInSummary)
                var panelStateBuilder = new BusinessLayer.Welding.Panel.PanelStateBuilder(conf, _context);

                var summaryProps = panelStateBuilder.BuildSummaryProperties(null, includeNotShowInSummary.GetValueOrDefault());
                if (summaryProps == null)
                    continue;

                var set = new VisiblePropertiesSet();

                summaryProps.ForEach(p =>
                {
                    if (!set.ContainsKey(p.PropertyCode))
                    {
                        set.Add(
                            p.PropertyCode,
                            // String.Format("{0} ({1})", p.Title, p.PropertyCode)
                            String.Format("{0}", p.Title)
                            );
                    }
                });

                result.Add(type.ID, set);
            }

            return new APIResponse2<VisibleProperties>(result);
        }
    }
}
