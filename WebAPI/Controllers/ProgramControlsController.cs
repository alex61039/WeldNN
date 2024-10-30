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
using BusinessLayer.Welding.Machine;
using BusinessLayer.Welding.Controls;
using BusinessLayer.Models.WeldingMachine;

namespace WebAPI.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]/[action]")]
    [ApiController]
    public class ProgramControlsController : ControllerBaseAuthenticated
    {
        ProgramControlsService _programControlsService;

        public ProgramControlsController(WeldingContext context,
            ProgramControlsService programControlsService,
            Microsoft.Extensions.Configuration.IConfiguration Configuration) : base(context, Configuration)
        {
            _programControlsService = programControlsService;
        }

        [HttpGet]
        public APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls> GetWeldingMachineDefaultControls(int WeldingMachineID)
        {
            // Load configuration
            var configurationLoader = new BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader(_context);

            var configuraion = configurationLoader.LoadByMachine(WeldingMachineID);
            if (configuraion == null) {
                return new APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls>(404, "");
            }

            var programControlsBuilder = new ProgramControlsBuilder(configuraion);
            var programControls = programControlsBuilder.BuildDefault();

            // Default program?
            var program = _programControlsService.GetDefaultWeldingMachineProgram(WeldingMachineID);
            if (program != null)
            {
                programControls.WeldingMaterialID = program.WeldingMaterialID.GetValueOrDefault();

                programControls.WeldingMachineTypeID = program.WeldingMachineTypeID.GetValueOrDefault();
                if (!program.WeldingMachineTypeID.HasValue)
                {
                    programControls.WeldingMachineTypeID = _context.WeldingMachines.Find(program.WeldingMachineID.GetValueOrDefault()).WeldingMachineTypeID;
                }
            }


            return new APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls>(programControls);
        }

        [HttpGet]
        public APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls> GetProgramControls(int WeldingLimitProgramID)
        {
            // Load configuration
            var configurationLoader = new BusinessLayer.Welding.Configuration.WeldingMachineTypeConfigurationLoader(_context);

            // Get program
            var program = _context.WeldingLimitPrograms.Find(WeldingLimitProgramID);
            if (program == null || program.Status != (int)GeneralStatus.Active)
            {
                return new APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls>(404, "Not found");
            }

            // Fetch type
            int weldingMachineTypeID = program.WeldingMachineTypeID.GetValueOrDefault();
            if (!program.WeldingMachineTypeID.HasValue)
            {
                weldingMachineTypeID = _context.WeldingMachines.Find(program.WeldingMachineID.GetValueOrDefault()).WeldingMachineTypeID;
            }

            // Load by Type
            var configuraion = configurationLoader.LoadByType(weldingMachineTypeID);
            if (configuraion == null)
            {
                return new APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls>(404, "");
            }

            var programControlsBuilder = new ProgramControlsBuilder(configuraion);
            var programControls = programControlsBuilder.BuildDefault();

            programControls.WeldingMaterialID = program.WeldingMaterialID.GetValueOrDefault();
            programControls.WeldingMachineTypeID = weldingMachineTypeID;

            return new APIResponse2<BusinessLayer.Models.WeldingMachine.ProgramControls>(programControls);
        }

        [HttpGet]
        public APIResponse2<Dictionary<string, ProgramControlItemValue>> LoadWeldingMachineDefaultProgramValues(
            [FromServices] ProgramControlsService programControlsService,
            [FromQuery] int WeldingMachineID
            )
        {
            var defaultProgram = programControlsService.GetDefaultWeldingMachineProgram(WeldingMachineID);
            if (defaultProgram == null)
                return new APIResponse2<Dictionary<string, ProgramControlItemValue>>(404, "");


            var values = programControlsService.LoadWeldingMachineProgramValues(defaultProgram.ID);

            return new APIResponse2<Dictionary<string, ProgramControlItemValue>>(values);
        }


        [HttpGet]
        public APIResponse2<Dictionary<string, ProgramControlItemValue>> LoadWeldingMachineProgramValues(
            [FromServices] ProgramControlsService programControlsService,
            [FromQuery] int WeldingLimitProgramID
            )
        {
            var values = programControlsService.LoadWeldingMachineProgramValues(WeldingLimitProgramID);

            return new APIResponse2<Dictionary<string, ProgramControlItemValue>>(values);
        }


        public class SaveWeldingMachineDefaultRequest
        {
            public int? WeldingMachineID { get; set; }

            public int? WeldingLimitProgramID { get; set; }

            public Dictionary<string, ProgramControlItemValue> Values { get; set; }

            public int WeldingMaterialID { get; set; }
        }

        [HttpPost]
        public APIResponse SaveWeldingMachineProgramValues(
            [FromServices] ProgramControlsService programControlsService,
            [FromBody] SaveWeldingMachineDefaultRequest req)
        {
            if (req.WeldingMachineID.HasValue)
            {
                // Save Default program 
                programControlsService.SaveWeldingMachineDefaultProgramValues(
                    req.WeldingMachineID.Value,
                    req.Values,
                    _userAccount.ID,
                    req.WeldingMaterialID
                    );

            }
            else
            {
                // Save program
                programControlsService.SaveWeldingMachineProgramValues(
                    req.WeldingLimitProgramID.Value,
                    req.Values,
                    _userAccount.ID,
                    req.WeldingMaterialID
                    );

            }

            return new APIResponse(null);
        }

    }
}
