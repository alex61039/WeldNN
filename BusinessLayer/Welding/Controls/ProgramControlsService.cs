using BusinessLayer.Models;
using BusinessLayer.Models.Configuration;
using BusinessLayer.Models.WeldingMachine;
using BusinessLayer.Welding.Configuration;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Controls
{
    public class ProgramControlsService
    {
        WeldingContext _context;

        public ProgramControlsService(WeldingContext context)
        {
            _context = context;
        }

        public Dictionary<string, ProgramControlItemValue> LoadWeldingMachineProgramValues(int WeldingLimitProgramID)
        {
            // Load values from database
            var dict = _context.WeldingLimitProgramParameters.Where(pp => pp.WeldingLimitProgramID == WeldingLimitProgramID)
                .ToDictionary(
                    pp => pp.PropertyCode,
                    pp => new ProgramControlItemValue
                    {
                        ID = pp.PropertyCode,
                        Value = pp.Value,
                        MinValue = Double.TryParse(pp.MinValue, out double v1) ? v1 : 0,
                        MaxValue = Double.TryParse(pp.MaxValue, out double v2) ? v2 : 0
                    }
                 );

            return dict;
        }

        public void SaveWeldingMachineDefaultProgramValues(
            int WeldingMachineID,
            Dictionary<string, ProgramControlItemValue> Values,
            int userID,
            int WeldingMaterialID
            )
        {
            var defaultProgram = GetDefaultWeldingMachineProgram(WeldingMachineID);

            if (defaultProgram == null)
            {
                // Create in database
                defaultProgram = new WeldingLimitProgram
                {
                    Status = (int)GeneralStatus.Active,
                    DateCreated = DateTime.Now,
                    Name = "",
                    CreatedUserID = userID,
                    WeldingMachineID = WeldingMachineID,
                    IsMachineDefault = true,
                    ProgramStatus = (int)GeneralStatus.Active,
                    WeldingMaterialID = WeldingMaterialID
                };

                _context.WeldingLimitPrograms.Add(defaultProgram);
                _context.SaveChanges();
            }
            else
            {
                // Set updated by
                defaultProgram.DateUpdated = DateTime.Now;
                defaultProgram.UpdatedUserID = userID;
                defaultProgram.WeldingMaterialID = WeldingMaterialID;
                _context.SaveChanges();
            }


            // Delete current values
            _context.WeldingLimitProgramParameters.RemoveRange(
                _context.WeldingLimitProgramParameters.Where(pp => pp.WeldingLimitProgramID == defaultProgram.ID)
                );

            // Insert values
            _context.WeldingLimitProgramParameters.AddRange(
                Values.Select(v => new WeldingLimitProgramParameter
                {
                    WeldingLimitProgramID = defaultProgram.ID,
                    PropertyCode = v.Key,
                    Value = v.Value.Value,
                    MinValue = v.Value.MinValue.ToString(),
                    MaxValue = v.Value.MaxValue.ToString()
                })
            );

            _context.SaveChanges();

            // =======================================================================================
            // Duplicate to history
            var programHistory = new WeldingLimitProgramHistory
            {
                Status = (int)GeneralStatus.Active,
                DateCreated = DateTime.Now,
                Name = "",
                CreatedUserID = userID,
                WeldingMachineID = WeldingMachineID,
                IsMachineDefault = true

            };

            _context.WeldingLimitProgramHistories.Add(programHistory);
            _context.SaveChanges();

            // Duplicate parameters to history
            _context.WeldingLimitProgramHistoryParameters.AddRange(
                Values.Select(v => new WeldingLimitProgramHistoryParameter
                {
                    WeldingLimitProgramHistoryID = programHistory.ID,
                    PropertyCode = v.Key,
                    Value = v.Value.Value,
                    MinValue = v.Value.MinValue.ToString(),
                    MaxValue = v.Value.MaxValue.ToString()
                })
                );
            _context.SaveChanges();
        }


        public void SaveWeldingMachineProgramValues(
            int WeldingLimitProgramID,
            Dictionary<string, ProgramControlItemValue> Values,
            int userID,
            int WeldingMaterialID)
        {
            var program = GetWeldingMachineProgram(WeldingLimitProgramID);

            if (program == null)
                return;

            // Set updated by
            program.DateUpdated = DateTime.Now;
            program.UpdatedUserID = userID;
            program.WeldingMaterialID = WeldingMaterialID;
            _context.SaveChanges();


            // Delete current values
            _context.WeldingLimitProgramParameters.RemoveRange(
                _context.WeldingLimitProgramParameters.Where(pp => pp.WeldingLimitProgramID == program.ID)
                );

            // Insert values
            _context.WeldingLimitProgramParameters.AddRange(
                Values.Select(v => new WeldingLimitProgramParameter
                {
                    WeldingLimitProgramID = program.ID,
                    PropertyCode = v.Key,
                    Value = v.Value.Value,
                    MinValue = v.Value.MinValue.ToString(),
                    MaxValue = v.Value.MaxValue.ToString()
                })
            );

            _context.SaveChanges();

            // =======================================================================================
            // Duplicate to history
            var programHistory = new WeldingLimitProgramHistory
            {
                Status = (int)GeneralStatus.Active,
                DateCreated = DateTime.Now,
                Name = "",
                CreatedUserID = userID,
                WeldingMachineID = program.WeldingMachineID,
                WeldingMachineTypeID = program.WeldingMachineTypeID,
                IsMachineDefault = true

            };

            _context.WeldingLimitProgramHistories.Add(programHistory);
            _context.SaveChanges();

            // Duplicate parameters to history
            _context.WeldingLimitProgramHistoryParameters.AddRange(
                Values.Select(v => new WeldingLimitProgramHistoryParameter
                {
                    WeldingLimitProgramHistoryID = programHistory.ID,
                    PropertyCode = v.Key,
                    Value = v.Value.Value,
                    MinValue = v.Value.MinValue.ToString(),
                    MaxValue = v.Value.MaxValue.ToString()
                })
                );
            _context.SaveChanges();
        }


        public int GetCurrentWeldingMachineProgramID(int WeldingMachineID)
        {
            int programID = 0;

            // By barcode?
            var scan = "";
            try
            {
                var stateService = new Machine.MachineStateService(_context);
                var currentState = stateService.GetCurrentWeldingMachineState(WeldingMachineID);
                if (currentState != null 
                    && (currentState.Status == WeldingMachineStatus.Ready || currentState.Status == WeldingMachineStatus.Working || currentState.Status == WeldingMachineStatus.Error))
                {
                    scan = currentState.GetRawValue(PropertyCodes.Barcode);

                    if (!string.IsNullOrEmpty(scan))
                    {
                        var machine = _context.WeldingMachines.Find(WeldingMachineID);

                        // Попробовать найти режим по заданному штрихкоду
                        var tmp_program = _context.WeldingLimitPrograms.Where(p => p.Status == (int)GeneralStatus.Active
                            && p.ProgramStatus == (int)GeneralStatus.Active
                            && p.WeldingMachineTypeID == machine.WeldingMachineTypeID
                            && p.ByBarcode == true
                            && p.Barcode == scan)
                            .FirstOrDefault();

                        if (tmp_program != null)
                        {
                            return tmp_program.ID;
                        }


                        // Найти режим по автоматическому штрихкоду
                        if (Utils.ScancodesHelper.TryParse(scan, out ScancodeEntity entity))
                        {
                            if (entity.Entity == "WeldingLimitProgram" && !String.IsNullOrEmpty(entity.ID))
                            {
                                var int_entity_id = Convert.ToInt32(entity.ID);

                                tmp_program = _context.WeldingLimitPrograms.Where(p => p.Status == (int)GeneralStatus.Active
                                    && p.ProgramStatus == (int)GeneralStatus.Active
                                    && p.WeldingMachineTypeID == machine.WeldingMachineTypeID
                                    && p.ByBarcode == true
                                    && p.ID == int_entity_id)
                                    .FirstOrDefault();

                                if (tmp_program != null)
                                {
                                    return tmp_program.ID;
                                }
                            }
                        }

                    }


                }
            }
            catch { }


            try
            {
                var result = _context.GetCurrentWeldingLimitProgram(WeldingMachineID);

                programID = result.First().Value;
            }
            catch (Exception ex) {
                // throw ex;
            }

            if (programID <= 0)
            {
                var program = GetDefaultWeldingMachineProgram(WeldingMachineID);
                if (program != null)
                    programID = program.ID;
            }

            return programID;
        }

        // ===========================================================================================================
        public WeldingLimitProgram GetDefaultWeldingMachineProgram(int WeldingMachineID)
        {
            var program = _context.WeldingLimitPrograms.FirstOrDefault(p => p.WeldingMachineID == WeldingMachineID && p.IsMachineDefault && p.Status == (int)GeneralStatus.Active);

            return program;
        }

        public WeldingLimitProgram GetWeldingMachineProgram(int WeldingLimitProgramID)
        {
            var program = _context.WeldingLimitPrograms.FirstOrDefault(p => p.ID == WeldingLimitProgramID && p.Status == (int)GeneralStatus.Active);

            return program;
        }


    }
}
