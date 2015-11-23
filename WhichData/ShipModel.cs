using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WhichData
{
    public class ShipModel
    {
        //wrt this frame
        private bool m_partEventOccured = false;
        private bool m_crewEventOccured = false;
        private bool m_scienceEventOccured = false;
        //generic arg handlers
        public void OnShipEvent<T>(T arg) { m_partEventOccured = true; }
        public void OnCrewEvent<T>(T arg) { m_crewEventOccured = true; }
        public void OnScienceEvent<T>(T arg) { m_scienceEventOccured = true; }

        //public static EventData<Part> onPartActionUICreate; //anytime right cick menu opens (mess with data name?)
        //public static EventData<Part> onPartActionUIDismiss;

        //from ModuleScienceContainer...
        //CollectDataExternalEvent
        //ReviewDataEvent
        //StoreDataExternalEvent
        
        public void OnAwake()
        {
            //register for events
            GameEvents.onPartCouple.Add(OnShipEvent);       //dock
            GameEvents.onPartJointBreak.Add(OnShipEvent);   //undock, shear part off, decouple, destroy (phys parts)
            GameEvents.onPartDie.Add(OnShipEvent);          //destroy (including non-phys parts)
            GameEvents.onVesselChange.Add(OnShipEvent);     //load/launch/quicks witch

            GameEvents.onCrewBoardVessel.Add(OnCrewEvent);
            GameEvents.onCrewOnEva.Add(OnCrewEvent);
            GameEvents.onCrewKilled.Add(OnCrewEvent);
            GameEvents.onCrewTransferred.Add(OnCrewEvent);

            GameEvents.OnExperimentDeployed.Add(OnScienceEvent);
        }

        public void OnDestroy()
        {
            //deregister for events
            GameEvents.onPartCouple.Remove(OnShipEvent);        //dock
            GameEvents.onPartJointBreak.Remove(OnShipEvent);    //undock, shear part off, decouple, destroy (phys parts)
            GameEvents.onPartDie.Remove(OnShipEvent);           //destroy (including non-phys parts)
            GameEvents.onVesselChange.Remove(OnShipEvent);      //load/launch/quicks witch

            GameEvents.onCrewBoardVessel.Remove(OnCrewEvent);
            GameEvents.onCrewOnEva.Remove(OnCrewEvent);
            GameEvents.onCrewKilled.Remove(OnCrewEvent);
            GameEvents.onCrewTransferred.Remove(OnCrewEvent);

            GameEvents.OnExperimentDeployed.Remove(OnScienceEvent);
        }

        //flag pack
        public class Flags
        {
            //dirty flags
            public bool sciDataModulesDirty { get; set; }
            public bool labModulesDirty { get; set; }
            public bool radioModulesDirty { get; set; }
            public bool scienceDatasDirty { get; set; }
            public bool crewDirty { get; set; }
            public Flags() { Clear(); }
            public void Clear()
            { sciDataModulesDirty = labModulesDirty = radioModulesDirty = scienceDatasDirty = crewDirty = false; }
        }
        private Flags m_flags = new Flags();
        public Flags dirtyFlags { get { return m_flags; } } //get-only property

        //ship pieces
        //HACKJEFFGIFFEN accessors better, plus setting involves work
        public List<IScienceDataContainer> m_sciDataModules = new List<IScienceDataContainer>();
        public Dictionary<ScienceData, IScienceDataContainer> m_sciDataParts = new Dictionary<ScienceData, IScienceDataContainer>();
        
        //sciDataModules split into experiments and containers
        public List<ModuleScienceExperiment> m_experiModules = new List<ModuleScienceExperiment>();
        public List<ModuleScienceContainer> m_containerModules = new List<ModuleScienceContainer>();
        
        //labs and radios
        public List<ModuleScienceLab> m_labModules = new List<ModuleScienceLab>();
        public List<IScienceDataTransmitter> m_radioModules = new List<IScienceDataTransmitter>();

        //ship ScienceData flattened
        public List<ScienceData> m_scienceDatas = new List<ScienceData>();

        //crew
        //TODOJEFFGIFFEN

        private void ScanParts()
        {
            Debug.Log("GA ShipModel::ScanParts");
            //TODOJEFFGIFFEN return more detailed flags of what changed

            //check scidat containers
            List<IScienceDataContainer> sciDataModules = FlightGlobals.ActiveVessel.FindPartModulesImplementing<IScienceDataContainer>();
            if (!sciDataModules.SequenceEqual(m_sciDataModules))
            {
                m_flags.sciDataModulesDirty = true;
                m_sciDataModules = sciDataModules;
                m_experiModules = sciDataModules.OfType<ModuleScienceExperiment>().ToList<ModuleScienceExperiment>();
                m_containerModules = sciDataModules.OfType<ModuleScienceContainer>().ToList<ModuleScienceContainer>();
            }

            //check labs
            List<ModuleScienceLab> labModules = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceLab>();
            if (!labModules.SequenceEqual(m_labModules))
            {
                m_flags.labModulesDirty = true;
                m_labModules = labModules;
            }

            //check radios
            List<IScienceDataTransmitter> radioModules = FlightGlobals.ActiveVessel.FindPartModulesImplementing<IScienceDataTransmitter>();
            if (!radioModules.SequenceEqual(m_radioModules))
            {
                m_flags.radioModulesDirty = true;
                m_radioModules = radioModules;
            }
        }

        private void ScanDatas()
        {
            Debug.Log("GA ShipModel::ScanDatas");
            //check data
            //TODOJEFFGIFFEN this cooooould do partial updating based on changed containers...
            List<ScienceData> scienceDatas = new List<ScienceData>();
            m_sciDataModules.ForEach(sdm => scienceDatas.AddRange(sdm.GetData()));
            if (!scienceDatas.SequenceEqual(m_scienceDatas))
            {
                m_flags.scienceDatasDirty = true;
                m_scienceDatas = scienceDatas;

                //rebuild data->part map
                m_sciDataParts.Clear();
                foreach (IScienceDataContainer part in m_sciDataModules)
                {   //HACKJEFFGIFFEN do faster later
                    ScienceData[] datas = part.GetData();
                    foreach( ScienceData data in datas ) { m_sciDataParts.Add(data, part); }
                }
            }
        }

        private void ScanCrew()
        {
            Debug.Log("GA ShipModel::ScanCrew");
            //TODOJEFFGIFFEN
            if (false)
            {
                m_flags.crewDirty = true;
                //...
            }
        }
         

        //returns empty string on success, error string on failure
        public string Initialize()
        {
            string errorMsg = string.Empty;
            //TODOJEFFGIFFEN no init needed

            return errorMsg;
        }

        public void Update()
        {
            m_flags.Clear(); //clear all dirty flags to false

            if (m_partEventOccured) { ScanParts(); }
            //part change can alter the sci/crew payload
            if (m_partEventOccured || m_scienceEventOccured) { ScanDatas(); }
            if (m_partEventOccured || m_crewEventOccured) { ScanCrew(); }

            m_partEventOccured = m_scienceEventOccured = m_crewEventOccured = false;
        }
    }
}
