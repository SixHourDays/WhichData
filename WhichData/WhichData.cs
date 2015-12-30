using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WhichData
{
    [KSPAddon(KSPAddon.Startup.Flight, false)] //simple enough we can just exist in flight, new instance / scene
    public class WhichData : MonoBehaviour
    {
        private ShipModel m_shipModel = new ShipModel();
        private GUIView m_GUIView = new GUIView();

        public static WhichData instance { get; private set; }
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

            instance = this;
        }

        public void OnDestroy()
        {
            instance = null;

            m_shipModel.OnDestroy();
            m_GUIView.OnDestroy();

        }
        
        public List<DataPage> m_selectedPages = new List<DataPage>();
        public ModuleScienceContainer m_reviewContainer = null;
        //public ModuleScienceContainer m_collectContainer = null;
        //public ModuleScienceContainer m_storeContainer = null;
        
        bool m_globalsReady = false;

        public void OnGUI()
        {
            if (m_globalsReady)
            {
                //the gui does stuff even when not drawing
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
            Daemon, //the default.  minimized
            Review, //new science / review of onboard science
            Picker, //picking a glowing destinatino for onboard science moves
          //  Collect,//right clicked is source, eva kerbal is dst, all actions are disabled except move
          //  Store,  //eva kerbal is src, right clicked is dst, all actions are disabled except move
        };
        public State m_state = State.Daemon;

        public void Update()
        {
            if (!m_globalsReady)
            {
                //spin on these, we need em
                if (FlightGlobals.ActiveVessel == null || ResearchAndDevelopment.Instance == null) { return; }

                Debug.Log("GA controller unblocked by globals, first Update");
                m_globalsReady = true;
            }

            m_shipModel.Update();

            if (m_shipModel.m_flags.vesselSwitch)
            {
                Debug.Log("GA controller vessel switch");
                m_shipModel.SwitchVessel(FlightGlobals.ActiveVessel);

                //reset controller back to daemon
                m_selectedPages.Clear();
                //m_reviewContainer = m_collectContainer = m_storeContainer = null;
                m_state = State.Daemon;

                //flip view
                m_GUIView.ResetData();
            }

            ShipModel.Flags flags = m_shipModel.m_flags;

            //sync model to controller and data
            if (flags.scienceDatasDirty)
            {   
                Debug.Log("GA controller delta pages - " + flags.lostScienceDatas.Count + " + " + flags.newScienceDatas.Count);

                //prune the lost science from selected
                flags.lostScienceDatas.ForEach(dp => m_selectedPages.Remove(dp));
                //propogate model data to view, regardless of actually showing GUI
                m_GUIView.DeltaData(flags.lostScienceDatas, flags.newScienceDatas);

                //minimize when there's no data
                if (m_shipModel.m_scienceDatas.Count == 0)
                {
                    m_state = State.Daemon;
                }
            }

            //need to listen for deploy, review, and take/store, at all times
            if (flags.experimentDeployed)
            {
                Debug.Log("GA controller experi deploy");
                //new experiments become the selection
                m_selectedPages.Clear();
                m_selectedPages.AddRange(flags.newScienceDatas);
                m_GUIView.Select(m_selectedPages);
                m_state = State.Review;
            }
            else if (m_reviewContainer)
            {
                Debug.Log("GA controller review data");
                //right clicked container's datas becomes selection
                m_selectedPages.Clear();
                m_selectedPages.AddRange( m_shipModel.GetContainerPages(m_reviewContainer) );
                m_GUIView.Select(m_selectedPages);
                m_state = State.Review;
                m_reviewContainer = null;
            }
            /*else if (m_collectContainer)
            {
                Debug.Log("GA controller collect");
                //clicked container's ship is src
                //HACKJEFFGIFFEN
                m_state = State.Collect;
            }
            else if storewhichdata
            */

            //picking
            switch (m_state)
            {
                case State.Review:
                    {
                        /*
                        evas take / store should spawn dialog in 'fixed dst/src' mode - kerb icon, no discard, no lab, no transmit.
                        NOTE you will need to run the model on the OTHER SHIP to do take mode!
                        */

                        m_GUIView.Update();
                        //remainder is calc & push data

                        //lock appropriate actions for selection that [we set to/user clicked on] the view
                        if (m_GUIView.m_dirtySelection)
                        {
                            Debug.Log("GA controller enable/disable action buttons");

                            m_selectedPages.Clear();
                            m_selectedPages.AddRange(m_GUIView.selectedDataPages);
                                
                            //newly selected means updating the view info
                            int resetable = m_selectedPages.FindAll(pg => pg.m_dataModule is ModuleScienceExperiment).Count; //number of selected that are resettable
                            int labableCount = m_selectedPages.FindAll(pg => pg.m_labPts > 0).Count; //number of selected that could lab copy

                            m_moveEnabled = (m_shipModel.m_containerModules.Count > 0 && resetable > 0); //experi result -> pod data
                            m_moveEnabled |= (m_shipModel.m_containerModules.Count > 1 && m_GUIView.m_selectedPages.Count - resetable > 0); //pod1 data -> pod2 data
                            m_labEnabled = m_shipModel.m_labModules.Count > 0 && labableCount > 0; //need lab & needs to be unique to said lab
                            m_transEnabled = m_shipModel.m_radioModules.Count > 0; //need a radio

                            m_GUIView.SetViewInfo(m_moveEnabled, m_labEnabled, m_transEnabled);
                        }

                        //TODOJEFFGIFFEN
                        //buttons should context sensitive - number of experi they apply to displayed like X / All.
                        //move button imagery:
                        //  onboard should be folder arrow capsule //thought, science symbol instead?
                        //  eva get should be folder arrow kerb
                        //  eva put should be folder arrow capsule
                        //buttons should either be live or ghosted, NEVER gone, NEVER move.

                        
                        //action button handling
                        if (m_GUIView.closeBtn)
                        {
                            m_state = State.Daemon;
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
                        
                        //we've updated to match the new state - so clear flags
                        m_shipModel.m_flags.Clear();

                        break;
                    }
                case State.Picker:
                    {
                        bool endPicking = false;
                        Vector3 screenClick = Vector3.zero;

                        //TODOJEFFGIFFEN could be less safe here
                        //if what we're to send, or where we're to send it, chnage - bail
                        if (flags.scienceDatasDirty || flags.sciDataModulesDirty)
                        {
                            endPicking = true;
                        }
                        else if (m_GUIView.HaveClicked(0, out screenClick))
                        {
                            //if user clicked, picking ends either way
                            endPicking = true;

                            //miss means cancel
                            Part dstPart;
                            if (m_shipModel.HaveClickedPart(screenClick, out dstPart))
                            {
                                //non-container means cancel too
                                ModuleScienceContainer dst;
                                if (m_shipModel.IsPartSciContainer(dstPart, out dst))
                                {
                                    //success - we have a dst sci container.
                                    IScienceDataContainer dstCont = dst as IScienceDataContainer;

                                    //partial selection: select src != dst (src == dst are effectively no-ops)
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

                            m_state = State.Review;
                        }
                        break;
                    }
                case State.Daemon:
                    {
                        //we've updated to match the new state - so clear flags
                        m_shipModel.m_flags.Clear();
                        break;
                    }
                default: break;
            }
        }

        public void LaunchCoroutine( IEnumerator routine ) { StartCoroutine(routine); }

        public void OnEvaScienceMove()
        {
            m_shipModel.OnEvaScienceMove();
        }

        public void OnReviewData(ModuleScienceContainer container)
        {
            m_reviewContainer = container;
        }
        //TODOJEFFGIFFEN
        /*public void OnCollectWhichData(ModuleScienceContainer container)
        {
            m_collectContainer = container;
        }

        public void OnStoreWhichData(ModuleScienceContainer container)
        {
            m_storeContainer = container;
        }
        */
        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
