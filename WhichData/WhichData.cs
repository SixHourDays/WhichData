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
    public class ModuleWhichDataContainer : PartModule
    {
        public override void OnAwake()
        {
            Debug.Log("GA ModuleWhichDataContainer");
//            Events["CollectTURBOTACOSEvent"].active = true;
//            Events["StoreTURBOTACOSEvent"].active = true;
        }
#error
        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = false, externalToEVAOnly = false, unfocusedRange = 3f, guiName = "Collect TACOS")]
        public void CollectTACOSEvent()//DataExternalEvent()
        {
            Events["CollectTURBOTACOSEvent"].active = true;
            Debug.Log("GA Collect TACOS!");
        }

        [KSPEvent(active = true, guiActive = true, guiActiveUnfocused = false, externalToEVAOnly = false, unfocusedRange = 3f, guiName = "Store TACOS")]
        public void StoreTACOSEvent()//DataExternalEvent();
        {
            Debug.Log("GA Store TACOS!");
        }

        [KSPEvent(active = true, guiActive = false, guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 3f, guiName = "Collect Extern TACOS")]
        public void CollectTURBOTACOSExternalEvent()//DataExternalEvent()
        {
            Debug.Log("GA Collect TURBO TACOS!");
        }

        [KSPEvent(active = true, guiActive = false, guiActiveUnfocused = true, externalToEVAOnly = true, unfocusedRange = 3f, guiName = "Store Extern TACOS")]
        public void StoreTURBOTACOSExternalEvent()//DataExternalEvent();
        {
            Debug.Log("GA Store TURBO TACOS!");
        }

        public override void OnUpdate()
        {
        }
        /*
         * Called when the game is loading the part information. It comes from: the part's cfg file,
         * the .craft file, the persistence file, or the quicksave file.
         */
        public override void OnLoad(ConfigNode node)
        {
        }

        /*
         * Called when the game is saving the part information.
         */
        public override void OnSave(ConfigNode node)
        {
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
        
        public bool m_dirtyPages = false;
        public List<DataPage> scienceDatas => m_shipModel.m_scienceDatas;
        public List<DataPage> m_selectedPages = new List<DataPage>();
        bool m_RnDready = false;

        public void OnGUI()
        {
            if (m_RnDready)
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

        public void Update()
        {
            if (!m_RnDready)
            {
                while (ResearchAndDevelopment.Instance == null)
                {
                    return;
                }
                Debug.Log("GA unblocked by R&D, first Update");

                m_RnDready = true;
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
                            List<DataPage> moveablePages = m_selectedPages.FindAll(dp => dp.m_dataModule != dstCont);
                            //partial selection: discard repeats wrt container
                            if (!dst.allowRepeatedSubjects) { moveablePages.RemoveAll(dp => dst.HasData(dp.m_scienceData)); }

                            m_shipModel.ProcessMoveDatas(dst, moveablePages);

                            m_selectedPages.Clear();
                        }
                    }
                }

                //regardless of how we end, cleanup picking mode
                if (endPicking)
                {
                    m_shipModel.UnhighlightContainers();
                    m_GUIView.DisableScreenMessage();

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
                    m_selectedPages.Clear();
                    //HACKJEFFGIFFEN the selected need preserved across this
                    m_dirtyPages = true; //new pages means we need to resort
                }

                //view will propogate info
                m_GUIView.Update();

                //HACKJEFFGIFFEN move to model ERD spawns tell us what to highlight
                if (ExperimentsResultDialog.Instance != null)
                {
                    ExperimentsResultDialog erd = ExperimentsResultDialog.Instance;
                    Debug.Log("GA ERD pages " + erd.pages.Count);
                    List<DataPage> erdSelect = m_shipModel.m_scienceDatas.FindAll(dp=> erd.pages.Any(
                        dlgpg=> dp.Equals(dlgpg) ) 
                    );

                    //override selection
                    m_GUIView.Select(erdSelect);

                    //this really should go in the model, but I need GameObject.Destroy
                    Destroy(ExperimentsResultDialog.Instance.gameObject); //dead next frame
                }

                //remainder is calc & push data, if there is any
                if (m_shipModel.m_scienceDatas.Count > 0)
                {
                    //get selection from view    
                    if (m_GUIView.m_dirtySelection)
                    {
                        Debug.Log("GA rebuild controller selection");
                        //keep selected DataPage list
                        m_selectedPages = m_GUIView.selectedDataPages;

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
                        m_shipModel.ProcessDiscardDatas(m_selectedPages);
                    }

                    //move btn
                    if (m_GUIView.moveBtn && m_moveEnabled)
                    {
                        Debug.Log("GA control movebtn down");
                        m_state = State.Picker;
                        m_shipModel.HighlightContainers(Color.cyan);
                        m_GUIView.SetScreenMessage("Choose where to move Data, click away to cancel");
                    }

                    //lab button
                    //i think 1st is only lab that matters.  a docking of 2 together only works the 1st in the tree.
                    if (m_GUIView.labBtn && m_labEnabled)
                    {
                        //partial selection - reduce selection to labable datas
                        List<DataPage> labablePages = m_selectedPages.FindAll(pg => pg.m_labPts > 0f);
                        m_shipModel.ProcessLabDatas(labablePages);
                    }

                    //transmit button
                    if (m_GUIView.transmitBtn && m_transEnabled)
                    {
                        //TODOJEFFGIFFEN what happens on a transmit cut from power?
                        //TODOJEFFGIFFEN what happens in remotetech?
                        m_shipModel.ProcessTransmitDatas(m_selectedPages);
                    }

                } //m_dataPages.Count > 0

            } //m_state == State.Alive

        }

        public void LaunchCoroutine( IEnumerator routine ) { StartCoroutine(routine); }

        //HACKJEFFGIFFEN old below
/*
        if (m_state != State.Picker)
        {
            m_state = m_dataPages.Count > 0 ? State.Alive : State.Daemon;
        }
*/        
        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
