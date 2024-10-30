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

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class WeldingMaterialsController : ControllerBaseAuthenticated
    {
        public WeldingMaterialsController(WeldingContext context, Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
        }

        // ==============================================================================================
        [HttpGet]
        public APIResponse2<ICollection<WeldingMaterial>> List()
        {
            //if (!HasAccess("WeldingMaterials", PermissionAccess.Read))
            //    return new APIResponse2<ICollection<WeldingMaterial>>(403, "No access");

            var statuses = new int[] { (int)GeneralStatus.Active };

            var list = _context.WeldingMaterials.Where(m => statuses.Contains(m.Status)).ToList();

            // Update names
            list.ForEach(setMaterialFullName);

            return new APIResponse2<ICollection<WeldingMaterial>>(list);
        }

        [HttpGet]
        public APIResponse2<WeldingMaterial> Get(int id)
        {
            //if (!HasAccess("WeldingMaterials", PermissionAccess.Read))
            //    return new APIResponse2<WeldingMaterial>(403, "No access");

            var item = _context.WeldingMaterials.Where(m => m.ID == id && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
            if (item == null)
                return new APIResponse2<WeldingMaterial>(404, "Not found");

            return new APIResponse2<WeldingMaterial>(item);
        }

        [HttpPost]
        public APIResponse2<WeldingMaterial> Save([FromBody] WeldingMaterial item)
        {
            if (!HasAccess("WeldingMaterials", UserPermissionAccess.Write))
                return new APIResponse2<WeldingMaterial>(403, "No access");

            // Validate

            // Check name - maybe people with same name
            if (_context.WeldingMaterials.Any(m => m.Status == (int)GeneralStatus.Active && m.Name == item.Name && m.ID != item.ID))
            {
                return new APIResponse2<WeldingMaterial>(2101, "Name already exists");
            }


            // Load or create new
            WeldingMaterial _item;
            if (item.ID > 0)
            {
                _item = _context.WeldingMaterials.Where(m => m.ID == item.ID && m.Status == (int)GeneralStatus.Active).FirstOrDefault();
                if (_item == null)
                    return new APIResponse2<WeldingMaterial>(404, "Not found");
            }
            else
            {
                _item = new WeldingMaterial
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now
                };

                _context.WeldingMaterials.Add(_item);
            }

            // Update properties
            _item.WeldingMaterialTypeID = item.WeldingMaterialTypeID;
            _item.Name = item.Name;
            _item.Brand = item.Brand;
            _item.Model = item.Model;
            _item.Description = item.Description;
            _item.Diameter_mm = item.Diameter_mm;
            _item.WeightBlock_kg = item.WeightBlock_kg;
            _item.LengthBlock_m = item.LengthBlock_m;
            _item.WeightPerMeter_kg = item.WeightPerMeter_kg;
            _item.Composition = item.Composition;
            _item.LengthItem_mm = item.LengthItem_mm;
            _item.WeightItem_kg = item.WeightItem_kg;
            _item.QuantityInBlock = item.QuantityInBlock;
            _item.Category = item.Category;
            _item.Sizes = item.Sizes;
            _item.Thickness_mm = item.Thickness_mm;
            _item.k0 = item.k0;
            _item.k1 = item.k1;
            _item.k2 = item.k2;
            _item.limit_upper = item.limit_upper;
            _item.limit_lower = item.limit_lower;



            // Save
            _context.SaveChanges();

            return new APIResponse2<WeldingMaterial>(_item);
        }

        [HttpDelete]
        public APIResponse Delete(int ID)
        {
            if (!HasAccess("WeldingMaterials", UserPermissionAccess.Write))
                return new APIResponse(403, "No access");

            var wmt = _context.WeldingMaterials.Find(ID);
            if (wmt == null) {
                return new APIResponse(404, "Not found");
            }

            // Has linked ...
            //if (
            //    _context.Organizations.Any(m=> m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
            //    || _context.OrganizationUnits.Any(m => m.Status == (int)GeneralStatus.Active && m.HeadUserId == ID)
            //    )
            //{
            //    return new APIResponse(2100, "Cannot delete, objects attached.");
            //}

            // Do delete
            wmt.Status = (int)GeneralStatus.Deleted;
            _context.SaveChanges();

            return new APIResponse(null);
        }

        private void setMaterialFullName(WeldingMaterial wm)
        {
            if (wm.WeldingMaterialTypeID == (int)WeldingMaterialTypeEnum.Wire)
                wm.Name = String.Format("{0}, {1}mm", wm.Name, wm.Diameter_mm);

            if (wm.WeldingMaterialTypeID == (int)WeldingMaterialTypeEnum.Electrode)
                wm.Name = String.Format("{0}, D={1}mm", wm.Name, wm.Diameter_mm);
        }
    }
}
