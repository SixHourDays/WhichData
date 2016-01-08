using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WhichData
{
    [KSPAddon(KSPAddon.Startup.Flight, false)] //simple enough we can just exist in flight, new instance / scene
    public class WhichData : MonoBehaviour
    {
        private const int Active = 0;
        private const int Extern = 1;
        private const int ModelCount = 2;

        private ShipModel[] m_shipModels = new ShipModel[ModelCount];
        private ShipModel m_activeShip { get { return m_shipModels[Active]; } }
        private ShipModel m_externShip { get { return m_shipModels[Extern]; } }

        private GUIView[] m_guiViews = new GUIView[ModelCount];
        private GUIView m_activeView { get { return m_guiViews[Active]; } }
        private GUIView m_externView { get { return m_guiViews[Extern]; } }

        private List<DataPage>[] m_selectedLists = new List<DataPage>[ModelCount];
        private List<DataPage> m_activeSelectedPages { get { return m_selectedLists[Active]; } }
        private List<DataPage> m_externSelectedPages { get { return m_selectedLists[Extern]; } }

        private bool[] m_selectedDirty = new bool[ModelCount];
        private bool m_activeSelectedDirty { get { return m_selectedDirty[Active]; } set { m_selectedDirty[Active] = value; } }
        private bool m_externSelectedDirty { get { return m_selectedDirty[Extern]; } set { m_selectedDirty[Extern] = value; } }

        public static WhichData instance { get; private set; }

        public bool m_vesselSwitch = false;
        public void OnVesselSwitchEvent(Vessel arg) { m_vesselSwitch = true; }
        //keep visible ships equipped with our modules
        public void OnVesselLoaded(Vessel v) { ShipModel.EnsureHelperModules(v); }

        //first call after ctr, so we do init here
        public void Awake()
        {
            Debug.Log("GA WhichData::Awake");

            //initialize sub components.
            string error = string.Empty;
            for(int i = Active; i < ModelCount; ++i)
            {
                m_shipModels[i] = new ShipModel();
                m_guiViews[i] = new GUIView();

                error += m_shipModels[i].Initialize(this, i);
                error += m_guiViews[i].Initialize(this, i);
            }

            if (error != string.Empty)
            {
                //They can fail, cascading a cleanup & shutdown of the mod.
                //TODOJEFFGIFFEN i teardowns
                Debug.Log("Giffen Aerospace WhichData error: " + error);
                return;
            }

            for (int i = Active; i < ModelCount; ++i)
            {
                //faultless inits
                m_selectedLists[i] = new List<DataPage>();
                m_selectedDirty[i] = false;

                //passthrough the Awake events
                m_shipModels[i].OnAwake();
                m_guiViews[i].OnAwake();
            }

            GameEvents.onVesselChange.Add(OnVesselSwitchEvent);     //load/launch/quick switch
            GameEvents.onVesselLoaded.Add(OnVesselLoaded);          //when it physically loads (2.5kmish)

            instance = this;
        }

        public void OnDestroy()
        {
            instance = null;

            GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
            GameEvents.onVesselChange.Remove(OnVesselSwitchEvent);

            for (int i = Active; i < ModelCount; ++i)
            {
                m_guiViews[i].OnDestroy();
                m_shipModels[i].OnDestroy();
            }
        }

        //only active
        public IScienceDataContainer m_reviewContainer = null;
        //only extern
        public ModuleScienceContainer m_externCollectContainer = null;
        public ModuleScienceContainer m_externStoreContainer = null;
        public ModuleScienceExperiment m_externDeployExperi = null;

        public int m_blockedFrames = 0;

        public void OnGUI()
        {
            if (m_blockedFrames == 0)
            {
                switch (m_state)
                {
                    case State.Daemon: //the gui does stuff even when not drawing
                    case State.Picker: //these three states require our ship view
                    case State.Review: //HACKJEFFGIFFEN what about review of *other* ship?
                    case State.Store:
                        m_activeView.OnGUI();
                        break;
                    case State.Collect: //this requires external ship view
                        m_externView.OnGUI();
                        break;
                    default:
                        Debug.Log("GA controller OnGUI uncaught state!!!");
                        break;
                }
            }
        }

        //HACKJEFFGIFFEN dual?
        bool m_discardEnabled = false;
        bool m_moveEnabled = false;
        bool m_labEnabled = false;
        bool m_transEnabled = false;

        public enum State
        {
            Daemon, //the default.  minimized
            Review, //new science / review of onboard science
            Picker, //picking a glowing destination for onboard science moves
            Collect,//right clicked is source, eva kerbal is dst, all actions are disabled except move
            Store,  //eva kerbal is src, right clicked is dst, all actions are disabled except move
        };
        public State m_state = State.Daemon;

        public void Update()
        {
            //spin on these, we need em
            if (FlightGlobals.ActiveVessel == null || ResearchAndDevelopment.Instance == null)
            {
                m_blockedFrames += 1;
                return;
            }
            else
            {
                if (m_blockedFrames > 0)
                {
                    Debug.Log("GA controller blocked by globals for " + m_blockedFrames + " frames");
                    m_blockedFrames = 0;
                }
            }


            //potential (active and external) vessel switches
            if (m_vesselSwitch)
            {
                m_vesselSwitch = false;
                Debug.Log("GA controller active vessel switch");
                
                //reset controller back to daemon
                SwitchState(State.Daemon);

                //switch active model's subject
                m_activeShip.SwitchVessel(FlightGlobals.ActiveVessel);
                //model generates diff of the whole vessel
                //data propagation clears our selected, cleans the view, populating w new data
                m_reviewContainer = m_externCollectContainer = m_externStoreContainer = null;
                m_externDeployExperi = null;
            }
            else
            {
                Vessel newExternVessel = null;
                if (m_externCollectContainer) { newExternVessel = m_externCollectContainer.vessel; }
                if (m_externStoreContainer) { newExternVessel = m_externStoreContainer.vessel; }
                if (m_externDeployExperi) { newExternVessel = m_externDeployExperi.vessel; }
                if (newExternVessel)
                {
                    Debug.Log("GA controller extern vesel switch");
                    //set the extern model to the vessel we collect from
                    m_externShip.SwitchVessel(newExternVessel);
                }
            }

            //Active = 0, Extern = 1, ModelCount = 2
            for(int i = Active; i < ModelCount; ++i)
            {
                m_shipModels[i].Update();

                //prunes our selection lists, propagates model data to view
                DeltaData(i);
            }

            //need to listen for deploy, review, and take/store, at all times
            if (m_activeShip.m_flags.experimentDeployed)
            {
                Debug.Log("GA controller experi deploy");
                Review(Active, m_activeShip.m_flags.newScienceDatas); //new experiments become the selection
                SwitchState(State.Review);
            }
            else if (m_reviewContainer != null)
            {
                Debug.Log("GA controller review data");
                Review(Active, m_activeShip.GetContainerPages(m_reviewContainer)); //right clicked container's datas becomes selection
                SwitchState(State.Review);
                m_reviewContainer = null;
            }
            else if (m_externCollectContainer)
            {
                //for the take/store we highlight the part's sum data - container & experi modules
                Review(Extern, m_externShip.GetPartPages(m_externCollectContainer.part));
                m_externCollectContainer = null;
                SwitchState(State.Collect);
            }
            /*else if (m_storeWhichData)
            {
                Debug.Log("GA controller store Which Data");
                m_state = State.Store;
            }
            */
            //minimize when no data to display
            else
            {
                if ((m_state == State.Collect ? m_externShip : m_activeShip).m_scienceDatas.Count == 0)
                {
                    SwitchState(State.Daemon);
                }
            }


            switch (m_state)
            {
                case State.Review:
                    m_activeView.Update();

                    //lock appropriate actions for selection that [we set to/user clicked on] the view
                    if (m_activeSelectedDirty || m_activeView.m_dirtySelection)
                    {
                        Debug.Log("GA controller selection button locking");

                        //when user has changed selection
                        if (!m_activeSelectedDirty)
                        {
                            m_activeSelectedPages.Clear();
                            m_activeSelectedPages.AddRange(m_activeView.selectedDataPages);
                        }

                        //newly selected means updating the view info
                        int resetable = m_activeSelectedPages.FindAll(pg => pg.m_isExperi).Count; //number of selected that are resettable
                        int labableCount = m_activeSelectedPages.FindAll(pg => pg.m_labPts > 0).Count; //number of selected that could lab copy

                        m_discardEnabled = true; //in review, can always discard
                        m_moveEnabled = (m_activeShip.m_containerModules.Count > 0 && resetable > 0); //experi result -> pod data
                        m_moveEnabled |= (m_activeShip.m_containerModules.Count > 1 && m_activeView.m_selectedPages.Count - resetable > 0); //pod1 data -> pod2 data
                        m_moveEnabled &= !m_activeShip.m_ship.isEVA; //on eva, dissallow moving soil and eva report to container
                        m_labEnabled = m_activeShip.m_labModules.Count > 0 && labableCount > 0; //need lab & needs to be unique to said lab
                        m_transEnabled = m_activeShip.m_radioModules.Count > 0; //need a radio

                        //locks as told, and clears view dirty select flag
                        m_activeView.SetViewInfo(m_discardEnabled, m_moveEnabled, m_labEnabled, m_transEnabled);
                        m_activeSelectedDirty = false;
                    }

                    //TODOJEFFGIFFEN
                    //buttons should context sensitive - number of experi they apply to displayed like X / All.
                    //move button imagery:
                    //  onboard should be folder arrow capsule //thought, science symbol instead?


                    //action button handling
                    if (m_activeView.closeBtn)
                    {
                        m_state = State.Daemon;
                    }

                    //TODOJEFFGIFFEN should use reset on showReset bool
                    if (m_activeView.discardBtn && m_discardEnabled)
                    {
                        m_activeShip.ProcessDiscardDatas(m_activeSelectedPages);
                    }

                    //move btn
                    if (m_activeView.moveBtn && m_moveEnabled)
                    {
                        m_state = State.Picker;
                        m_activeShip.HighlightContainers(Color.cyan);
                        m_activeView.SetScreenMessage("Choose where to move Data, click away to cancel");
                    }

                    //lab button
                    if (m_activeView.labBtn && m_labEnabled)
                    {
                        //partial selection - reduce selection to labable datas
                        List<DataPage> labablePages = m_activeSelectedPages.FindAll(pg => pg.m_labPts > 0f);
                        m_activeShip.ProcessLabDatas(labablePages);
                    }

                    //transmit button
                    if (m_activeView.transmitBtn && m_transEnabled)
                    {
                        m_activeShip.ProcessTransmitDatas(m_activeSelectedPages);
                    }

                    break;
                case State.Picker:
                    {
                        bool endPicking = false;
                        Vector3 screenClick = Vector3.zero;

                        //if parts destroy & the selection is altered - bail
                        if (m_activeSelectedDirty)
                        {
                            endPicking = true;
                            //leave m_activeSelectedDirty so review updates next frame
                        }
                        else if (m_activeView.HaveClicked(0, out screenClick))
                        {
                            //if user clicked, picking ends either way
                            endPicking = true;

                            //miss means cancel
                            Part dstPart;
                            if (m_activeShip.HaveClickedPart(screenClick, out dstPart))
                            {
                                //non-container means cancel too
                                ModuleScienceContainer dst;
                                if (m_activeShip.IsPartSciContainer(dstPart, out dst))
                                {
                                    //success - we have a dst sci container.
                                    IScienceDataContainer dstCont = dst as IScienceDataContainer;

                                    //partial selection: select src != dst (src == dst are effectively no-ops)
                                    List<DataPage> moveablePages = m_activeSelectedPages.FindAll(dp => dp.m_dataModule != dstCont);
                                    //partial selection: discard repeats wrt container
                                    if (!dst.allowRepeatedSubjects) { moveablePages.RemoveAll(dp => dst.HasData(dp.m_scienceData)); }

                                    m_activeShip.ProcessMoveDatas(dst, moveablePages, m_activeShip.MoveEnd);
                                }
                            }
                        }

                        //regardless of how we end, cleanup picking mode
                        if (endPicking)
                        {
                            EndPicking();

                            m_state = State.Review;
                        }

                        break;
                    }
                case State.Collect:
                    {
                        /*
                        kerb icon
                        */

                        m_externView.Update();

                        //lock appropriate actions for selection that [we set to/user clicked on] the view
                        if (m_externSelectedDirty || m_externView.m_dirtySelection)
                        {
                            Debug.Log("GA controller extern selection button locking");

                            //when user has changed selection
                            if (!m_externSelectedDirty)
                            {
                                m_externSelectedPages.Clear();
                                m_externSelectedPages.AddRange(m_externView.selectedDataPages);
                            }

                            //in collect mode, we only allow data move.
                            m_moveEnabled = true;
                            m_discardEnabled = m_labEnabled = m_transEnabled = false;

                            //locks as told, and clears view dirty select flag
                            m_externView.SetViewInfo(m_discardEnabled, m_moveEnabled, m_labEnabled, m_transEnabled);
                            m_externSelectedDirty = false;
                        }

                        //TODOJEFFGIFFEN
                        //move button imagery:
                        //  eva get should be folder arrow kerb
                        //  eva put should be folder arrow capsule

                        //action button handling
                        if (m_externView.closeBtn)
                        {
                            EndExternMove();
                        }

                        //move btn
                        if (m_externView.moveBtn && m_moveEnabled)
                        {
                            //we know active will be our eva kerb
                            ModuleScienceContainer dst = m_activeShip.m_containerModules[0];
                            //partial selection: discard repeats wrt container
                            if (!dst.allowRepeatedSubjects) { m_externSelectedPages.RemoveAll(dp => dst.HasData(dp.m_scienceData)); }

                            //#error concern about if the lost/new queues will catch all the change from moves off ship + a ship switch
                            m_externShip.ProcessMoveDatas(dst, m_externSelectedPages, EndExternMove);
                        }

                        break;
                    }
                case State.Daemon:
                    {
                        break;
                    }
                default: break;
            }

            //done w model flags
            m_activeShip.m_flags.Clear();
            m_externShip.m_flags.Clear();
        }

        public void SwitchState(State newState)
        {
            if (m_state != newState)
            {
                switch (m_state)
                {
                    case State.Picker:
                        EndPicking();
                        break;
                    case State.Collect:
                        EndExternMove();
                        break;
                    case State.Store:
                        //EndExternCollect?
                        break;
                    case State.Daemon:
                    case State.Review:
                        //nothing but state switch
                        break;
                    default:
                        Debug.Log("GA controller SwitchState uncaught state!!!");
                        break;
                }

                m_state = newState;
            }
        }

        public void EndPicking()
        {
            m_activeShip.UnhighlightContainers();
            m_activeView.DisableScreenMessage();
        }

        public void EndExternMove()
        {
            m_externShip.ResetData(); //better to purge the model than leave it full of old state
            m_externSelectedPages.Clear();
            m_externSelectedDirty = false;
            m_externView.ResetData();

            m_activeShip.FireScienceEvent(); //pump the active model

            m_state = State.Daemon;
        }

        public void DeltaData(int i)
        {
            ShipModel model = m_shipModels[i];

            //sync model to controller and data
            if (model.m_flags.scienceDatasDirty)
            {
                Debug.Log("GA controller delta pages");

                //prune the lost science from selected
                model.m_flags.lostScienceDatas.ForEach(dp => m_selectedDirty[i] |= m_selectedLists[i].Remove(dp));
                //propogate model data to view, regardless of actually showing GUI
                m_guiViews[i].DeltaData(model.m_flags.lostScienceDatas, model.m_flags.newScienceDatas);
            }
        }

        public void Review(int index, List<DataPage> toReview)
        {
            List<DataPage> selectedPages = m_selectedLists[index];
            selectedPages.Clear();
            selectedPages.AddRange(toReview);
            m_selectedDirty[index] = true;
            m_guiViews[index].Select(selectedPages);
        }

        public void LaunchCoroutine( IEnumerator routine ) { StartCoroutine(routine); }

        public void OnEvaScienceMove()
        {
            //wholesale take/store needn't set the external model, just update active eva kerb
            m_activeShip.OnEvaScienceMove();
        }

        //only from external containers - experis offer only Take All (1)
        public void OnCollectWhichData(ModuleScienceContainer container)
        {
            m_externCollectContainer = container;
        }

        //potentially from active ship containers or experis
        public void OnReviewData(IScienceDataContainer container)
        {
            m_reviewContainer = container;
        }

        //only external containers allow store
        public void OnStoreWhichData(ModuleScienceContainer container)
        {
            m_externStoreContainer = container;
        }

        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
