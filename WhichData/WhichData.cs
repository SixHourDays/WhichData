using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public List<Func<bool>> m_rightClickEvents = new List<Func<bool>>();
        public bool m_reviewEvent = false;
        public bool m_externCollectEvent = false;
        public bool m_externStoreEvent = false;
        public bool m_externDeployEvent = false;
        public PartModule m_dialogModule = null; //track what container was rmb
        public float m_dialogSqrRange = 0f; //range to close on in externDeploy, collect, store

        public int m_blockedFrames = 0;

        public void OnGUI()
        {
            if (m_blockedFrames == 0)
            {
                switch (m_state)
                {
                    case State.Daemon: //the gui does stuff even when not drawing
                    case State.Picker: //these three states require our ship view
                    case State.Review:
                    case State.Store:
                        m_activeView.OnGUI();
                        break;
                    case State.ExternReview:
                    case State.Collect: //this requires external ship view
                        m_externView.OnGUI();
                        break;
                    default:
                        Debug.Log("GA controller OnGUI uncaught state!!!");
                        break;
                }
            }
        }


        bool m_discardEnabled = false;
        bool m_moveEnabled = false;
        bool m_labEnabled = false;
        bool m_transEnabled = false;

        public enum State
        {
            Daemon, //the default.  minimized
            Review, //new science / review of onboard science
            ExternReview, //new extern science
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
                m_reviewEvent = m_externCollectEvent = m_externStoreEvent = m_externDeployEvent = false;
                m_dialogModule = null;
                m_rightClickEvents.Clear();
            }
            else
            {
                //these events can set the extern flags below
                //any right clicks the module takes a few frames to do, we poll for here
                while( m_rightClickEvents.Count > 0 && m_rightClickEvents[0]() )
                {
                    m_rightClickEvents.RemoveAt(0);
                }

                Vessel newExternVessel = null;
                if (m_externCollectEvent) { newExternVessel = m_dialogModule.vessel; }
                if (m_externDeployEvent) { newExternVessel = m_dialogModule.vessel; }
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
            //active-ship-only updates
            if (m_activeShip.m_flags.scienceDatasDirty)
            {
                m_gatherDesc = "Gather Data Here (" + m_activeShip.m_experiScienceDatas.Count + ")";
            }
            if (m_activeShip.m_flags.disabledExperisDirty)
            {
                m_cleanDesc = "Clean Experiments (" + m_activeShip.m_disabledExperiModules.Count + ")";
            }

            //need to listen for deploy, review, and take/store, at all times
            if (m_activeShip.m_flags.experimentDeployed)
            {
                Debug.Log("GA controller EXPERI DEPLOY");
                Review(Active, m_activeShip.m_flags.newScienceDatas); //new experiments become the selection
                SwitchState(State.Review);
            }
            else if (m_reviewEvent)
            {
                Debug.Log("GA controller REVIEW DATA");
                Review(Active, m_activeShip.GetContainerPages(m_dialogModule as IScienceDataContainer)); //right clicked container's datas becomes selection
                SwitchState(State.Review);
                m_dialogModule = null;
                m_reviewEvent = false;
            }
            else if (m_externCollectEvent)
            {
                Debug.Log("GA controller COLLECT DATA");
                //highlight the right click part's sum data - container & experi modules
                Review(Extern, m_externShip.GetPartPages(m_dialogModule.part));
                m_externCollectEvent = false;
                SwitchState(State.Collect);
            }
            else if (m_externStoreEvent)
            {
                Debug.Log("GA controller STORE DATA");
                //highlight whole src ship's data
                Review(Active, m_activeShip.m_scienceDatas);
                m_externStoreEvent = false;
                SwitchState(State.Store);
            }
            else if (m_externDeployEvent)
            {
                Debug.Log("GA controller EXTERN DEPLOY");
                Review(Extern, m_externShip.GetContainerPages(m_dialogModule as ModuleScienceExperiment)); //new external experiment becomes selection
                m_externDeployEvent = false;
                SwitchState(State.ExternReview);
            }
            else //cases where we go back to daemon
            {
                //OR together reasons to close here:
                bool close = false;

                //when eva kerb floats too far from Collect/Store part
                if (m_dialogModule != null) //review case nulled out by handler above.  just externDeploy, collect, store
                {
                    Vector3 offsetToShip = m_dialogModule.part.transform.position - FlightGlobals.ActiveVessel.transform.position;
                    float sqrDst = offsetToShip.sqrMagnitude;
                    close |= sqrDst > m_dialogSqrRange;
                }

                //when ship loses its last data, so we've nothing to display
                switch (m_state)
                {
                    case State.Daemon:
                    case State.Picker:
                    case State.Review:
                    case State.Store:
                        close |= m_activeShip.m_scienceDatas.Count == 0;
                        break;
                    case State.ExternReview:
                    case State.Collect:
                        close |= m_externShip.m_scienceDatas.Count == 0;
                        break;
                }

                if (close)
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
                        m_activeShip.ProcessDiscardDatas(m_activeSelectedPages, m_activeShip.FireScienceEvent);
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
                case State.ExternReview:
                case State.Collect:
                    //kerb icon
                    UpdateExternMove(Extern, m_activeShip.m_containerModules[0], CollectEnd); //active ship is eva kerb, our dst
                    break;
                case State.Store:
                    //pod icon
                    UpdateExternMove(Active, m_dialogModule as ModuleScienceContainer, StoreEnd); //right clicked part is dst
                    break;
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

        public void UpdateExternMove(int reviewIndex, ModuleScienceContainer dst, Action endFunc)
        {
            int i = reviewIndex;
            List<DataPage> selection = m_selectedLists[i];
            GUIView view = m_guiViews[i];

            view.Update();

            //lock appropriate actions for selection that [we set to/user clicked on] the view
            if (m_selectedDirty[i] || view.m_dirtySelection)
            {
                Debug.Log("GA controller active selection button locking");

                //when user has changed selection
                if (!m_selectedDirty[i])
                {
                    selection.Clear();
                    selection.AddRange(view.selectedDataPages);
                }

                //knowing dst up front, we can screen for repeats wrt container
                if (dst.allowRepeatedSubjects) { m_moveEnabled = true; }
                else { m_moveEnabled = selection.Exists(dp => !dst.HasData(dp.m_scienceData)); }
                //scientists can additionally use discard
                m_discardEnabled = m_activeShip.m_scientistAboard;
                //in collect mode, we disallow lab and transmit
                m_labEnabled = m_transEnabled = false;

                //locks as told, and clears view dirty select flag
                view.SetViewInfo(m_discardEnabled, m_moveEnabled, m_labEnabled, m_transEnabled);
                m_selectedDirty[i] = false;
            }

            //action button handling
            if (view.closeBtn)
            {
                CloseDialog();
            }

            //TODOJEFFGIFFEN should use reset on showReset bool
            if (view.discardBtn && m_discardEnabled)
            {
                m_shipModels[i].ProcessDiscardDatas(selection, ExternDiscardEnd);
                CloseDialog();
            }

            //move btn
            if (view.moveBtn && m_moveEnabled)
            {
                //partial selection: discard repeats wrt container
                if (!dst.allowRepeatedSubjects) { selection.RemoveAll(dp => dst.HasData(dp.m_scienceData)); }

                m_shipModels[i].ProcessMoveDatas(dst, selection, endFunc);
                CloseDialog();
            }
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
                    case State.Store:
                    case State.ExternReview:
                        //they all share the dialog module...going elsewhere should clear it
                        if (newState != State.Collect
                            && newState != State.Store
                            && newState != State.ExternReview) { m_dialogModule = null; m_rightClickEvents.Clear();}
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

        private void ResetExtern()
        {
            m_externShip.ResetData(); //better to purge the model than leave it full of old state
            m_externSelectedPages.Clear();
            m_externSelectedDirty = false;
            m_externView.ResetData();
        }
        public void CloseDialog()
        {
            m_dialogModule = null;
            m_rightClickEvents.Clear();
            m_state = State.Daemon;
        }

        public void CollectEnd()
        {
            ResetExtern();
            m_activeShip.FireScienceEvent(); //pump the active model
        }

        public void StoreEnd()
        {
            m_activeShip.FireScienceEvent(); //pump the active model
        }

        public void ExternDiscardEnd()
        {
            ResetExtern();
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

        public string m_gatherDesc;
        //when experis have data to gather, and we're not on eva
        public bool gatherEnabled { get { return m_activeShip.m_experiScienceDatas.Count > 0 && !m_activeShip.m_ship.isEVA; } }

        public void OnPMGatherData(ModuleScienceContainer cont)
        {
            //HACKJEFFGIFFEN should note silent fails here
            List<DataPage> moveable = new List<DataPage>();
            //partial selection: discard repeats wrt container
            if (cont.allowRepeatedSubjects) { moveable.AddRange(m_activeShip.m_experiScienceDatas); }
            else { moveable = m_activeShip.m_experiScienceDatas.FindAll(dp => !cont.HasData(dp.m_scienceData)); }

            m_activeShip.ProcessMoveDatas(cont, moveable, m_activeShip.MoveEnd);
        }

        public string m_cleanDesc;
        //when there are disabled experis and we have a scientist (part module takes care of disabling lab case)
        public bool cleanEnabled { get { return m_activeShip.m_disabledExperiModules.Count > 0 && m_activeShip.m_scientistAboard; } }

        public void OnPMCleanExperis()
        {
            m_activeShip.ProcessCleanDatas(m_activeShip.m_disabledExperiModules);
        }

        public string GetHeaderString()
        {
            switch (m_state)
            {
                case State.Review:
                    return "Reviewing " + m_activeShip.GetShipString() + m_activeShip.GetStatusString();
                case State.ExternReview:
                    return "Reviewing " + m_externShip.GetShipString() + m_externShip.GetStatusString();
                case State.Collect:
                    return "Take from " + m_externShip.GetShipString();
                case State.Store:
                    return "Store to " + ShipModel.ShipString(m_dialogModule.vessel); //no extern model in Store
                case State.Daemon:
                case State.Picker:
                default:
                    Debug.Log("GA controller ERROR uncaught case in GetHeaderString");
                    return null;
            }
        }

        public void LaunchCoroutine( IEnumerator routine ) { StartCoroutine(routine); }


        public void OnPMEvaScientistDeploy(ModuleScienceExperiment experi, float sqrRange)
        {
            //validate the event, helper fires for all kerb professions
            if (!m_activeShip.m_scientistAboard) { return; }

            //condition to meet before we signal controller
            Func<bool> pollForDeploy = () => //experi and sqrRange get captured into the delegate
            {
                //wait for module deploy
                if (experi.GetData().Length == 0) { return false; }
                    
                //spawn dialog
                m_dialogModule = experi;
                m_dialogSqrRange = sqrRange;
                m_externDeployEvent = true;

                return true;
            };

            m_rightClickEvents.Add(pollForDeploy);
        }

        private void PMReset(ModuleScienceExperiment experi, ShipModel ship)
        {
            //condition to meet before we signal controller
            Func<bool> pollForReset = () => //experi get captured into the delegate
            {
                //wait for module to reset
                if (experi.GetData().Length > 0) { return false; }

                //alert model
                ship.FireScienceEvent();
                return true;
            };

            m_rightClickEvents.Add(pollForReset);
        }

        //onboard reset via part rigth click
        public void OnPMReset(ModuleScienceExperiment experi)
        {
            PMReset(experi, m_activeShip);
        }
        //eva scientist reset via part right click
        public void OnPMEvaScientistReset(ModuleScienceExperiment experi)
        {
            //validate the event, helper fires for all kerbs
            if (m_activeShip.m_scientistAboard) { PMReset(experi, m_externShip); }
        }

        public void OnPMEvaCollectStore(IScienceDataContainer cont, int origDataCount)
        {
            //condition to meet before we signal controller
            Func<bool> pollForMove = () => //cont get captured into the delegate
            {
                //wait for module to collect or store
                if (cont.GetData().Length == origDataCount) { return false; }

                //alert models
                m_activeShip.FireScienceEvent();
                m_externShip.FireScienceEvent();
                return true;
            };

            m_rightClickEvents.Add(pollForMove);
        }


        //only from external containers - experis offer only Take All (1)
        public void OnPMEvaCollectWhichData(ModuleScienceContainer container, float sqrRange)
        {
            m_dialogModule = container;
            m_dialogSqrRange = sqrRange;
            m_externCollectEvent = true;
        }

        //only from active ship containers or experis - no range as active
        public void OnPMReviewData(PartModule container)
        {
            m_dialogModule = container;
            m_reviewEvent = true;
        }

        //only external containers allow store
        public void OnPMEvaStoreWhichData(ModuleScienceContainer container, float sqrRange)
        {
            m_dialogModule = container;
            m_dialogSqrRange = sqrRange;
            m_externStoreEvent = true;
        }

        public void OnDisable()
        {
//            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }
    }
}
