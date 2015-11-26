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
                string error = m_shipModel.Initialize();
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

        

        void HighlightPart(Part part, Color color)
        {
            //old normal based glow
            part.SetHighlightType(Part.HighlightType.AlwaysOn);
            part.SetHighlightColor( color );
            part.SetHighlight(true, false);

            //PPFX glow edge highlight
            GameObject go = part.FindModelTransform("model").gameObject;
            HighlightingSystem.Highlighter hl = go.GetComponent<HighlightingSystem.Highlighter>();
            if (hl == null) { hl = go.AddComponent<HighlightingSystem.Highlighter>(); }
            hl.ConstantOn( color );
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


        enum State 
        {
            Daemon, //the default.  Invisibly listening for orig dialog to prompt and events
            Alive,  //daemon mode + drawing our new GUI, lisetning for button pushes
            Picker  //daemon mode + asking for a destination part pick via message and highlights, listening for clicks.
        };
        State m_state = State.Daemon;
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
            //ok so you know what changed on ship.  controller should populate GUI
            if (flags.scienceDatasDirty)
            {
                m_dataPages.Clear();
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
                    //newly selected means updating the view info
                    int resetable = m_GUIView.m_selectedPages.FindAll(pg => pg.m_src.m_dataModule is ModuleScienceExperiment).Count; //number of selected that are resettable
                    int labableCount = m_GUIView.m_selectedPages.FindAll(pg => pg.m_src.m_labPts > 0).Count; //number of selected that could lab copy

                    bool moveable = (m_shipModel.m_containerModules.Count > 0 && resetable > 0); //experi result -> pod data
                    moveable |= (m_shipModel.m_containerModules.Count > 1 && m_GUIView.m_selectedPages.Count - resetable > 0); //pod1 data -> pod2 data
                    bool labable = m_shipModel.m_labModules.Count > 0 && labableCount > 0; //need lab & needs to be unique to said lab
                    bool transable = m_shipModel.m_radioModules.Count > 0; //need a radio

                    m_GUIView.SetViewInfo(moveable, labable, transable);
                }
            }


            //HACKJEFFGIFFEN old below
            /*            switch (m_state)
                        {
                            case State.Picker:
                            {
                                bool endPicking = false;
                                //HACKJEFFGIFFEN repeat of far below
                                List<ModuleScienceContainer> containerParts = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();

                                if (Input.GetMouseButtonDown(0))
                                {
                                    Ray clickRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                                    RaycastHit hit;
                                    bool strike = Physics.Raycast(clickRay, out hit, 1000.0f, 1 << 0); //1km length.  Note ship parts are on layer 0, mask needs to be 1<<layer desired
                                    if (strike)
                                    {
                                        Part clickedPart = hit.collider.gameObject.GetComponentInParent<Part>(); //the collider's gameobject is child to partmodule's gameobject

                                        //if a science container was clicked
                                        if ( containerParts.Exists(sciCont => sciCont.part == clickedPart) )
                                        {
                                            //ProcessMoveData needs to know clickedPart to skip src==dst selections
                                            //capture in a lambda, pass that instead ;-)
                                           Func<DataPage, bool> delgt = (DataPage page) => {return ProcessMoveData(page, clickedPart); };
                                            ReverseProcessSelected(delgt);

                                            endPicking = true;
                                        }
                                    }
                                }

                                if (endPicking || Input.GetKey(KeyCode.Escape))
                                {
                                    containerParts.ForEach(sciCont => UnHighlightPart(sciCont.part));
                                    ScreenMessages.RemoveMessage(m_scrnMsg);
                                    m_scrnMsg = null;

                                    m_state = State.Alive; //daemon poll below will go to daemon if nothing left
                                }
                                break;
                            }
                            case State.Alive:
                            {

                                //TODOJEFFGIFFEN
                                //buttons should context sensitive - number of experi they apply to displayed like X / All.
                                //move button imagery:
                                //  onboard should be folder arrow capsule //thought, science symbol instead?
                                //  eva get should be folder arrow kerb
                                //  eva put should be folder arrow capsule
                                //buttons should either be live or ghosted, NEVER gone, NEVER move.

                                //action button handling
                                //all removers of pages & selections
                                if (m_closeBtn)
                                {
                                    //note close means "keep" for everything
                                    m_dataPages.ForEach(pg => pg.m_kspPage.OnKeepData(pg.m_scienceData));
                                    m_dataPages.Clear();
                                    m_selectedPages.Clear();
                                    m_dirtySelection = true;

                                    m_closeBtn = false; //clear the button click; stays on forever with no UI pump after this frame
                                }

                                //HACKJEFFGIFFEN should use reset on showReset bool
                                if (m_discardBtn)
                                {
                                    ReverseProcessSelected(ProcessDiscardData);
                                    m_discardBtn = false;
                                }

                                //move btn
                                if (m_moveBtn)
                                {
                                    if (m_moveBtnEnabled)
                                    {
                                        m_state = State.Picker;

                                        m_scrnMsg = ScreenMessages.PostScreenMessage("Choose where to move Data, [Esc] to cancel", 3600.0f, ScreenMessageStyle.UPPER_CENTER); //one hour, then screw it

                                        //HACKJEFFGIFFEN repeat of far below
                                        List<ModuleScienceContainer> containerParts = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();

                                        containerParts.ForEach(sciCan => HighlightPart(sciCan.part, Color.cyan));

                                        m_moveBtn = false;
                                    }
                                    //TODOJEFFGIFFEN force ghosted
                                }

                                //lab button
                                //HACKJEFFGIFFEN
                                //i think 1st is only lab that matters.  a docking of 2 together only works the 1st in the tree.
                                //true == CAN copy
                                //ModuleScienceLab.IsLabData( FlightGlobals.ActiveVessel, pg.m_kspPage.pageData ).ToString();
                                if (m_labBtn)
                                {
                                    if (m_labBtnEnabled)
                                    {
                                        ReverseProcessSelected(ProcessLabData);
                                        m_labBtn = false;
                                    }
                                    //TODOJEFFGIFFEN force ghosted
                                }

                                if (m_transmitBtn)
                                {
                                    if (m_transmitBtnEnabled)
                                    {
                                        //TODOJEFFGIFFEN what happens on a transmit cut from power?
                                        //TODOJEFFGIFFEN what happens in remotetech?
                                        ReverseProcessSelected(ProcessTransmitData);
                                        m_transmitBtn = false;
                                    }
                                    //TODOJEFFGIFFEN force ghosted
                                }

                                break;
                            }
                            default: break;
                        }



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

                              /+  //list click handling
                                {
                                    //when there is no selection, default
                                    if (m_selectedPages.Count == 0)
                                    {
                                        m_dataPages.First().m_selected = true;
                                        m_selectedPages.Add(m_dataPages.First());

                                        m_dirtySelection = true;
                                    }

                                    //find first page with its row button clicked (or none, giving null)
                                    DataPage selectedPage = m_dataPages.Find(page => page.m_rowButton); //my first lambda EVER!  hooray

                                    if (selectedPage != null)
                                    {
                                        //unhighlight all the old
                                        m_selectedPages.ForEach(page => page.m_selected = false);

                                        //now discern whether click or altclick (note altclick requires prev regular click)
                                        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                                        {
                                            int a = m_selectedPages.First().m_index; //smallest selected index 
                                            int b = m_selectedPages.Last().m_index; //largest
                                            int c = selectedPage.m_index; //brand new index chosen
                                            int start = Math.Min(Math.Min(a, b), c);
                                            int end = Math.Max(Math.Max(a, b), c);
                                            m_selectedPages = m_dataPages.GetRange(start, end - start + 1); // inclusive range
                                        }
                                        else
                                        {
                                            m_selectedPages.Clear();
                                            m_selectedPages.Add(selectedPage);
                                        }

                                        //highlight all the new
                                        m_selectedPages.ForEach(page => page.m_selected = true);

                                        m_dirtySelection = true;
                                    }
                                }
                                +/

                                //ship part detection
                                //HACKJEFFGIFFEN observe this smarter w events
                                m_transmitBtnEnabled = FlightGlobals.ActiveVessel.FindPartModulesImplementing<IScienceDataTransmitter>().Count > 0;
                                List<ModuleScienceContainer> containerParts = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();

                                //populate info pane
                                if (m_dirtySelection)
                                {
                                    //group mode
                                    if (m_selectedPages.Count > 1)
                                    {
                                        float recoverSci = 0.0f;
                                        int resetable = 0;
                                        int labAble = 0;
                                        float labCopyData = 0.0f;
                                        float transmitAvg = 0.0f;
                                        float transmitSci = 0.0f;
                                        foreach (DataPage page in m_selectedPages)
                                        {
                                            page.m_selected = true; //newly selected pages need highlighted

                                            recoverSci += page.m_kspPage.scienceValue;

                                            if (page.m_kspPage.showReset) { resetable += 1; }
                                            if (page.m_kspPage.showLabOption)
                                            {
                                                labAble += 1;
                                                labCopyData += page.m_kspPage.pageData.labValue;
                                            }

                                            transmitAvg += page.m_kspPage.xmitDataScalar * 100.0f;
                                            transmitSci += page.m_kspPage.transmitValue;
                                        }
                                        transmitAvg /= m_selectedPages.Count;

                                        //layout info pane stats
                                        m_titleBar = m_selectedPages.Count + " Experiments Selected";

                                        m_boxMsg =
                                            recoverSci.ToString("F1") + "pts recoverable science onboard!\n" +
                                            (m_selectedPages.Count - resetable) + " can discard, " + resetable + " can reset.\n" +
                                            labAble + "/" + m_selectedPages.Count + " experiments available for lab copy.\n" +
                                            transmitSci.ToString("F1") + "pts via transmitting science.";
                                        m_layoutInfoPaneBody = LayoutBodyGroup;

                                        m_moveBtnEnabled = (resetable > 0 && containerParts.Count > 0);                               //experi result -> pod data
                                        m_moveBtnEnabled |= (m_selectedPages.Count - resetable) > 0 && containerParts.Count > 1;    //pod1 data -> pod2 data

                                        m_labBtnEnabled = labAble > 0;
                                        m_labBtnMsg = m_labBtnEnabled ? "+" + labCopyData.ToString("F0") : "0";

                                        m_transmitBtnMsg = transmitAvg.ToString("F0") + "%";
                                    }
                                    else //single mode
                                    {
                                        DataPage page = m_selectedPages.First();
                                        page.m_selected = true;

                                        m_titleBar = page.m_kspPage.title;
                                        m_boxMsg = page.m_kspPage.resultText;
                                        m_layoutInfoPaneBody = LayoutBodySingle;
                                        m_moveBtnEnabled = containerParts.Count > (page.m_kspPage.showReset ? 0 : 1); //experi result need 1 container to move to, intercontainer needs 2
                                        m_labBtnEnabled = page.m_kspPage.showLabOption;
                                        m_labBtnMsg = m_labBtnEnabled ? page.m_labBtnData : "0";
                                        m_transmitBtnMsg = page.m_transBtnPerc;
                                    }

                                    m_dirtySelection = false;
                                }

                                break;
                            }
                            default: break;
                        }
            */
            /*Debug.Log("GA dataSize " + curPg.dataSize);
                Debug.Log("GA refValue " + curPg.refValue);
                Debug.Log("GA scienceValue " + curPg.scienceValue);
                Debug.Log("GA transmitValue " + curPg.transmitValue);
                Debug.Log("GA valueRcvry " + curPg.valueAfterRecovery); //what its worth _after_ recovering this sample
                Debug.Log("GA valueTrsnmt " + curPg.valueAfterTransmit);
                Debug.Log("GA xmit " + curPg.xmitDataScalar);
                Debug.Log("GA s dataScale " + s.dataScale);
                Debug.Log("GA s science " + s.science); //science accumulated already
                Debug.Log("GA s scicap " + s.scienceCap); //max science possible here
                Debug.Log("GA s scival " + s.scientificValue); //repeat multiplier
                Debug.Log("GA s subVal " + s.subjectValue); //multiplier for situation@body
                Debug.Log("GA rd scival " + ResearchAndDevelopment.GetScienceValue( curPg.dataSize, s));
                Debug.Log("GA rd scivalxmit " + ResearchAndDevelopment.GetScienceValue( curPg.dataSize, s, curPg.xmitDataScalar));
                Debug.Log("GA rd nxtscival " + ResearchAndDevelopment.GetNextScienceValue( curPg.dataSize, s));
                Debug.Log("GA rd nxtscivalxmit " + ResearchAndDevelopment.GetNextScienceValue( curPg.dataSize, s, curPg.xmitDataScalar));
                Debug.Log("GA rd refscival " + ResearchAndDevelopment.GetReferenceDataValue( curPg.dataSize, s));
                */
        }

        //return whether to delete from m_selectedPages
        //HACKJEFFGIFFEN
        /*        public bool ProcessDiscardData(DataPage page) { page.m_kspPage.OnDiscardData(page.m_kspPage.pageData); return true; } //always delete cause always succeed
                public bool ProcessTransmitData(DataPage page)
                {
                    //HACKJEFFGIFFEN
                    //page.m_kspPage.OnTransmitData(page.m_kspPage.pageData); return true; } //always delete cause always succeed
                    List<ModuleDataTransmitter> radios = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleDataTransmitter>();
                    List<ScienceData> dataList = new List<ScienceData>();
                    dataList.Add( page.m_kspPage.pageData);
                    radios[0].TransmitData( dataList );
                    return true;
                }
                public void DonePrint(ScienceData dat) { Debug.Log("GA DonePrint: " + dat.subjectID); }
                public bool ProcessLabData(DataPage page)
                {
                    bool labbed = false;
                    //partial selection can happen for lab copies
                    if (page.m_kspPage.showLabOption)
                    {
                        //HACKJEFFGIFFEN
                        //try { page.m_kspPage.OnSendToLab(page.m_kspPage.pageData); }
                        //catch { } //the callback tries to dismiss the murdered ExperimentsResultDialog here, can't do much but catch.
                        List<ModuleScienceLab> labs = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceLab>();
                        StartCoroutine( labs[0].ProcessData( page.m_kspPage.pageData, DonePrint ) );

                        labbed = true;
                    }

                    return labbed;
                }
                public bool ProcessMoveData(DataPage page, Part dstPart)
                {
                    bool moved = false;
                    //a partial selection can contain src==dst selectees, skip em
                    if (page.m_kspPage.host != dstPart)
                    {
                        ScienceData sciData = page.m_scienceData;
                        //all destinations will have ModuleScienceContainer modules (dst cannot be experi)
                        ModuleScienceContainer dstCont = dstPart.GetComponent<ModuleScienceContainer>();

                        //when container disallows repeats, check for uniqueness
                        if (dstCont.allowRepeatedSubjects || !dstCont.HasData(sciData))
                        {
                            page.m_dataModule.DumpData(sciData);
                            dstCont.AddData(sciData);
                            dstCont.ReviewDataItem(sciData); //create new entry to replace the old //HACKJEFFGIFFEN maybe part scan catches?
                            moved = true;
                        }
                    }

                    return moved;
                }

                public void ReverseProcessSelected(Func<DataPage, bool> processDataDlgt)
                {
                    for (int i = m_selectedPages.Count - 1; i >= 0; i--)
                    {
                        DataPage page = m_selectedPages[i];
                        if (processDataDlgt(page))
                        {
                            m_dataPages.RemoveAt(page.m_index);
                            m_selectedPages.RemoveAt(i);  //and that's why we're removing selectees backwards
                        }
                    }
                    m_dirtyPages = true; //removed some, need to re-index remaining
                    m_dirtySelection = true; //need to default select, and update info pane
                }
        */
        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
