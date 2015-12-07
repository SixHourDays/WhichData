using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.IO;
using System.Collections;

namespace WhichData
{
    public class DataPage
    {
        //ksp data
        public ScienceData m_scienceData;
        public ScienceSubject m_subject;
        public IScienceDataContainer m_dataModule;

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

        public DataPage(ScienceData sciData, ShipModel shipModel )
        {
            m_scienceData = sciData;
            m_subject = ResearchAndDevelopment.GetSubjectByID(m_scienceData.subjectID);
            //ModuleScienceContainer and ModuleScienceExperiment subclass this
            m_dataModule = shipModel.m_sciDataParts[sciData];

            //compose data used in row display
            //displayed science in KSP is always 2x the values used in bgr
            m_fullValue = ResearchAndDevelopment.GetScienceValue(m_scienceData.dataAmount, m_subject, 1.0f);
            m_nextFullValue = ResearchAndDevelopment.GetNextScienceValue(m_scienceData.dataAmount, m_subject, 1.0f);
            m_transmitValue = ResearchAndDevelopment.GetScienceValue(m_scienceData.dataAmount, m_subject, m_scienceData.transmitValue);
            m_nextTransmitValue = ResearchAndDevelopment.GetNextScienceValue(m_scienceData.dataAmount, m_subject, m_scienceData.transmitValue);
            m_trnsWarnEnabled = (m_dataModule is ModuleScienceExperiment) && !((ModuleScienceExperiment)m_dataModule).IsRerunnable();
            m_labPts = shipModel.GetLabResearchPoints( sciData );

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
    }

    public class SortField
    {

        public string m_text;
        public bool m_guiToggle;
        public bool m_enabled;
        public Func<DataPage, DataPage, int> m_sortDlgt;

        public SortField(string title, Func<DataPage, DataPage, int> sortDlgt)
        {
            m_text = title;
            m_sortDlgt = sortDlgt;

            m_guiToggle = false; //actual HUD toggle
            m_enabled = true; //toggle locking via ranks
        }
    }

    public class RankedSorter : IComparer<DataPage>
    {
        private List<SortField> m_sortedFields = new List<SortField>(); //ranked SortFields to sort on
        public SortField GetLastSortField() { return m_sortedFields.Last(); }
        public int GetTotalRanks() { return m_sortedFields.Count; }
        public void AddSortField(SortField sf) { m_sortedFields.Add(sf); }
        public void RemoveLastSortField() { m_sortedFields.RemoveAt(m_sortedFields.Count - 1); }

        public int Compare(DataPage x, DataPage y)
        {
            // -1 < , 0 == , 1 >
            foreach (SortField sf in m_sortedFields)
            {
                int result = sf.m_sortDlgt(x, y);
                if (result != 0) { return result; }
            }

            //if all criteria returned equal, let order of list remain
            return 0;
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)] //simple enough we can just exist in flight, new instance / scene
    public class WhichData : MonoBehaviour
    {
        private ShipModel m_shipModel = new ShipModel();
        private GUIView m_GUIView = new GUIView();

        //first call after ctr, so we do init here
        public void Awake()
        {
            Debug.Log("GA WhichData::Awake");
            //initialize sub components.
            {
                string error = m_shipModel.Initialize(this);
                if (error != string.Empty)
                {
                    //They can fail, cascading a cleanup & shutdown of the mod.
                    //TODOJEFFGIFFEN teardown
                    Debug.Log("Giffen Aerospace WhichData error: " + error);
                }

                error = m_GUIView.Initialize(this);
                if (error != string.Empty)
                {
                    //They can fail, cascading a cleanup & shutdown of the mod.
                    //2x TODOJEFFGIFFEN teardown
                    Debug.Log("Giffen Aerospace WhichData error: " + error);
                }
            }

            //passthrough the Awake event
            {
                m_shipModel.OnAwake();
                m_GUIView.OnAwake();
            }
        }

        public void OnDestroy()
        {
            m_shipModel.OnDestroy();
            m_GUIView.OnDestroy();

        }
        
        

        


//HACKJEFFGIFFEN
        //generic comparer for a < b is -1, a == b is 0, a > b is 1
/*        public static int Compare<T>(T x, T y) where T : IComparable { return x.CompareTo(y); }

        //mod state
        //sortfields, lambdas dictating member to compare on
        public List<SortField> m_sortFields = new List<SortField>
        {
            new SortField("Part",           (x, y) => Compare(x.m_experi, y.m_experi)),
            new SortField("Recover Sci",    (x, y) => Compare(x.m_kspPage.scienceValue, y.m_kspPage.scienceValue)), 
            new SortField("Transm. Sci",    (x, y) => Compare(x.m_kspPage.transmitValue, y.m_kspPage.transmitValue)),
            new SortField("Mits",           (x, y) => Compare(x.m_kspPage.dataSize, y.m_kspPage.dataSize)),
            new SortField("Biome",          (x, y) => Compare(x.m_biome, y.m_biome)),
            new SortField("Situation",      (x, y) => Compare(x.m_situ, y.m_situ)),
            new SortField("Celes. Body",    (x, y) => Compare(x.m_body, y.m_body))
        };
*/
        public bool m_dirtyPages = false;
        public List<DataPage> m_dataPages = new List<DataPage>();
        public List<DataPage> m_selectedPages = new List<DataPage>();
//        public RankedSorter m_rankSorter = new RankedSorter();
//      HACKJEFFGIFFEN
        bool ready = false;

        public void OnGUI()
        {
           //HACKJEFFGIFFEN
           //if (m_state == State.Alive)
            if ( ready )
            {
                m_GUIView.OnGUI();
            }
        }

        //TODOJEFFGIFFEN pass back:
        //  toggle pushes on unlocked
        //  list picks
        //  info button pushes        
        bool m_moveEnabled = false;
        bool m_labEnabled = false;
        bool m_transEnabled = false;

        public enum State 
        {
            Daemon, //the default.  Invisibly listening for orig dialog to prompt and events
            Alive,  //daemon mode + drawing our new GUI, lisetning for button pushes
            Picker  //daemon mode + asking for a destination part pick via message and highlights, listening for clicks.
        };
        public State m_state = State.Alive;
        ScreenMessage m_scrnMsg = null;

        public void Update()
        {
            if (!ready)
            {
                while (ResearchAndDevelopment.Instance == null)
                {
                    return;
                }
                Debug.Log("GA unblocked by R&D, first Update");

                ready = true;
            }

            m_dirtyPages = false;

            m_shipModel.Update();

            ShipModel.Flags flags = m_shipModel.dirtyFlags;

            //picking
            if ( m_state == State.Picker)
            {
                bool endPicking = false;
                Vector3 screenClick = Vector3.zero;

                //TODOJEFFGIFFEN could be less safe here
                //if what we're to send, or where we're to send it, chnage - bail
                if ( flags.scienceDatasDirty || flags.sciDataModulesDirty )
                {
                    endPicking = true;
                }
                else if ( m_GUIView.HaveClicked( 0, out screenClick) )
                {
                    //if user clicked, picking ends either way
                    endPicking = true;

                    //miss means cancel
                    Part dstPart;
                    if (m_shipModel.HaveClickedPart(screenClick, out dstPart))
                    {
                        //TODOJEFFGIFFEN should be in model update
                        //non-container means cancel too
                        ModuleScienceContainer dst;
                        if (m_shipModel.IsPartSciContainer(dstPart, out dst))
                        {
                            //success - we have a dst sci container.
                            IScienceDataContainer dstCont = dst as IScienceDataContainer;

                            //partial selection: select src != dst (src == dst is effectively no-ops)
                            List<DataPage> moveablePages = m_selectedPages.FindAll(pg => pg.m_dataModule != dstCont);
                            List<ScienceData> moveableSciDatas = moveablePages.ConvertAll(pg => pg.m_scienceData);

                            //partial selection: discard repeats wrt container
                            if (!dst.allowRepeatedSubjects) { moveableSciDatas.RemoveAll(sd => dst.HasData(sd)); }

                            m_shipModel.ProcessMoveDatas(dst, moveableSciDatas);

                            m_selectedPages.Clear(); //delayed clear
                        }
                    }
                }

                //regardless of how we end, cleanup picking mode
                if (endPicking)
                {
                    m_shipModel.UnhighlightContainers();
                    //TODOJEFFGIFFEN put in view
                    ScreenMessages.RemoveMessage(m_scrnMsg);
                    m_scrnMsg = null;

                    m_state = State.Alive;
                }
            }

            //dialog alive.  note deliberately not an else - we want to catch if a dirty model cancelled picking
            if (m_state == State.Alive)
            {
                //ok so you know what changed on ship.  controller should populate GUI
                if (flags.scienceDatasDirty)
                {
                    Debug.Log("GA rebuild controller pages");
                    m_dataPages.Clear();
                    m_selectedPages.Clear();
                    //HACKJEFFGIFFEN the selected need preserved across this
                    m_shipModel.m_scienceDatas.ForEach(sciData => m_dataPages.Add(new DataPage(sciData, m_shipModel)));
                    m_dirtyPages = true; //new pages means we need to resort
                }

                //view will propogate info
                m_GUIView.Update();

                //remainder is calc & push data, if there is any
                if (m_dataPages.Count > 0)
                {
                    //get selection from view    
                    if (m_GUIView.m_dirtySelection)
                    {
                        Debug.Log("GA rebuild controller selection");
                        //keep selected DataPage list
                        m_selectedPages.Clear();
                        m_GUIView.m_selectedPages.ForEach(pg => m_selectedPages.Add(pg.m_src));

                        //newly selected means updating the view info
                        int resetable = m_selectedPages.FindAll(pg => pg.m_dataModule is ModuleScienceExperiment).Count; //number of selected that are resettable
                        int labableCount = m_selectedPages.FindAll(pg => pg.m_labPts > 0).Count; //number of selected that could lab copy

                        m_moveEnabled = (m_shipModel.m_containerModules.Count > 0 && resetable > 0); //experi result -> pod data
                        m_moveEnabled |= (m_shipModel.m_containerModules.Count > 1 && m_GUIView.m_selectedPages.Count - resetable > 0); //pod1 data -> pod2 data
                        m_labEnabled = m_shipModel.m_labModules.Count > 0 && labableCount > 0; //need lab & needs to be unique to said lab
                        m_transEnabled = m_shipModel.m_radioModules.Count > 0; //need a radio

                        m_GUIView.SetViewInfo(m_moveEnabled, m_labEnabled, m_transEnabled);
                    }

                    //HACKJEFFGIFFEN case State.Alive:
                    //              {

                    //TODOJEFFGIFFEN
                    //buttons should context sensitive - number of experi they apply to displayed like X / All.
                    //move button imagery:
                    //  onboard should be folder arrow capsule //thought, science symbol instead?
                    //  eva get should be folder arrow kerb
                    //  eva put should be folder arrow capsule
                    //buttons should either be live or ghosted, NEVER gone, NEVER move.

                    //action button handling
                    //all removers of pages & selections
                    if (m_GUIView.closeBtn)
                    {
                        //TODOJEFFGIFFEN need actual close & open ;-)
                        Debug.Log("GA close btn pushed");
                        //m_selectedPages.Clear();
                        //m_dirtySelection = true;

                        //TODOJEFFGIFFEN move this clear to view
                        //m_closeBtn = false; //clear the button click; stays on forever with no UI pump after this frame
                    }

                    //TODOJEFFGIFFEN should use reset on showReset bool
                    if (m_GUIView.discardBtn)
                    {
                        List<ScienceData> discardDatas = m_selectedPages.ConvertAll(pg => pg.m_scienceData);
                        m_shipModel.DiscardScienceDatas(discardDatas);
                        //TODOJEFFGIFFEN move this clear to view
                        //m_discardBtn = false;
                    }

                    //move btn
                    if (m_GUIView.moveBtn && m_moveEnabled)
                    {
                        Debug.Log("GA control movebtn down");
                        m_state = State.Picker;
                        m_shipModel.HighlightContainers(Color.cyan);
                        //TODOJEFFGIFFEN move to view
                        m_scrnMsg = ScreenMessages.PostScreenMessage("Choose where to move Data, click away to cancel", 3600.0f, ScreenMessageStyle.UPPER_CENTER); //one hour, then screw it
                    }

                    //lab button
                    //i think 1st is only lab that matters.  a docking of 2 together only works the 1st in the tree.
                    if (m_GUIView.labBtn && m_labEnabled)
                    {
                        //partial selection - reduce selection to labable datas
                        List<DataPage> labablePages = m_selectedPages.FindAll(pg => pg.m_labPts > 0f);
                        m_shipModel.ProcessLabDatas(labablePages);
                    }

                    if (m_GUIView.transmitBtn && m_transEnabled)
                    {
                        //TODOJEFFGIFFEN what happens on a transmit cut from power?
                        //TODOJEFFGIFFEN what happens in remotetech?
                        List<ScienceData> transmitDatas = m_selectedPages.ConvertAll(pg => pg.m_scienceData);
                        m_shipModel.ProcessTransmitDatas(transmitDatas);
                    }

                } //m_dataPages.Count > 0

            } //m_state == State.Alive

        }

        public void LaunchCoroutine( IEnumerator routine ) { StartCoroutine(routine); }

            //HACKJEFFGIFFEN old below
/*
                        //now that we've removed all we need, add new pages
                        //when dialog is spawned, steal its data and kill it
                        if (ExperimentsResultDialog.Instance != null)
                        {
                            //HACKJEFFGIFFEN subsumed basically all this
                            //get rid of original dialog
                            Destroy(ExperimentsResultDialog.Instance.gameObject); //1 frame up still...ehh
                        }
                        if (m_state != State.Picker)
                        {
                            m_state = m_dataPages.Count > 0 ? State.Alive : State.Daemon;
                        }



                        switch (m_state)
                        {
                            case State.Picker:
                            {
                                break;
                            }
                            case State.Alive:
                            {
                                //m_dataPages now holds all we need this frame, update UI
                                //list field sorting
                                {
                                    //toggle chain logic
                                    //notion of sorting fwd/back seems nice, but unclear / annoying in practice
                                    //cycling a chain of toggles means both nuisance in setup, accidents on the tail, and desire to insert midway, which we cant.
                                    //so simple, fwd only chain of sort criteria.  They will embrace the simplicity.
                                    foreach (SortField sf in m_sortFields)
                                    {
                                        if (sf.m_guiToggle == sf.m_enabled) //toggle state changed from logic
                                        {
                                            if (sf.m_enabled) //toggle off->on
                                            {
                                                m_rankSorter.AddSortField(sf);
                                                sf.m_enabled = false;
                                                sf.m_text = sf.m_text.Insert(0, m_rankSorter.GetTotalRanks().ToString() + "^"); //HACKJEFFGIFFEN shitty arrow
                                                m_dirtyPages = true;
                                            }
                                            else
                                            {
                                                //can only untoggle most recent
                                                if (sf.Equals(m_rankSorter.GetLastSortField()))
                                                {
                                                    m_rankSorter.RemoveLastSortField();
                                                    sf.m_enabled = true;
                                                    sf.m_text = sf.m_text.Remove(0, 2);
                                                    m_dirtyPages = true;
                                                }
                                                else //otherwise force toggle to stay on
                                                {
                                                    sf.m_guiToggle = true;
                                                }
                                            }
                                        }
                                    }

                                    if (m_dirtyPages)
                                    {
                                        //actual sort based on toggle ranks
                                        //ok to sort on no criteria
                                        m_dataPages.Sort(m_rankSorter);

                                        //once re-ordered, indices need updating
                                        int i = 0;
                                        m_dataPages.ForEach(page => page.m_index = i++);

                                        m_dirtyPages = false;
                                    }
                                }
                                
*/        
        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
