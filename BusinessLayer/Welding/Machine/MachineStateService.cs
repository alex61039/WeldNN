using BusinessLayer.Models;
using BusinessLayer.Welding.Configuration;
using DataLayer.Welding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Welding.Machine
{
    public class MachineStateService
    {
        const int StateTimeoutSeconds = 10;    // 10 sec

        WeldingContext _context;

        public MachineStateService(WeldingContext context)
        {
            _context = context;
        }

        public void SaveState(int WeldingMachineID, Models.WeldingMachine.StateSummary State)
        {
            Models.WeldingMachine.StateSummary latestState = null;

            // Create new or update existing?
            var bNewState = false;

            // Offline data?
            if (State.IsOfflineData)
            {
                bNewState = true;
            }
            else
            {
                // No state or expired?
                latestState = GetCurrentWeldingMachineState(WeldingMachineID);
                if (latestState == null || !latestState.LastDatetimeUpdate.HasValue || IsStateDateExpired(latestState.LastDatetimeUpdate.Value))
                {
                    bNewState = true;
                }
                else
                {
                    // COMPARE States, if something changed - SAVE New state
                    // Not null, and not expired - compare parameter values
                    if (!latestState.Equals(State))
                    {
                        bNewState = true;
                    }
                }
            }


            WeldingMachineState latestStateData = null;
            if (bNewState || latestState.WeldingMachineStateID == 0)
            {
                bNewState = true;

                // Calculate duration
                if (latestState != null && latestState.LastDatetimeUpdate.HasValue && !IsStateDateExpired(latestState.LastDatetimeUpdate.Value))
                {
                    // not expired, just some props changed.
                    // update Duration of the latest state
                    var tempLatestStateData = _context.WeldingMachineStates.Find(latestState.WeldingMachineStateID);
                    tempLatestStateData.StateDurationMs = (int)DateTime.Now.Subtract(tempLatestStateData.DateCreated).TotalMilliseconds;
                }

                // Datetime of state
                var state_datetime = DateTime.Now;

                // Offline?
                if (State.IsOfflineData)
                {
                    try
                    {
                        var tmp_dt = Utils.WeldingDateTimeFormat.Parse(State.GetRawValue(Models.Configuration.PropertyCodes.MachineDatetime));
                        if (tmp_dt != null)
                        {
                            state_datetime = tmp_dt.Value;
                        }
                        else
                        {
                            // Offline, but datetime not parsed
                            return;
                        }
                    }
                    catch
                    {
                        // Offine, but problems with Datetime - break.
                        return;
                    }
                }


                // Create new state
                latestStateData = new WeldingMachineState
                {
                    DateCreated = state_datetime,
                    DateUpdated = state_datetime,
                    WeldingMachineID = WeldingMachineID,
                    RFID = State.ContainsPropertyCode(Models.Configuration.PropertyCodes.RFID) 
                        ? Utils.RFIDHelper.Clean(State.GetRawValue(Models.Configuration.PropertyCodes.RFID)) 
                        : null,
                    WeldingMachineStatus = (int)State.Status,
                    Control = State.Control,
                    ControlState = State.ControlState,
                    ErrorCode = State.ErrorCode,
                    StateDurationMs = 1000,          // Default period - 1 second
                    WeldingMaterialID = State.WeldingMaterialID,
                    LimitsExceeded = State.LimitsExceeded,
                    WeldingLimitProgramID = State.WeldingLimitProgramID,
                    WeldingLimitProgramName = State.WeldingLimitProgramName
                };

                _context.WeldingMachineStates.Add(latestStateData);
            }
            else
            {
                // retrieve state
                latestStateData = _context.WeldingMachineStates.Find(latestState.WeldingMachineStateID);

                // update date
                latestStateData.DateUpdated = DateTime.Now;

                // update duration
                latestStateData.StateDurationMs = (int)DateTime.Now.Subtract(latestStateData.DateCreated).TotalMilliseconds;
            }

            // Save state
            // _context.SaveChanges();


            // Store property values only for NEW state
            if (bNewState)
            {
                // int stateID = latestStateData.ID;

                if (latestStateData.WeldingMachineParameterValues == null)
                    latestStateData.WeldingMachineParameterValues = new List<WeldingMachineParameterValue>();

                // Insert property values
                foreach (var p in State.Properties.Values)
                {
                    var weldingMachineParameterValue = new WeldingMachineParameterValue
                    {
                        // WeldingMachineStateID = stateID,
                        PropertyCode = p.PropertyCode,
                        Value = p.Value,
                        PropertyType = p.PropertyType,
                        RawValue = p.RawValue,
                        LimitsExceeded = p.LimitsExceeded,
                        LimitMin = p.LimitMin,
                        LimitMax = p.LimitMax
                    };

                    latestStateData.WeldingMachineParameterValues.Add(weldingMachineParameterValue);
                    // _context.WeldingMachineParameterValues.Add(weldingMachineParameterValue);
                }
            }

            // Save values
            _context.SaveChanges();
        }

        public bool IsStateDateExpired(DateTime DateUpdated)
        {
            return DateUpdated.AddSeconds(StateTimeoutSeconds) < DateTime.Now;
        }

        public Models.WeldingMachine.StateSummary GetCurrentWeldingMachineState(int WeldingMachineID)
        {
            // Check Machine's maintenance/service
            var isOnService = _context.Maintenances.Any(ss => ss.WeldingMachineID == WeldingMachineID
                && ss.Status == (int)GeneralStatus.Active
                && ss.MaintenanceStatus == (int)MaintenanceStatus.InProcess);

            if (isOnService)
            {
                return new Models.WeldingMachine.StateSummary
                {
                    DateCreated = DateTime.Now,
                    WeldingMachineStateID = 0,
                    LastDatetimeUpdate = null,
                    Properties = null,
                    Status = Models.WeldingMachineStatus.Service,
                    StateDurationMs = 0
                };
            }

            var stateData = GetLatestMachineStateData(WeldingMachineID);

            // Check if data exists or expired
            if (stateData == null)
            {
                return new Models.WeldingMachine.StateSummary
                {
                    DateCreated = DateTime.Now,
                    WeldingMachineStateID = 0,
                    LastDatetimeUpdate = null,
                    Properties = null,
                    Status = Models.WeldingMachineStatus.Off,
                    StateDurationMs = 0
                };
            }

            // Expired? (Off?)
            if (IsStateDateExpired(stateData.DateUpdated))
            {
                return new Models.WeldingMachine.StateSummary
                {
                    Status = Models.WeldingMachineStatus.Off,
                    DateCreated = stateData.DateCreated,
                    WeldingMachineStateID = stateData.ID,
                    LastDatetimeUpdate = stateData.DateUpdated,
                    Properties = null,
                    StateDurationMs = stateData.StateDurationMs,
                    WeldingMaterialID = stateData.WeldingMaterialID,
                    LimitsExceeded = stateData.LimitsExceeded.GetValueOrDefault(),
                    WeldingLimitProgramID = stateData.WeldingLimitProgramID,
                    WeldingLimitProgramName = stateData.WeldingLimitProgramName
                };
            }

            // Get Actual data
            return LoadState(stateData);
        }

        public Models.WeldingMachine.StateSummary LoadState(WeldingMachineState stateData)
        {
            if (stateData == null)
                return null;

            // Default state - READY
            var result = new Models.WeldingMachine.StateSummary
            {
                DateCreated = stateData.DateCreated,
                WeldingMachineStateID = stateData.ID,
                LastDatetimeUpdate = stateData.DateUpdated,
                // Status = Models.WeldingMachineStatus.Ready
                Status = (Models.WeldingMachineStatus)stateData.WeldingMachineStatus,
                StateDurationMs = stateData.StateDurationMs,
                Control = stateData.Control,
                ControlState = stateData.ControlState,
                ErrorCode = stateData.ErrorCode,
                WeldingMaterialID = stateData.WeldingMaterialID,
                LimitsExceeded = stateData.LimitsExceeded.GetValueOrDefault(),
                WeldingLimitProgramID = stateData.WeldingLimitProgramID,
                WeldingLimitProgramName = stateData.WeldingLimitProgramName,

                Properties = new Dictionary<string, Models.WeldingMachine.StateSummaryPropertyValue>()
            };

            var propValues = GetWeldingMachineParameterValues(stateData.ID);
            foreach (var pv in propValues)
            {
                if (!String.IsNullOrEmpty(pv.PropertyCode))
                {
                    result.Properties[pv.PropertyCode] = new Models.WeldingMachine.StateSummaryPropertyValue {
                        PropertyCode = pv.PropertyCode,
                        Value = pv.Value,
                        PropertyType = pv.PropertyType,
                        RawValue = pv.RawValue,
                        LimitsExceeded = pv.LimitsExceeded.GetValueOrDefault(),
                        LimitMin = pv.LimitMin,
                        LimitMax = pv.LimitMax
                    };
                }
                    
            }

            /*
            // Is machine working?
            if (result.GetNumericValue(Models.Configuration.PropertyCodes.I) > 0)
            {
                result.Status = Models.WeldingMachineStatus.Working;
            }
            */

            return result;
        }

        public Models.WeldingMachine.StateSummary LoadState(int StateID)
        {
            var stateData = GetStateData(StateID);

            return LoadState(stateData);
        }


        /// <summary>
        /// Returns Property values by StateID from DB
        /// </summary>
        public List<WeldingMachineParameterValue> GetWeldingMachineParameterValues(int WeldingMachineStateID)
        {
            return _context.WeldingMachineParameterValues
                .Where(pv => pv.WeldingMachineStateID == WeldingMachineStateID)
                .ToList();
        }

        /// <summary>
        /// Returns State record from DB
        /// </summary>
        public WeldingMachineState GetLatestMachineStateData(int WeldingMachineID)
        {
            var query = _context.WeldingMachineStates
                .Where(s => s.WeldingMachineID == WeldingMachineID)
                .OrderByDescending(s => s.DateUpdated)
                .Take(1);

            var state = query
                .FirstOrDefault();

            return state;
        }


        /// <summary>
        /// Returns State record from DB
        /// </summary>
        public WeldingMachineState GetStateData(int StateID)
        {
            var state = _context.WeldingMachineStates
                .Where(s => s.ID == StateID)
                .FirstOrDefault();

            return state;
        }

    }
}
