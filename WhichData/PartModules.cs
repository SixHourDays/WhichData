using System.Collections.Generic;
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

        //events & modules need a bit to setup, so do this after setups complete.
        public virtual void Start()
        {
            List<Helper> helpers = part.FindModulesImplementing<Helper>();
            List<T> modules = part.FindModulesImplementing<T>();
            int pairIndex = helpers.FindIndex(h => h.Equals(this));
            m_module = modules[pairIndex];

            pairIndex += 1;
            Debug.Log("GA Helper " + pairIndex + "/" + helpers.Count + " for " + m_module.name);

            m_stockCollect = m_module.Events["CollectDataExternalEvent"];
            m_stockReview = m_module.Events["ReviewDataEvent"];

            m_wrapCollect = Events["CollectWrapper"];

            m_review = Events["ReviewData"];

            //copy the radius from the orignial
            m_wrapCollect.unfocusedRange = m_stockCollect.unfocusedRange;
        }

        //defaults
        //active = true, guiName = "funcName", guiActive = false, guiActiveUnfocused = false, externalToEVAOnly = true, unfocusedRange = ??
        [KSPEvent(guiActiveUnfocused = true)]
        public void CollectWrapper()
        {
            m_stockCollect.Invoke();
            WhichData.instance.OnEvaScienceMove();
        }

        [KSPEvent(guiActive = true)]
        public void ReviewData()
        {
            //dont pass along to stock event, we've no use for the ERD to popup just to be killed again
            WhichData.instance.OnReviewData(m_module);
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
        //no store on experis

        public override void Start()
        {
            if (m_module != null) { return; }

            //Debug.Log("GA Experiment Helper " + pairIndex + "/" + helpers.Count + " for " + m_module.name);
            base.Start();

            //disguise our wrappers with real event names
            m_review.guiName = m_stockReview.GUIName;
            m_wrapCollect.guiName = m_stockCollect.GUIName;

            //eva-kerb-only fixes
            if (vessel.isEVA)
            {
                //the kerb to kerb external soil & eva reports dont work anyway, so hide them
                m_module.Events["DeployExperimentExternal"].guiActiveUnfocused = false;
                //I think squad forgot to disable kerb's experi's external collect events.  The uninitialized name "Take Data" furthers the theory.
                //Pods display only a single summed Take Data n on external collect.
                //EVA kerb's also display that, but accidentally show a Take Data per experiment too.
                //So we disable the per-experiment Take Data.
                m_wrapCollect.active = false;
            }
        }

        public override void Update()
        {
            base.Update();

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

            m_wrapStore = Events["StoreWrapper"];

            m_collect = Events["CollectWhichData"];
            m_store = Events["StoreWhichData"];

            //copy the radius from the orignial
            m_wrapStore.unfocusedRange = m_collect.unfocusedRange = m_store.unfocusedRange = m_stockCollect.unfocusedRange;
        }

        //HACKJEFFGIFFEN the radius needs to be per part
        [KSPEvent(guiName = "Take Which Data...", guiActiveUnfocused = true)]
        public void CollectWhichData()
        {
            WhichData.instance.OnCollectWhichData(m_module);
        }

        [KSPEvent(guiActiveUnfocused = true)]
        public void StoreWrapper()
        {
            m_stockStore.Invoke();
            WhichData.instance.OnEvaScienceMove();
        }

        [KSPEvent(guiName = "Store Which Data...", guiActiveUnfocused = true)]
        public void StoreWhichData()
        {
            WhichData.instance.OnStoreWhichData(m_module);
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
