using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        //ship lab research subjects flattened
        public List<string> m_researchIDs = new List<string>();

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
                    foreach ( ScienceData data in datas )
                    {
                        Debug.Log("GA dict add " + data.subjectID + " " + part.ToString());
                        m_sciDataParts.Add(data, part);
                    }

                }
            }

            //the labs researched subjects alter the worth of the science datas
            List<string> researchIDs = new List<string>();
            m_labModules.ForEach(lab => researchIDs.AddRange(lab.ExperimentData));
            if ( !researchIDs.SequenceEqual(m_researchIDs))
            {
                m_flags.scienceDatasDirty = true;
                m_researchIDs = researchIDs;
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

        WhichData m_controller;

        public void DiscardScienceDatas(List<ScienceData> discards)
        {
            //remove data from parts
            Debug.Log("GA model discarding " + discards.Count);
            discards.ForEach( sd => m_sciDataParts[sd].DumpData(sd) );

            m_scienceEventOccured = true;
        }


        void HighlightPart(Part part, Color color)
        {
            //old normal based glow
            part.SetHighlightType(Part.HighlightType.AlwaysOn);
            part.SetHighlightColor(color);
            part.SetHighlight(true, false);

            //PPFX glow edge highlight
            GameObject go = part.FindModelTransform("model").gameObject;
            HighlightingSystem.Highlighter hl = go.GetComponent<HighlightingSystem.Highlighter>();
            if (hl == null) { hl = go.AddComponent<HighlightingSystem.Highlighter>(); }
            hl.ConstantOn(color);
            hl.SeeThroughOn();
        }

        void UnHighlightPart(Part part)
        {
            //old normal based glow
            part.SetHighlightDefault();
            part.SetHighlight(false, false);

            //PPFX glow edge highlight
            GameObject go = part.FindModelTransform("model").gameObject;
            HighlightingSystem.Highlighter hl = go.GetComponent<HighlightingSystem.Highlighter>();
            if (hl != null)
            {
                hl.ConstantOff();
            }
        }

        public void HighlightContainers(Color color)
        {
            m_containerModules.ForEach(cont => HighlightPart(cont.part, color));
        }

        public void UnhighlightContainers()
        {
            m_containerModules.ForEach(cont => UnHighlightPart(cont.part));
        }

        public bool HaveClickedPart(Vector3 screenClick, out Part clickedPart)
        {
            clickedPart = null;

            Ray clickRay = Camera.main.ScreenPointToRay(screenClick);
            RaycastHit hit;
            bool strike = Physics.Raycast(clickRay, out hit, 2500f, 1 << 0); //2.5km length.  Note ship parts are on layer 0, mask needs to be 1<<layer desired
            if (strike)
            {
                clickedPart = hit.collider.gameObject.GetComponentInParent<Part>(); //the collider's gameobject is child to partmodule's gameobject
            }

            return clickedPart != null;
        }

        public bool IsPartSciContainer(Part part, out ModuleScienceContainer clickedContainer)
        {
            clickedContainer = m_containerModules.Find(sciCont => sciCont.part == part);
            return clickedContainer != null;
        }

        public class DataMove
        {
            public ScienceData m_sciData;
            public IScienceDataContainer m_src;
            public ModuleScienceContainer m_dst;
            public DataMove(ModuleScienceContainer dst, IScienceDataContainer src, ScienceData sd)
            {
                m_sciData = sd;
                m_src = src;
                m_dst = dst;

                src.DumpData(sd); //begin the dump
            }
            public bool IsComplete()
            {
                if ( !m_src.GetData().Contains(m_sciData) ) //dump data seems to take 1 frame to complete so poll
                {
                    //once DumpData is done, AddData is instantaneous
                    m_dst.AddData(m_sciData);
                    return true;
                }
                return false;
            }
        }

        public List<DataMove> m_dataMoves = new List<DataMove>();
        public void ProcessMoveDatas(ModuleScienceContainer dst, List<ScienceData> sources)
        {
            sources.ForEach(sd=> m_dataMoves.Add(new DataMove( dst, m_sciDataParts[sd], sd)));
        }

        public float GetLabResearchPoints(ScienceData sciData)
        {
            //no labs
            if (m_labModules.Count == 0) { return 0f; }

            ModuleScienceLab lab = m_labModules.First(); //TODOJEFFGIFFEN multilab tests

            //lab already has data
            if (lab.ExperimentData.Contains(sciData.subjectID)) { return 0f; }
            //if (!ModuleScienceLab.IsLabData(FlightGlobals.ActiveVessel, d)) { return 0f; }

            CelestialBody body = FlightGlobals.getMainBody();

            float scalar = 1f;

            //surface boost
            bool grounded = FlightGlobals.ActiveVessel.Landed || FlightGlobals.ActiveVessel.Splashed;
            if (grounded)
            {
                scalar *= 1f + lab.SurfaceBonus;

                //kerbin penalty
                if (body.isHomeWorld)
                {
                    scalar *= lab.homeworldMultiplier;
                }
            }

            //neighborhood boost
            if (sciData.subjectID.Contains(body.bodyName))
            {
                scalar *= 1f + lab.ContextBonus;
            }

            ScienceSubject subject = ResearchAndDevelopment.GetSubjectByID(sciData.subjectID);
            float refValue = ResearchAndDevelopment.GetReferenceDataValue(sciData.dataAmount, subject);
            float sciMultiplier = HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

            return refValue * scalar * sciMultiplier;
        }

        protected class ResearchDatum
        {
            public ScienceData scienceData;
            public float researchPoints;
            public ResearchDatum(DataPage d)
            {
                scienceData = d.m_scienceData;
                researchPoints = d.m_labPts;
            }
        }

        protected List<ResearchDatum> m_labCopyQueue = new List<ResearchDatum>();
        protected bool m_copying = false;

        protected void FinishLabCopy( ScienceData finishedData )
        {
            m_copying = false;              //clear guard bool

            ResearchDatum rd = m_labCopyQueue.First();
            Debug.Log("GA model finish lab copy " + m_labCopyQueue.Count + " " + rd.scienceData.title);

            //HACKJEFFGIFFEN the lab copy fails (missing the ExperiDlg), so we manually add the pts
            m_labModules.First().dataStored += rd.researchPoints;
            
            m_labCopyQueue.RemoveAt(0);     //dequeue finished data
            m_scienceEventOccured = true;   //the experiment will re-appeared in the container //TODOJEFFGIFFEN verify

            //continue through queue
            StartNextLabCopy();
        }

        protected void StartNextLabCopy()
        {
            //TODOJEFFGIFFEN multiple labs concerns
            //TODOJEFFGIFFEN possible to leave scene overtop of processing long time...
            if (!m_copying)
            {
                //HACKJEFFGIFFEN does..the lab fail after copy on data full?
                if (m_labCopyQueue.Count > 0)
                {
                    m_copying = true;
                    Debug.Log("GA model start lab copy " + m_labCopyQueue.Count + " " + m_labCopyQueue.First().scienceData.title);

                    m_controller.LaunchCoroutine(
                        m_labModules.First().ProcessData(m_labCopyQueue.First().scienceData, FinishLabCopy)
                    );
                }
            }
        }

        public void ProcessLabDatas(List<DataPage> dataPages)
        {
            //queue up & run lab copy data
            dataPages.ForEach(dp => m_labCopyQueue.Add(new ResearchDatum(dp)));
            StartNextLabCopy();

            //consider it queued in lab, so remove from parts
            //TODOJEFFGIFFEN cant re-add to experi...so we leave it?
            //dataPages.ForEach(dp => dp.m_dataModule.DumpData(dp.m_scienceData));

            m_scienceEventOccured = true;
        }


        public void ProcessTransmitDatas(List<ScienceData> datas)
        {
            Debug.Log("GA model transmit:");
            datas.ForEach(sd => Debug.Log(sd.subjectID));
            //TODOJEFFGIFFEN choosing radio
            //TODOJEFFGIFFEN kill during transmit concern
            m_radioModules.First().TransmitData(datas);

            //consider it queued in radio?, so remove from parts
            datas.ForEach(sd => m_sciDataParts[sd].DumpData(sd));

            m_scienceEventOccured = true;
        }


        //returns empty string on success, error string on failure
        public string Initialize(WhichData controller)
        {
            string errorMsg = string.Empty;

            m_controller = controller;
            
            return errorMsg;
        }

        public void Update()
        {
            //process queued data moves
            //TODOJEFFGIFFEN possibly coroutine
            if (m_dataMoves.Count() > 0)
            {
                //wait for all moves to complete
                m_dataMoves.RemoveAll(dm => dm.IsComplete());

                //when all complete, throw event
                if ( m_dataMoves.Count == 0) {m_scienceEventOccured = true;}
            }

            m_flags.Clear(); //clear all dirty flags to false

            if (m_partEventOccured) { ScanParts(); }
            //part change can alter the sci/crew payload
            if (m_partEventOccured || m_scienceEventOccured) { ScanDatas(); }
            if (m_partEventOccured || m_crewEventOccured) { ScanCrew(); }

            m_partEventOccured = m_scienceEventOccured = m_crewEventOccured = false;
        }

        //HACKJEFFGIFFEN
        static void ExploreClass(string typeName)
        {
            Debug.Log("GA ExploreClass " + typeName);
            
            //grabbing types from assemblies other than yours needs fully qualified name
            string fullyQualName = typeName + ", Assembly-CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            Type t = Type.GetType(fullyQualName);

            FieldInfo[] privFields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic); //specifically search for non-public instance methods to find hidden/private ones
            foreach (FieldInfo field in privFields)
            {
                Debug.Log("GA field: " + field.FieldType.Name + " " + field.Name);
            }

            PropertyInfo[] privProps = t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (PropertyInfo prop in privProps)
            {
                Debug.Log("GA prop: writeable " + prop.CanWrite + " " + prop.PropertyType.Name + " " + prop.Name);
            }

            object[] privAttri = t.GetCustomAttributes(true); //include hierarchy
            foreach (object attri in privAttri)
            {
                Debug.Log("GA attri: " + attri.ToString());
            }

            MethodInfo[] privMethods = t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (MethodInfo method in privMethods)
            {
                string paramas = string.Empty;
                foreach (ParameterInfo pi in method.GetParameters())
                {
                    paramas += " " + pi.ParameterType.Name + " " + pi.Name;
                }
                Debug.Log("GA method: " + method.ReturnType.ToString() + " " + method.Name + paramas);
            }
        }
    }
}
