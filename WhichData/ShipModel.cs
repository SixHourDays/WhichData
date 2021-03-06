﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace WhichData
{
    public class DataPage : IEquatable<DataPage>
    {
        public ScienceData m_scienceData;
        public ScienceSubject m_subject;
        public IScienceDataContainer m_dataModule;
        public PartModule m_partModule;

        public bool m_isExperi;
        public float m_fullValue;
        public float m_nextFullValue;
        public float m_transmitValue;
        public float m_nextTransmitValue;
        public bool m_trnsWarnEnabled;
        public float m_labPts;

        //subjectID parsed
        public string m_experi;
        public string m_body;
        public string m_situ;
        public string m_biome;

        //for ship scans
        //HACKJEFFGIFFEN this will need updated to work in the multiple data 1 part scenario
        public override int GetHashCode()
        {
            return m_scienceData.GetHashCode() + m_subject.GetHashCode() + m_dataModule.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DataPage);
        }

        public bool Equals(DataPage other)
        {
            if ( ReferenceEquals(this, other) ) { return true; } //this==this
            bool anull = ReferenceEquals(this, null);
            bool bnull = ReferenceEquals(other, null);
            if (anull ^ bnull) { return false; } //this==null or null==this
            if (anull && bnull ) { return true; } //null==null or this==this
            
            return m_scienceData == other.m_scienceData //m_scienceData distinguishes between instances (can detect repeats)
                && m_dataModule == other.m_dataModule   //m_dataModule distinguishes between hosts (can detect moves)
                && Mathf.Approximately(m_fullValue, other.m_fullValue)     //the next & transmit values all base off this //TODOJEFFGIFFEN can pump updates later
                && Mathf.Approximately(m_labPts, other.m_labPts);          //represent the content of the labs
        }

        //for ERD spawns
        public bool Equals(ExperimentResultDialogPage erdp)
        {
            //ERDP is a might tricky - because it only returns the host as a part, we must distinguish
            //between containers and experiments ourselves if that part has both.
            IScienceDataContainer cont = erdp.showReset ? 
                erdp.host.FindModuleImplementing<ModuleScienceExperiment>() as IScienceDataContainer
                : erdp.host.FindModuleImplementing<ModuleScienceContainer>() as IScienceDataContainer;

            return m_scienceData == erdp.pageData && m_dataModule == cont;
        }

        public DataPage(IScienceDataContainer dataModule, ScienceData sciData, ShipModel shipModel)
        {
            m_scienceData = sciData;
            m_subject = ResearchAndDevelopment.GetSubjectByID(m_scienceData.subjectID);
            //ModuleScienceContainer and ModuleScienceExperiment subclass this
            m_dataModule = dataModule;
            m_isExperi = dataModule is ModuleScienceExperiment;
            m_partModule = m_isExperi //downcast off interface 1:2 ways, then upcast back to common concrete heirarchy
              ? m_dataModule as ModuleScienceExperiment as PartModule
              : m_dataModule as ModuleScienceContainer as PartModule;
            

            //compose data used in row display
            //displayed science in KSP is always 2x the values used in bgr
            m_fullValue = ResearchAndDevelopment.GetScienceValue(m_scienceData.dataAmount, m_subject, 1.0f);
            m_nextFullValue = ResearchAndDevelopment.GetNextScienceValue(m_scienceData.dataAmount, m_subject, 1.0f);
            m_transmitValue = ResearchAndDevelopment.GetScienceValue(m_scienceData.dataAmount, m_subject, m_scienceData.transmitValue);
            m_nextTransmitValue = ResearchAndDevelopment.GetNextScienceValue(m_scienceData.dataAmount, m_subject, m_scienceData.transmitValue);
            m_trnsWarnEnabled = m_isExperi && !((ModuleScienceExperiment)m_dataModule).IsRerunnable();
            m_labPts = shipModel.GetLabResearchPoints(sciData);

            //parse out subjectIDs of the form (we ditch the @):
            //  crewReport@KerbinSrfLandedLaunchPad
            m_experi = m_body = m_situ = m_biome = string.Empty;

            //experiment
            string[] strings = m_scienceData.subjectID.Split('@');
            m_experi = strings[0];                                                  // crewReport

            //body
            string subject = strings[1];
            List<CelestialBody> bodies = FlightGlobals.Bodies;
            foreach (CelestialBody body in bodies)
            {
                if (subject.StartsWith(body.name))                                  // Kerbin
                {
                    m_body = body.name;
                    subject = subject.Substring(body.name.Length);
                    break;
                }
            }
            if (m_body == string.Empty)
            {
                Debug.Log("GA ERROR no body in id " + m_scienceData.subjectID);
            }

            //situation
            List<string> situations = ResearchAndDevelopment.GetSituationTags();
            foreach (string situ in situations)
            {
                if (subject.StartsWith(situ))                                       // SrfLanded
                {
                    m_situ = situ;
                    subject = subject.Substring(situ.Length);
                    break;
                }
            }
            if (m_situ == string.Empty)
            {
                Debug.Log("GA ERROR no situ in id " + m_scienceData.subjectID);
            }

            //biome
            //when a situation treats experiment as global, no biome name is appended, and subject will now be string.empty
            if (subject != string.Empty)
            {
                //TODOJEFFGIFFEN can't find a complete list of them programmatically,
                //R&D doesnt return "R&D" or "Flag Pole" etc
                m_biome = subject;                                                      //LaunchPad
            }
        }

        //experiments reset, containers discard.  if a non-repeatable experi discards, it becomes unusable...not our intention.
        public void DiscardModuleData()
        {
            if (m_isExperi) { (m_dataModule as ModuleScienceExperiment).ResetExperiment(); } //reset when possible
            else { m_dataModule.DumpData(m_scienceData); } //interface method
        }
    }

    public class ShipModel
    {
        //wrt this frame
        private bool m_partEventOccured = false;
        private bool m_crewEventOccured = false;
        private bool m_scienceEventOccured = false;
        //generic arg handlers
        public void OnShipEvent<T>(T arg) { m_partEventOccured = true; }
        public void OnCrewEvent<T>(T arg) { m_crewEventOccured = true; }
        //notable handlers
        private ScienceData m_deployedResult = null; //check against this later in scandata
        public void OnScienceEvent(ScienceData result) { m_deployedResult = result; m_scienceEventOccured = true; }

        public void OnAwake()
        {
            //register for events
            GameEvents.onPartCouple.Add(OnShipEvent);       //dock
            GameEvents.onPartJointBreak.Add(OnShipEvent);   //undock, shear part off, decouple, destroy (phys parts)
            GameEvents.onPartDie.Add(OnShipEvent);          //destroy (including non-phys parts)

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

            GameEvents.onCrewBoardVessel.Remove(OnCrewEvent);
            GameEvents.onCrewOnEva.Remove(OnCrewEvent);
            GameEvents.onCrewKilled.Remove(OnCrewEvent);
            GameEvents.onCrewTransferred.Remove(OnCrewEvent);

            GameEvents.OnExperimentDeployed.Remove(OnScienceEvent);

            CancelDataQueues();
        }

        //flag pack
        public class Flags
        {
            //events
            public bool experimentDeployed { get; set; }

            //dirty flags
            public bool sciDataModulesDirty { get; set; }
            public bool labModulesDirty { get; set; }
            public bool radioModulesDirty { get; set; }
            public bool scienceDatasDirty { get; set; }
            public List<DataPage> newScienceDatas { get; set; }
            public List<DataPage> lostScienceDatas { get; set; }
            public bool disabledExperisDirty { get; set; }
            public bool habitablePartsDirty { get; set; }
            public bool crewDirty { get; set; }
            public Flags()
            {
                newScienceDatas = new List<DataPage>();
                lostScienceDatas = new List<DataPage>();
                Clear();
            }
            public void Clear()
            {
                experimentDeployed = sciDataModulesDirty = labModulesDirty = radioModulesDirty = scienceDatasDirty = disabledExperisDirty = habitablePartsDirty = crewDirty = false;
                newScienceDatas.Clear(); lostScienceDatas.Clear();
            }
        }
        public Flags m_flags = new Flags();

        //ship we refer to
        public Vessel m_ship = null;

        //ship science parts, containers and experiments
        public List<IScienceDataContainer> m_sciDataModules = new List<IScienceDataContainer>();

        //sciDataModules split into experiments and containers
        public List<ModuleScienceExperiment> m_experiModules = new List<ModuleScienceExperiment>();
        public List<ModuleScienceContainer> m_containerModules = new List<ModuleScienceContainer>();
        //sciDataModules that are inert (needing reset)
        public List<ModuleScienceExperiment> m_disabledExperiModules = new List<ModuleScienceExperiment>();

        //labs and radios
        public List<ModuleScienceLab> m_labModules = new List<ModuleScienceLab>();
        public List<ModuleDataTransmitter> m_radioModules = new List<ModuleDataTransmitter>();

        //ship ScienceData flattened, inside a metadata class
        public List<DataPage> m_scienceDatas = new List<DataPage>();
        //ship lab research subjects flattened
        public List<string> m_researchIDs = new List<string>();
        //experi-only science data flattened
        public List<DataPage> m_experiScienceDatas = new List<DataPage>();

        //parts that accomodate kerbals
        public List<Part> m_habitableParts = new List<Part>();
        //crew flattened
        public List<ProtoCrewMember> m_crewMembers = new List<ProtoCrewMember>();
        public bool m_scientistAboard = false;

        private void ScanParts()
        {
            if (m_ship == null) { return; }//intentionally will happen on the extern model fairly often

            Debug.Log("GA model" + m_index + " ScanParts");
            //TODOJEFFGIFFEN return more detailed flags of what changed

            //check scidat containers
            List<IScienceDataContainer> sciDataModules = m_ship.FindPartModulesImplementing<IScienceDataContainer>();
            if (!sciDataModules.SequenceEqual(m_sciDataModules))
            {
                m_flags.sciDataModulesDirty = true;
                m_sciDataModules = sciDataModules; //deliberate ref swap
                m_experiModules = sciDataModules.OfType<ModuleScienceExperiment>().ToList<ModuleScienceExperiment>();
                m_containerModules = sciDataModules.OfType<ModuleScienceContainer>().ToList<ModuleScienceContainer>();
            }

            //check labs
            List<ModuleScienceLab> labModules = m_ship.FindPartModulesImplementing<ModuleScienceLab>();
            if (!labModules.SequenceEqual(m_labModules))
            {
                m_flags.labModulesDirty = true;
                m_labModules = labModules; //deliberate ref swap
            }

            //check radios
            List<ModuleDataTransmitter> radioModules = m_ship.FindPartModulesImplementing<ModuleDataTransmitter>();
            if (!radioModules.SequenceEqual(m_radioModules))
            {
                m_flags.radioModulesDirty = true;
                m_radioModules = radioModules; //deliberate ref swap
            }

            List<Part> habitableParts = m_ship.parts.FindAll(p => p.CrewCapacity > 0);
            if ( !habitableParts.SequenceEqual(m_habitableParts))
            {
                m_flags.habitablePartsDirty = true;
                m_habitableParts = habitableParts;
            }
        }

        private void ScanDatas()
        {
            Debug.Log("GA model" + m_index + " ScanDatas");
            //check data
            //TODOJEFFGIFFEN this cooooould do partial updating based on changed containers...
            List<DataPage> scienceDatas = new List<DataPage>();
            foreach (IScienceDataContainer cont in m_sciDataModules)
            {
                foreach (ScienceData sd in cont.GetData())
                {
                    scienceDatas.Add(new DataPage(cont, sd, this));
                }
            }

            //the m_scienceData list and the processor lists are mutually exclusive
            //TODOJEFFGIFFEN if the processor supported it we could lazily maintain this list
            scienceDatas.RemoveAll(dp=> m_discardDataQueue.m_queue.Contains(dp));
            scienceDatas.RemoveAll(dp=> m_moveDataQueue.m_queue.Contains(dp));
            scienceDatas.RemoveAll(dp=> m_labDataQueue.m_queue.Contains(dp));
            scienceDatas.RemoveAll(dp=>m_transmitDataQueue.m_queue.Contains(dp));

            //m_scienceDatas.ForEach(dp => Debug.Log("GA model"+m_index+" scandatas old " + dp + " " + dp.m_subject.id));
            //scienceDatas.ForEach(dp => Debug.Log("GA model"+m_index+" scandatas new " + dp + " " + dp.m_subject.id));

            if (!scienceDatas.SequenceEqual(m_scienceDatas)) //will rely on DataPage::Equals
            {
                m_flags.scienceDatasDirty = true;
                m_flags.lostScienceDatas.AddRange(m_scienceDatas.Except(scienceDatas)); //accumulate
                m_flags.newScienceDatas.AddRange(scienceDatas.Except(m_scienceDatas));

                m_scienceDatas = scienceDatas; //deliberate ref swap

                //catch science experiment deploys in our new science pages
                //m_flags.newScienceDatas.ForEach(dp => Debug.Log("GA model" + m_index + " scandatas delta " + dp + " " + dp.m_subject.id));
                if (m_deployedResult != null && m_flags.newScienceDatas.Exists(dp => dp.m_scienceData == m_deployedResult))
                {
                    m_flags.experimentDeployed = true;
                }
                m_deployedResult = null;

                //list of science data in the experiments (not the containers)
                m_experiScienceDatas = m_scienceDatas.FindAll(dp => dp.m_isExperi);
            }

            //list of the inoperable experis (reset case doesn't actuall have a ScienceData change)
            List<ModuleScienceExperiment> disabledExperis = m_experiModules.FindAll(e => e.Inoperable);
            if (!disabledExperis.SequenceEqual(m_disabledExperiModules))
            {
                m_flags.disabledExperisDirty = true;
                m_disabledExperiModules = disabledExperis;
            }

            //the labs researched subjects alter the worth of the science datas
            List<string> researchIDs = new List<string>();
            m_labModules.ForEach(lab => researchIDs.AddRange(lab.ExperimentData));
            if (!researchIDs.SequenceEqual(m_researchIDs))
            {
                m_flags.scienceDatasDirty = true;
                m_researchIDs = researchIDs; //deliberate ref swap
            }
        }

        private void ScanCrew()
        {
            Debug.Log("GA model" + m_index + " ScanCrew");

            List<ProtoCrewMember> crewMembers = new List<ProtoCrewMember>();
            m_habitableParts.ForEach(hp => crewMembers.AddRange(hp.protoModuleCrew));
            if ( !crewMembers.SequenceEqual( m_crewMembers) )
            {
                m_crewMembers = crewMembers;
                m_scientistAboard = m_crewMembers.Exists(c => c.experienceTrait.TypeName == "Scientist");
                m_flags.crewDirty = true;
            }
        }

        public List<DataPage> GetContainerPages(IScienceDataContainer cont)
        {
            return m_scienceDatas.FindAll(dp => dp.m_dataModule.Equals(cont));
        }

        public List<DataPage> GetPartPages(Part part)
        {
            return m_scienceDatas.FindAll(dp => dp.m_partModule.part == part);
        }


        WhichData m_controller;
        int m_index;


        class DataProcessor<T> where T : class
        {
            public List<T> m_queue = new List<T>();
            Action<T> m_step1;
            Func<T, bool> m_poll2;
            Action<T> m_step3;
            public Action m_end4;

            bool m_polling = false;

            public DataProcessor(Action<T> step1, Func<T, bool> poll2, Action<T> step3, Action end)
            {
                m_step1 = step1;
                m_poll2 = poll2;
                m_step3 = step3;
                m_end4 = end;
            }

            public void Process(List<T> datas)
            {
                m_queue.AddRange(datas);
            }

            public void Update()
            {
                while (m_queue.Count > 0)
                {
                    T dp = m_queue.First();

                    //step1 called once
                    if (!m_polling)
                    {
                        if (m_step1 != null) { m_step1(dp); }

                        m_polling = true;
                    }
                    //poll2 spins
                    else if (!m_poll2(dp))
                    {
                        return;
                    }
                    //step3 called once, and we loop
                    else
                    {
                        m_polling = false;

                        if (m_step3 != null) { m_step3(dp); }

                        m_queue.RemoveAt(0);

                        //step4 if we've finished the queue
                        if (m_queue.Count == 0 && m_end4 != null) { m_end4(); }
                    }
                }
            }
        }

        //async clean
        DataProcessor<ModuleScienceExperiment> m_cleanDataQueue;
        ScreenMessage m_scrnMsg = null;
        public void ProcessCleanDatas(List<ModuleScienceExperiment> cleans)
        {
            //want to work in series, even on step 1
            m_cleanDataQueue.Process(cleans);
        }

        const float m_cleanDuration = 10f; //timed off of real lab & goo
        float m_cleanStartTime = 0f;
        void CleanStart(ModuleScienceExperiment e)
        {
            m_cleanStartTime = (float)m_ship.missionTime;
                                                         //muted lime green
            m_scrnMsg = ScreenMessages.PostScreenMessage("<color=#60a000ff>Cleaning out <i>" + e.part.partInfo.title + "</i>... [XX%]</color>", 3600f, ScreenMessageStyle.UPPER_LEFT);
        }
        bool CleanPoll(ModuleScienceExperiment e)
        {
            if (m_ship.missionTime < m_cleanStartTime + m_cleanDuration)
            {
                //wait on hack timer (thanks ksp for yet another function that doesnt work)
                float percDone = ((float)m_ship.missionTime - m_cleanStartTime) / m_cleanDuration;
                int num = (int)(percDone * 100);
                int percIndex = m_scrnMsg.message.Length - 12;
                m_scrnMsg.message = m_scrnMsg.message.Remove(percIndex, 2).Insert(percIndex, num.ToString("D2"));
                return false;
            }
            else if (m_cleanStartTime != 0f)
            {
                //complete, so cycle the container              //bright lime green
                m_scrnMsg.message = "<color=#a0ff00ff><b>Clean out on <i>" + e.part.partInfo.title + "</i> complete.</b></color>";
                m_cleanStartTime = 0f;
                e.ResetExperiment();
                return false;
            }
            else
            { return !e.Inoperable; }

        }
        void CleanEnd(ModuleScienceExperiment e)
        {
            ScreenMessages.RemoveMessage(m_scrnMsg);
            m_scrnMsg = null;
            FireScienceEvent();
        }


        //async discard 
        DataProcessor<DataPage> m_discardDataQueue;
        public void ProcessDiscardDatas(List<DataPage> discards, Action endStep)
        {
            m_discardDataQueue.m_end4 = endStep; //allows us to use custom callbacks
            //remove data from parts
            //move step 1:
            //do this here to remove n data in 1 frame (using the DataProcessor hooks it would take n frames)
            discards.ForEach(dp => dp.DiscardModuleData()); //note helper version will properly reset experiments

            m_discardDataQueue.Process(discards);
            FireScienceEvent();
        }
        //poll2
        bool DiscardPoll(DataPage dp)
        {
            return !dp.m_dataModule.GetData().Contains(dp.m_scienceData);
        }
        //step3
        void DiscardPost(DataPage dp)
        {   //HACKJEFFGIFFEN
            Debug.Log("GA model" + m_index + " discarded " + dp.m_scienceData.subjectID);
        }
        //end of queue
        public void FireScienceEvent() { m_scienceEventOccured = true; }


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
                return true;
            }

            return false;
        }

        public bool IsPartSciContainer(Part part, out ModuleScienceContainer clickedContainer)
        {
            clickedContainer = m_containerModules.Find(sciCont => sciCont.part == part);
            return clickedContainer != null;
        }


        //async move
        DataProcessor<DataPage> m_moveDataQueue;
        ModuleScienceContainer m_moveDst = null;
        public void ProcessMoveDatas(ModuleScienceContainer dst, List<DataPage> sources, Action endStep)
        {
            m_moveDst = dst;
            m_moveDataQueue.m_end4 = endStep; //allows us to use custom callbacks

            //move step 1:
            //do this here to remove n data in 1 frame (using the DataProcessor hooks it would take n frames)
            sources.ForEach(dp => dp.m_dataModule.DumpData(dp.m_scienceData)); //note direct dump version, so experiments unusable after
            m_moveDataQueue.Process(sources);
            FireScienceEvent();
        }
        //step 2 is DiscardPoll
        //step3
        void MoveAdd(DataPage dp)
        {
            Debug.Log("GA model" + m_index + " moved " + dp.m_scienceData.subjectID);
            m_moveDst.AddData(dp.m_scienceData);
        }
        //end of queue
        public void MoveEnd()
        {
            m_moveDst = null;

            FireScienceEvent();
        }

        public ModuleScienceLab GetPrimeLab()
        {
            //HACKJEFFGIFFEN multilab concerns
            return m_labModules.First();
        }

        public float GetLabResearchPoints(ScienceData sciData)
        {
            //no labs
            if (m_labModules.Count == 0) { return 0f; }

            ModuleScienceLab lab = GetPrimeLab();

            //lab already has data
            if (lab.ExperimentData.Contains(sciData.subjectID)) { return 0f; }

            CelestialBody body = FlightGlobals.getMainBody();

            float scalar = 1f;

            //surface boost
            bool grounded = m_ship.Landed || m_ship.Splashed;
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

        DataProcessor<DataPage> m_labDataQueue;
        public void ProcessLabDatas(List<DataPage> dataPages)
        {
            m_labDataQueue.Process(dataPages);
            FireScienceEvent();
        }
        bool m_labCopying = false;
        //step1
        void LabStartCopy(DataPage dp)
        {
            //TODOJEFFGIFFEN possible to leave scene overtop of processing long time...
            Debug.Log("GA model" + m_index + " start lab copy " + dp.m_scienceData.subjectID);
            m_controller.LaunchCoroutine(
                GetPrimeLab().ProcessData(dp.m_scienceData, KSPLabEndCopy));
            m_labCopying = true;
        }
        //poll2 & ksp callback
        void KSPLabEndCopy(ScienceData sd) { m_labCopying = false; }
        bool LabIsDone(DataPage dp) { return !m_labCopying; }
        //step3
        void LabPostCopy(DataPage dp)
        {
            Debug.Log("GA model" + m_index + " finish lab copy " + dp.m_scienceData.subjectID);

            //HACKJEFFGIFFEN the lab copy fails (missing the ExperiDlg), so we manually add the pts
            GetPrimeLab().dataStored += dp.m_labPts;

            m_scienceEventOccured = true;
        }
        //end queue - none


        DataProcessor<DataPage> m_transmitDataQueue;
        public void ProcessTransmitDatas(List<DataPage> datas)
        {
            m_transmitDataQueue.Process(datas);
            FireScienceEvent();
        }
        bool m_transmitting = false;
        //step1
        void TransmitSend(DataPage dp)
        {
            //TODOJEFFGIFFEN choosing radio
            //TODOJEFFGIFFEN kill during transmit concern
            Debug.Log("GA model" + m_index + " start transmit " + dp.m_scienceData.subjectID);
            m_radioModules.First().TransmitData(new List<ScienceData>() { dp.m_scienceData });
            m_transmitting = true;
            //the callback version no longer exists
            //polling IsBusy switches at end of fold down
            //so we temporarily subscribe to science recieve event as our poll2 step
            GameEvents.OnScienceRecieved.Add(KSPOnScienceRcv);
        }
        //ksp callback - verify callback is us, flag tranmsit done, and begin data dump (takes a frame)
        void KSPOnScienceRcv(float sciEarned, ScienceSubject s, ProtoVessel p, bool b) //unsure what b does
        {
            DataPage dp = m_transmitDataQueue.m_queue.First();
            if (dp.m_scienceData.subjectID == s.id)
            {
                m_transmitting = false;
                dp.m_dataModule.DumpData(dp.m_scienceData);  //note direct dump version, so experiments unusable after
            }
        }
        //poll2
        public bool TransmitIsSent(DataPage dp) { return !m_transmitting && DiscardPoll(dp); }
        //step3
        public void TransmitDiscard(DataPage dp)
        {
            Debug.Log("GA model" + m_index + " end transmit " + dp.m_scienceData.subjectID);
            GameEvents.OnScienceRecieved.Remove(KSPOnScienceRcv);

            FireScienceEvent();
        }


        //returns empty string on success, error string on failure
        public string Initialize(WhichData controller, int i)
        {
            Debug.Log("GA model" + i + " initialize");
            string errorMsg = string.Empty;

            m_controller = controller;
            m_index = i;

            m_cleanDataQueue = new DataProcessor<ModuleScienceExperiment>(CleanStart, CleanPoll, CleanEnd, null);
            m_discardDataQueue = new DataProcessor<DataPage>(null, DiscardPoll, DiscardPost, null);
            m_moveDataQueue = new DataProcessor<DataPage>(null, DiscardPoll, MoveAdd, MoveEnd);
            m_labDataQueue = new DataProcessor<DataPage>(LabStartCopy, LabIsDone, LabPostCopy, null);
            m_transmitDataQueue = new DataProcessor<DataPage>(TransmitSend, TransmitIsSent, TransmitDiscard, null);

            return errorMsg;
        }

        public string GetShipString()
        {
            return ShipString(m_ship);
        }
        public static string ShipString(Vessel v)
        {
            return "[ " + v.vesselName + " ]";
        }
        public string GetStatusString()
        {
            string header = string.Empty;
            //skip discard and move, they're 2 frame operations anyway
            if (m_labDataQueue.m_queue.Count > 0) { header += ", " + m_labDataQueue.m_queue.Count + " lab copying"; }
            if (m_transmitDataQueue.m_queue.Count > 0) { header += ", " + m_transmitDataQueue.m_queue.Count + " transmitting"; }

            return header;
        }

        public void CancelDataQueues()
        {
            //TODOJEFFGIFFEN
            //HACKJEFFGIFFEN undo the queued data operations
            /*m_discardDataQueue;
            //async move
            m_moveDataQueue;
            m_moveDst = null;

            m_labDataQueue;
            m_labCopying = false;

            m_transmitDataQueue;
            m_transmitting = false;
            */
        }

        public void ResetData()
        {
            Debug.Log("GA model" + m_index + " reset");

            CancelDataQueues();

            m_sciDataModules.Clear();
            m_experiModules.Clear();
            m_containerModules.Clear();
            m_disabledExperiModules.Clear();
            m_labModules.Clear();
            m_radioModules.Clear();
            m_scienceDatas.Clear();
            m_researchIDs.Clear();
            m_experiScienceDatas.Clear();
            m_habitableParts.Clear();
            m_crewMembers.Clear();
            m_scientistAboard = false;

            m_partEventOccured = m_crewEventOccured = m_scienceEventOccured = false;
            m_flags.Clear();

            m_ship = null;
        }

        public void SwitchVessel(Vessel ship)
        {
            if ( m_ship == ship ) { return; }

            Debug.Log("GA model" + m_index + " switchvessel to " + ship.ToString());

            CancelDataQueues();

            //ScanParts will clear our lists

            //data based off global events
            m_partEventOccured = m_crewEventOccured = m_scienceEventOccured = false;
            m_flags.Clear();

            //all set - reinit
            m_ship = ship;

            EnsureHelperModules(m_ship);

            ScanParts();
            ScanDatas();
            ScanCrew();
        }

        public void Update()
        {
            if (ExperimentsResultDialog.Instance != null)
            {
                GameObject.Destroy(ExperimentsResultDialog.Instance.gameObject); //dead next frame
            }

            m_cleanDataQueue.Update();
            m_discardDataQueue.Update();
            m_moveDataQueue.Update();
            m_labDataQueue.Update();
            m_transmitDataQueue.Update();

            if (m_partEventOccured) { ScanParts(); }
            //part change can alter the sci/crew payload
            if (m_partEventOccured || m_scienceEventOccured) { ScanDatas(); }
            if (m_partEventOccured || m_crewEventOccured) { ScanCrew(); }

            m_partEventOccured = m_scienceEventOccured = m_crewEventOccured = false;
        }

        public static void EnsureHelperModules(Vessel ship)
        {
            //could use module manager for the ship portion of this, but that doesnt catch the kerbal eva case
            //uniform solution this way
            int contFound = 0, contAdded = 0;
            int experiFound = 0, experiAdded = 0;

            foreach (Part p in ship.Parts)
            {
                //FindPartModulesImplementing x4 seemed overkill
                List<ModuleScienceContainer> containerModules = new List<ModuleScienceContainer>();
                List<ModuleScienceExperiment> experiModules = new List<ModuleScienceExperiment>();

                List<ModuleContainerHelper> containerHelperModules = new List<ModuleContainerHelper>();
                List<ModuleExperimentHelper> experiHelperModules = new List<ModuleExperimentHelper>();

                foreach (PartModule pm in p.Modules)
                {
                    if (pm is ModuleScienceContainer) { containerModules.Add(pm as ModuleScienceContainer); }
                    else if (pm is ModuleScienceExperiment) { experiModules.Add(pm as ModuleScienceExperiment); }
                    else if (pm is ModuleContainerHelper) { containerHelperModules.Add(pm as ModuleContainerHelper); }
                    else if (pm is ModuleExperimentHelper) { experiHelperModules.Add(pm as ModuleExperimentHelper); }
                }

                //ensure helpers are paired up, then discount modules already helped
                foreach (ModuleContainerHelper helper in containerHelperModules)
                {
                    helper.Start(); //real pairing 1st time, harmless to repeat after
                    if (containerModules.Remove(helper.m_module)) { contFound += 1; }
                }

                foreach (ModuleExperimentHelper helper in experiHelperModules)
                {
                    helper.Start();
                    if (experiModules.Remove(helper.m_module)) { experiFound += 1; }
                }

                //any remaining modules need new helpers
                contAdded += containerModules.Count;
                containerModules.ForEach(c => c.part.AddModule("ModuleContainerHelper"));

                experiAdded += experiModules.Count;
                experiModules.ForEach(c => c.part.AddModule("ModuleExperimentHelper"));
            }

            string report = string.Empty;
            if (contFound + experiFound > 0)
            {
                report += "found ";
                if (contFound > 0) { report += contFound + " cont "; }
                if (experiFound > 0) { report += experiFound + " experi "; }
            }
            if (contAdded + experiAdded > 0)
            {
                report += "added ";
                if (contAdded > 0) { report += contAdded + " cont "; }
                if (experiAdded > 0) { report += experiAdded + " experi "; }
            }

            Debug.Log("GA ensured helpers '" + ship.vesselName + "' " + report);
        }

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

        static void ExploreShipKSPEvents(Vessel ship)
        {
            Debug.Log("GA ship event list");
            foreach (Part p in ship.Parts)
            {
                Debug.Log("GA " + p.ToString());
                foreach (PartModule m in p.GetComponents<PartModule>())
                {
                    Debug.Log("GA - " + m.ToString());
                    m.Events.ForEach(e => Debug.Log("GA - - " + e.GUIName +
                        " active " + e.active +
                        " evaOnly " + e.externalToEVAOnly +
                        " guiActive " + e.guiActive +
                        " guiActiveUnfocused " + e.guiActiveUnfocused));
                }
            }
        }
    }
}
