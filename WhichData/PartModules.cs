using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WhichData
{
    //A part module that always accompanies a ModuleScienceContainer/ModuleScienceExperiment
    //We deliberately hide some events, and replace them with ours
    public class ModuleScienceHelper<Helper, T> : PartModule where Helper : class where T : PartModule, IScienceDataContainer
    {
        public T m_module = null;

        public BaseEvent m_stockCollect;
        public BaseEvent m_stockReview;

        public BaseEvent m_wrapCollect;

        public BaseEvent m_review;

        //track when we've prompted an eva dialog
        //public bool m_evaDialog = false;
        public float m_evaDialogSqrRange;

        //events & modules need a bit to setup, so do this after setups complete.
        public virtual void Start()
        {
            List<Helper> helpers = part.FindModulesImplementing<Helper>();
            List<T> modules = part.FindModulesImplementing<T>();
            int pairIndex = helpers.FindIndex(h => h.Equals(this));
            m_module = modules[pairIndex];

            //pairIndex += 1;
            //Debug.Log("GA Helper " + pairIndex + "/" + helpers.Count + " for " + m_module.name);

            m_stockCollect = m_module.Events["CollectDataExternalEvent"];
            m_stockReview = m_module.Events["ReviewDataEvent"];

            m_wrapCollect = Events["ExternCollectWrapper"];

            m_review = Events["ReviewData"];

            //copy the radius from the orignial
            m_wrapCollect.unfocusedRange = m_stockCollect.unfocusedRange;
        }

        //defaults
        //active = true, guiName = "funcName", guiActive = false, guiActiveUnfocused = false, externalToEVAOnly = true, unfocusedRange = ??
        [KSPEvent(guiActiveUnfocused = true)]
        public void ExternCollectWrapper()
        {
            int oldCount = m_module.GetData().Length;
            m_stockCollect.Invoke();
            WhichData.instance.OnPMEvaCollectStore(m_module, oldCount);
        }

        [KSPEvent(guiActive = true)]
        public void ReviewData()
        {
            //dont pass along to stock event, we've no use for the ERD to popup just to be killed again
            WhichData.instance.OnPMReviewData(m_module);
        }

        public virtual void Update()
        {
            //hide the real events
            m_stockCollect.guiActiveUnfocused = false;
            m_stockReview.guiActive = false;

            //display wrappers and our events when the real events would
            m_review.active = m_stockReview.active;
        }
    }


    //ModuleScienceExperiment helper
    public class ModuleExperimentHelper : ModuleScienceHelper<ModuleExperimentHelper, ModuleScienceExperiment>
    {
        public BaseEvent m_stockExternDeploy;
        public BaseEvent m_stockReset;
        public BaseEvent m_stockExternReset;

        public BaseEvent m_wrapExternDeploy;
        public BaseEvent m_wrapReset;
        public BaseEvent m_wrapExternReset;

        //no store on experis

        public override void Start()
        {
            if (m_module != null) { return; }

            //Debug.Log("GA Experiment Helper " + pairIndex + "/" + helpers.Count + " for " + m_module.name);
            base.Start();

            m_stockExternDeploy = m_module.Events["DeployExperimentExternal"];
            m_stockReset = m_module.Events["ResetExperiment"];
            m_stockExternReset = m_module.Events["ResetExperimentExternal"];

            m_wrapExternDeploy = Events["ExternDeployWrapper"];
            m_wrapReset = Events["ResetWrapper"];
            m_wrapExternReset = Events["ExternResetWrapper"];

            //disguise our wrappers with real event names

            m_wrapCollect.guiName = m_stockCollect.GUIName;
            m_wrapExternDeploy.guiName = m_stockExternDeploy.GUIName;
            m_wrapReset.guiName = m_stockReset.GUIName;
            m_wrapExternReset.guiName = m_stockExternReset.GUIName;
            m_review.guiName = m_stockReview.GUIName;

            //copy the radius from the orignial
            m_wrapExternDeploy.unfocusedRange = m_stockExternDeploy.unfocusedRange;
            m_wrapExternReset.unfocusedRange = m_stockExternReset.unfocusedRange;
            m_evaDialogSqrRange = m_stockExternDeploy.unfocusedRange * m_stockExternDeploy.unfocusedRange; 

            //eva-kerb-only fixes
            if (vessel.isEVA)
            {
                //the kerb to kerb external soil & eva reports dont work anyway, so hide them
                m_stockExternDeploy.guiActiveUnfocused = false;
                m_wrapExternDeploy.guiActiveUnfocused = false;
                //I think squad forgot to disable kerb's experi's external collect events.  The uninitialized name "Take Data" furthers the theory.
                //Pods display only a single summed Take Data n on external collect.
                //EVA kerb's also display that, but accidentally show a Take Data per experiment too.
                //So we disable the per-experiment Take Data.
                m_wrapCollect.active = false;
            }
        }

        [KSPEvent(guiActiveUnfocused = true)]
        public void ExternDeployWrapper()
        {
            m_stockExternDeploy.Invoke();
            WhichData.instance.OnPMEvaScientistDeploy(m_module, m_evaDialogSqrRange);
        }
        [KSPEvent(guiActive = true)]
        public void ResetWrapper()
        {
            m_stockReset.Invoke();
            WhichData.instance.OnPMReset(m_module);
        }
        [KSPEvent(guiActiveUnfocused = true)]
        public void ExternResetWrapper()
        {
            m_stockExternReset.Invoke();
            WhichData.instance.OnPMEvaScientistReset(m_module);
        }

        public override void Update()
        {
            base.Update();

            //hide the real events
            m_stockExternDeploy.guiActiveUnfocused = false;
            m_stockReset.guiActive = false;
            m_stockExternReset.guiActiveUnfocused = false;

            //display wrappers when the real events would
            m_wrapExternDeploy.active = m_stockExternDeploy.active;
            m_wrapReset.active = m_stockReset.active;
            m_wrapExternReset.active = m_stockExternReset.active;

            //display wrappers and our events when the real events would
            //note eva kerbal's experiments are an exception
            if (!vessel.isEVA) { m_wrapCollect.active = m_stockCollect.active; }
        }
    }


    //ModuleScienceContainer helper
    public class ModuleContainerHelper : ModuleScienceHelper< ModuleContainerHelper, ModuleScienceContainer>
    {
        public BaseEvent m_stockStore;

        public BaseEvent m_wrapStore;

        public BaseEvent m_collect;
        public BaseEvent m_store;

        public override void Start()
        {
            if (m_module != null) { return; }

            base.Start();

            //Debug.Log("GA Container Helper " + pairIndex + "/" + helpers.Count + " for " + m_container.name);

            m_stockStore = m_module.Events["StoreDataExternalEvent"];

            m_wrapStore = Events["ExternStoreWrapper"];

            m_collect = Events["ExternCollectWhichData"];
            m_store = Events["ExternStoreWhichData"];

            //ensure CollectWrapper goes ahead of CollectWhichData in right click menu
            Events.Remove(m_wrapCollect);
            Events.Insert(Events.IndexOf(m_collect), m_wrapCollect);

            //copy the radius from the orignial
            m_wrapStore.unfocusedRange = m_collect.unfocusedRange = m_store.unfocusedRange = m_stockCollect.unfocusedRange;
            m_evaDialogSqrRange = m_collect.unfocusedRange * m_collect.unfocusedRange;

            //eva's use alternate text
            if (vessel.isEVA) { m_store.guiName = "Give Which Data..."; }
        }

        //HACKJEFFGIFFEN the radius needs to be per part
        [KSPEvent(guiName = "Take Which Data...", guiActiveUnfocused = true)]
        public void ExternCollectWhichData()
        {
            WhichData.instance.OnPMEvaCollectWhichData(m_module, m_evaDialogSqrRange);
        }

        [KSPEvent(guiActiveUnfocused = true)]
        public void ExternStoreWrapper()
        {
            int oldCount = m_module.GetData().Length;
            m_stockStore.Invoke();
            WhichData.instance.OnPMEvaCollectStore(m_module, oldCount);
        }

        [KSPEvent(guiName = "Store Which Data...", guiActiveUnfocused = true)]
        public void ExternStoreWhichData()
        {
            WhichData.instance.OnPMEvaStoreWhichData(m_module, m_evaDialogSqrRange);
        }

        public override void Update()
        {
            base.Update();

            //hide the real events
            m_stockStore.guiActiveUnfocused = false;

            //display wrappers and our events when the real events would
            m_wrapCollect.active = m_collect.active = m_stockCollect.active;
            m_wrapStore.active = m_store.active = m_stockStore.active;

            //disguise our wrappers with real event names
            m_wrapCollect.guiName = m_stockCollect.GUIName;
            m_wrapStore.guiName = m_stockStore.GUIName;
            m_review.guiName = m_stockReview.GUIName;
        }
    }
}
