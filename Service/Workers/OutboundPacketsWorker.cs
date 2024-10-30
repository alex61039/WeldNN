using BusinessLayer.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WeldingService.Workers
{
    public class WeldingMachineProgramInfo
    {
        public int WeldingMachineID { get; set; }
        public int WeldingLimitProgramID { get; set; }
        public DateTime? LoadedOn { get; set; }
        public DateTime? ProgramUpdated { get; set; }
    }

    public class OutboundPacketsWorker : ServiceHelpers.PeriodicWorker
    {
        private BusinessLayer.Welding.Controls.ProgramControlsService programControlsService;
        private Dictionary<int, WeldingMachineProgramInfo> programInfos;

        protected override string Name => "Messages for machines";

        public OutboundPacketsWorker(int periodSeconds) : base(periodSeconds)
        {
            Action = InternalCheck;

            // Build initial messages
            checkAndReloadAllMachines();
        }

        protected void InternalCheck()
        {
            checkAndReloadAllMachines();
        }



        private void checkAndReloadAllMachines()
        {
            programInfos = new Dictionary<int, WeldingMachineProgramInfo>();

            using (var context = GetWeldingContext())
            {
                programControlsService = new BusinessLayer.Welding.Controls.ProgramControlsService(context);

                foreach (var machine in Domain.DomainObjects.WeldingMachines.Values)
                {
                    // Reload program for the machine
                    try
                    {
                        checkMachineProgram(context, machine);
                    }
                    catch(Exception ex)
                    {
                        Logger.LogException(
                            ex,
                            String.Format("OutboundPacketsWorker.checkAndReloadAllMachines, ID={0}", machine.ID)
                            );
                    }
                }
            }
        }

        private void checkMachineProgram(DataLayer.Welding.WeldingContext context, DataLayer.Welding.WeldingMachine machine)
        {
            WeldingMachineProgramInfo programInfo = null;

            // Optimize?
            /*
            if (programInfos.ContainsKey(machine.ID))
            {
                programInfo = programInfos[machine.ID];
            }
            */

            // Load 'Limit program' or 'Defaut program' for the machine
            programInfo = loadProgramInfo(context, machine);

            // Load latest state
            BusinessLayer.Models.WeldingMachine.StateSummary state = null;
            if (!Domain.MachineStatesRepository.TryGet(machine.MAC, out state))
            {
                var machineStateService = new BusinessLayer.Welding.Machine.MachineStateService(context);
                state = machineStateService.GetCurrentWeldingMachineState(machine.ID);
            }

            if (programInfo != null)
            {
                // Program found
                programInfos[machine.ID] = programInfo;

                // Build packet message by the program
                var message = buildControlMessage(machine, programInfo, state);

                if (!String.IsNullOrEmpty(message))
                {
                    // Set the packet/message
                    var packet = new Models.Packet {
                        MAC = machine.MAC,
                        Data = message
                    };

                    Domain.OutboundPacketsRepository.Set(machine.MAC, packet);
                }
            }
        }

        private string buildControlMessage(
            DataLayer.Welding.WeldingMachine machine, 
            WeldingMachineProgramInfo programInfo, 
            BusinessLayer.Models.WeldingMachine.StateSummary state)
        {
            // Get machine's config
            if (!Domain.DomainObjects.ConfigsByMAC.ContainsKey(machine.MAC))
                return null;

            var config = Domain.DomainObjects.ConfigsByMAC[machine.MAC];

            // Fetch Contol parameter values
            var controlValues = programControlsService.LoadWeldingMachineProgramValues(programInfo.WeldingLimitProgramID);

            // Build and return message
            var machineControlMessageBuilder = new BusinessLayer.Welding.Machine.MachineControlMessageBuilder(config);
            var message = machineControlMessageBuilder.Build(machine, controlValues, state);

            return message;
        }

        private WeldingMachineProgramInfo loadProgramInfo(DataLayer.Welding.WeldingContext context, DataLayer.Welding.WeldingMachine machine)
        {
            // Get active program
            int activeProgramID = 0;

            /*
            var activeLimit = context.WeldingMachineLimits
                .Where(l => l.WeldingMachineID == machine.ID)
                .OrderByDescending(l => l.DateAssigned)
                .Take(1)
                .FirstOrDefault();

            if (activeLimit != null)
                activeProgramID = activeLimit.WeldingLimitProgramID;
                */


            activeProgramID = programControlsService.GetCurrentWeldingMachineProgramID(machine.ID);

            // Logger.Log(LogLevel.Debug, "wedingMachineID = {0}  activeProgramID = {1}", machine.ID, activeProgramID);

            // No active limit, look for default program
            DataLayer.Welding.WeldingLimitProgram limitProgram = null;

            if (activeProgramID <= 0)
            {
                limitProgram = programControlsService.GetDefaultWeldingMachineProgram(machine.ID);
            }
            else
            {
                limitProgram = context.WeldingLimitPrograms.Find(activeProgramID);
            }

            // LimitProgram loaded
            if (limitProgram != null && limitProgram.ProgramStatus == (int)GeneralStatus.Active)
            {
                var weldingMachineProgramInfo = new WeldingMachineProgramInfo
                {
                    WeldingMachineID = machine.ID,
                    WeldingLimitProgramID = limitProgram.ID,
                    LoadedOn = DateTime.Now,
                    ProgramUpdated = limitProgram.DateUpdated.HasValue ? limitProgram.DateUpdated.Value : limitProgram.DateCreated
                };

                return weldingMachineProgramInfo;
            }

            return null;
        }

        protected override void BeforeStop()
        {
            /*
            try
            {
                context.Dispose();
            }
            catch { }
            */
        }

    }
}
