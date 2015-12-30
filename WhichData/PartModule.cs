using UnityEngine;

namespace WhichData
{
    //A part module that always accompanies a ModuleScienceContainer
    //We deliberately hide some events, and replace them with ours
    public class ModuleWhichDataContainer : PartModule
    {
        ModuleScienceContainer m_container;
        public BaseEvent m_stockCollect;
        public BaseEvent m_stockReview;
        public BaseEvent m_stockStore;

        public BaseEvent m_wrapCollect;
        public BaseEvent m_wrapStore;

        public BaseEvent m_collect;
        public BaseEvent m_review;
        public BaseEvent m_store;

        //events & modules need a bit to setup, so do this after setups complete.
        public void Start()
        {
            Debug.Log("GA ModuleWhichDataContainer");
            m_container = part.FindModuleImplementing<ModuleScienceContainer>();

            m_stockCollect = m_container.Events["CollectDataExternalEvent"];
            m_stockReview = m_container.Events["ReviewDataEvent"];
            m_stockStore = m_container.Events["StoreDataExternalEvent"];

            m_wrapCollect = Events["CollectWrapper"];
            m_wrapStore = Events["StoreWrapper"];

            m_collect = Events["CollectWhichData"];
            m_review = Events["ReviewData"];
            m_store = Events["StoreWhichData"];

            //copy the radius from the orignial
            m_wrapCollect.unfocusedRange = m_wrapStore.unfocusedRange =
                m_collect.unfocusedRange = m_store.unfocusedRange = m_stockCollect.unfocusedRange;
        }

        //defaults
        //active = true, guiName = "funcName", guiActive = false, guiActiveUnfocused = false, externalToEVAOnly = true, unfocusedRange = ??
        [KSPEvent(guiActiveUnfocused = true)]
        public void CollectWrapper()
        {
            m_stockCollect.Invoke();
            WhichData.instance.OnEvaScienceMove();
        }

        //HACKJEFFGIFFEN the radius needs to be per part
        [KSPEvent(guiName = "Collect Which Data", guiActiveUnfocused = true)]
        public void CollectWhichData()
        {
            //TODOJEFFGIFFENWhichData.instance.OnCollectWhichData(m_container);
        }

        [KSPEvent(guiActive = true)]
        public void ReviewData()
        {
            //dont pass along to stock event, we've no use for the ERD to popup just to be killed again
            WhichData.instance.OnReviewData(m_container);
        }

        [KSPEvent(guiActiveUnfocused = true)]
        public void StoreWrapper()
        {
            m_stockStore.Invoke();
            WhichData.instance.OnEvaScienceMove();
        }

        [KSPEvent(guiName = "Store Which Data", guiActiveUnfocused = true)]
        public void StoreWhichData()
        {
            //TODOJEFFGIFFENWhichData.instance.OnStoreWhichData(m_container);
        }

        public void Update()
        {
            //hide the real events
            m_stockCollect.guiActiveUnfocused = false;
            m_stockReview.guiActive = false;
            m_stockStore.guiActiveUnfocused = false;

            //display wrappers and our events when the real events would
            m_wrapCollect.active = m_collect.active = m_stockCollect.active;
            m_review.active = m_stockReview.active;
            m_wrapStore.active = m_store.active = m_stockStore.active;

            //disguise our wrappers with real event names
            m_wrapCollect.guiName = m_stockCollect.GUIName;
            m_review.guiName = m_stockReview.GUIName;
            m_wrapStore.guiName = m_stockStore.GUIName;
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
}
