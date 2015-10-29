using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.IO;

namespace WhichData
{
    public class DataPage
    {
        //ksp data
        public ExperimentResultDialogPage m_kspPage;
        public ScienceSubject m_subject;
        public IScienceDataContainer m_dataModule;

        //subjectID parsed
        public string m_experi;
        public string m_body;
        public string m_situ;
        public string m_biome;

        //hud state
        public int m_index = 0; //into m_dataPages
        public bool m_rowButton = false;
        public bool m_selected = false;

        //hud display (truncated values, formatted strings etc)
        public string m_dataFieldDataMsg;
        public string m_dataFieldTrnsWarn;
        public string m_rcvrValue; //1 decimal accuracy
        public float m_rcvrPrcnt;
        public string m_rcvrFieldMsg;
        public float m_rcvrFieldBackBar;
        public string m_trnsValue; //1 decimal accuracy
        public float m_trnsPrcnt;
        public string m_trnsFieldMsg;
        public float m_trnsFieldBackBar;
        public string m_transBtnPerc; //0% when sci is 0 pts, and integer % otherwise
        public string m_labBtnData;

        //hacks 1
        public float m_sciHack = 0.5f;

        public DataPage(ExperimentResultDialogPage page)
        {
            m_kspPage = page;
            m_subject = ResearchAndDevelopment.GetSubjectByID(m_kspPage.pageData.subjectID);
            //ModuleScienceContainer and ModuleScienceExperiment subclass this
            m_dataModule = m_kspPage.host.FindModuleImplementing<IScienceDataContainer>();

            //compose data used in row display
            m_rcvrPrcnt = m_kspPage.scienceValue * m_sciHack / m_subject.scienceCap; //shows this experi's value vs max possible
            m_trnsPrcnt = m_kspPage.transmitValue * m_sciHack / m_subject.scienceCap; //TODOJEFFGIFFEN AAAAUGH why god why (always too low)
            m_rcvrValue = m_kspPage.scienceValue.ToString("F1");
            m_trnsValue = m_kspPage.transmitValue.ToString("F1");

            //compose data used in info pane (classic dialog fields)
            m_dataFieldDataMsg = "Data Size: " + m_kspPage.dataSize.ToString("F1") + " Mits";
            m_dataFieldTrnsWarn = m_kspPage.showTransmitWarning ? "Inoperable after Transmitting." : string.Empty;
            m_rcvrFieldMsg = "Recovery: +" + m_rcvrValue + " Science";
            m_rcvrFieldBackBar = 1.0f - m_subject.science / m_subject.scienceCap; //shows this experi's value when done next time vs max possible
            m_trnsFieldMsg = "Transmit: +" + m_trnsValue + " Science";
            m_trnsFieldBackBar = m_rcvrFieldBackBar * m_kspPage.xmitDataScalar;
            m_transBtnPerc = (m_trnsPrcnt >= 0.1f ? m_kspPage.xmitDataScalar * 100 : 0.0f).ToString("F0") + "%"; //if transmit sci is 0pts, then % is 0
            m_labBtnData = "+" + m_kspPage.pageData.labValue.ToString("F0");

            //parse out subjectIDs of the form (we ditch the @):
            //  crewReport@KerbinSrfLandedLaunchPad
            m_experi = m_body = m_situ = m_biome = string.Empty;

            //experiment
            string[] strings = m_kspPage.pageData.subjectID.Split('@');
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
                Debug.Log("GA ERROR no body in id " + m_kspPage.pageData.subjectID);
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
                Debug.Log("GA ERROR no situ in id " + m_kspPage.pageData.subjectID);
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
        public WhichData()
        {
            m_callCount = 0;
            Debug.Log("GA " + m_callCount++);
        }

        int m_callCount;

        //dock
        public void OnPartCouple(GameEvents.FromToAction<Part, Part> action) { Debug.Log("GA part couple"); }
        //undock, shear part off, decouple, destroy (phys parts)
        public void OnPartJointBreak(PartJoint joint) { Debug.Log("GA part joint break"); }
        //destroy (including non-phys parts)
        public void OnPartDie(Part part) { Debug.Log("GA part die"); }

        public void OnCrewBoardVessel(GameEvents.FromToAction<Part,Part> action) { Debug.Log("GA crew board"); }
        public void OnCrewEva(GameEvents.FromToAction<Part,Part> action) { Debug.Log("GA crew eva"); }
        public void OnCrewKilled(EventReport report) { Debug.Log("GA crew killed"); }
        //public static EventData<GameEvents.HostedFromToAction<ProtoCrewMember, Part>> onCrewTransferred; //lab funtions altering when 2/2?
        
        public void OnExperimentDeployed(ScienceData data) {Debug.Log("GA experi deploy");}

        //UI
        //public static EventVoid onHideUI; //should respond to this if its not forced
        //public static EventVoid onShowUI;
        //public static EventData<Part> onPartActionUICreate; //anytime right cick menu opens (mess with data name?)
        //public static EventData<Part> onPartActionUIDismiss;

        //from ModuleScienceContainer...
        //CollectDataExternalEvent
        //ReviewDataEvent
        //StoreDataExternalEvent

        /*[KSPEvent( guiName = "WHOOP" ) ]
        public void Review()
        {
            Debug.Log("GA " + m_callCount++ + " Review()");
        }
        */

        public void Awake()
        {
            Debug.Log("GA " + m_callCount++ + " Awake*()");

            GameEvents.onPartCouple.Add(OnPartCouple);
            GameEvents.onPartJointBreak.Add(OnPartJointBreak);
            GameEvents.onPartDie.Add(OnPartDie);
            
            GameEvents.onCrewBoardVessel.Add(OnCrewBoardVessel);
            GameEvents.onCrewOnEva.Add(OnCrewEva);
            GameEvents.onCrewKilled.Add(OnCrewKilled);
            //GameEvents.onCrewTransferred.Add(OnCrewTransferred);
                        
            GameEvents.OnExperimentDeployed.Add( OnExperimentDeployed );
            
        }

        public void ScanShip()
        { 
            //ModuleScienceExperiment, ModuleScienceLab, ModuleScienceContainer (pods)...all inherit from this interface
            List<IScienceDataContainer> conts = FlightGlobals.ActiveVessel.FindPartModulesImplementing<IScienceDataContainer>();

            Debug.Log("GA Vessel science containers:");
            int partCount = 0;
            foreach (IScienceDataContainer cont in conts)
            {
                PartModule partModule = cont as PartModule; //hooray for C# safe downcasting
                if (partModule != null)
                {
                    Debug.Log(partCount++ + " " + partModule.name);

                    int dataCount = 0;
                    ScienceData[] datas = cont.GetData();
                    foreach (ScienceData data in datas)
                    {
                        Debug.Log("\t" + dataCount++.ToString() + " " + data.title);
                    }
                }
            }
        }

        public void OnEnable()
        {
            Debug.Log("GA " + m_callCount++ + " OnEnable()");
        }

        public void Start()
        {
            Debug.Log("GA " + m_callCount++ + " Start()");
//Unity 4pro or 5 needed for asset bundles
/*            string absPath = "whichdataassetbundle";

            Debug.Log("app.dataPath " + Application.dataPath); //KSP_Data is where it is
            Uri uri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            Debug.Log("uri abspath " + uri.AbsolutePath);
            string absPath = Path.Combine( Path.GetDirectoryName(uri.AbsolutePath), "whichdataassetbundle" );
            absPath = absPath.Replace("\\","/"); //escape backslash
            Debug.Log("abspath " + absPath);
            

            AssetBundle gui = AssetBundle.CreateFromFile(absPath);
            if (gui)
            {
                Debug.Log("GA " + m_callCount++ + " Start()");
                GameObject proto = gui.Load("Canvas") as GameObject;
                if (proto)
                {
                    Debug.Log("GA " + m_callCount++ + " Start()");
                    m_test = Instantiate(proto) as GameObject;
                    Debug.Log("GA " + m_callCount++ + " Start()");
                }
                gui.Unload(false);
                Debug.Log("GA " + m_callCount++ + " Start()");
            }
*/
        }

        //hacks
        public float m_barMinPx = 7.0f;

        //window pixel positions etc
        //default window in 1920x1080 is at 243, 190, 413x240 (10px of grey round thinger at left)
        //500px of list pane, 413 of info pane ( -10 left grey thing -9 fat left pad of info. y+60 for 'all' button height //HACKJEFFGIFFEN
        public Rect m_window = new Rect(243, 190, 500 + 394, 240 + 64);
        public Rect m_listPaneRect = new Rect(6, 6, 500 - 6 * 2, 240 + 64 - 6 * 2); //y origin is m_padding really
        //6px border all around.  Then 14px of window title, 6px again below it to sync to orig window top.
        public Rect m_infoPaneRect = new Rect(500 + 3, 6, 391 - 3, 240 + 64 - 6 * 2); //note, top/bottom pad, 3 combined w list's 6 = 9 left pad.  no right pad (made of dropshadow).
        public int m_padding = 6;
        public int m_leftColumnWidth = 325;
        public int m_barToEndPad = 2;
        public Color m_inopWarnOrange = new Color(1.0f, 0.63f, 0.0f); //orangey gold
        public int m_listFieldMaxHeight = 22;  //height of each list row. min be 18, helps with click probability some
        
        //state from ksp
        public GUIStyle m_stylePrevPage;
        public GUIStyle m_styleDiscardButton;
        public GUIStyle m_styleKeepButton;
        public GUIStyle m_styleTransmitButton;
        public GUIStyle m_styleTooltips;
        public GUIStyle m_styleNextPage;
        public GUIStyle m_styleRfIcons;      //result field
        public GUIStyle m_styleRfText;       //
        public GUIStyle m_styleRfBackground; //
        public GUIStyle m_stylePrgBarBG;
        public GUIStyle m_stylePrgBarDarkGreen;
        public GUIStyle m_stylePrgBarLightGreen;
        public GUIStyle m_stylePrgBarDarkBlue;
        public GUIStyle m_stylePrgBarLightBlue;
        public GUIStyle m_styleLabButton;
        public GUIStyle m_styleResetButton;
        public float m_pageButtonPadding;
        public float m_pageButtonSize;
        public float m_progressBarWidth;
        public float m_rightSideWidth;
        GUISkin m_dlgSkin;

        //GUI state
        public bool m_prevBtDown = false;
        public bool m_nextBtDown = false;
        public bool m_closeBtn = false;
        public bool m_discardBtn = false;
        public bool m_moveBtn = false;
        public bool m_moveBtnEnabled = false;
        public bool m_labBtn = false;
        public bool m_labBtnEnabled = false;
        public bool m_transmitBtn = false;
        public bool m_transmitBtnEnabled = false;

        public Vector2 m_scrollPos;
        public Texture2D m_dataIcon = null;
        public Texture2D m_scienceIcon = null;
        public GUIStyle m_closeBtnStyle = new GUIStyle();
        public GUIStyle m_moveBtnStyle = new GUIStyle();

        public string m_titleBar;
        public string m_boxMsg;
        public Action m_layoutInfoPaneBody; //really just enable disable the oldschool bars at bottom of box
        public string m_labBtnMsg;
        public string m_transmitBtnMsg;


        //generic comparer for a < b is -1, a == b is 0, a > b is 1
        public static int Compare<T>(T x, T y) where T : IComparable { return x.CompareTo(y); }

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
        bool m_dirtySelection = false;
        public List<DataPage> m_selectedPages = new List<DataPage>();
        bool m_dirtyPages = false;
        public List<DataPage> m_dataPages = new List<DataPage>();
        public RankedSorter m_rankSorter = new RankedSorter();

        void OnGUI()
        {
            if (m_state == State.Alive)
            {
                int uid = GUIUtility.GetControlID(FocusType.Passive); //get a nice unique window id from system
                GUI.skin = m_dlgSkin;
                m_window = GUI.Window(uid, m_window, WindowLayout, "", HighLogic.Skin.window); //style
            }
        }



        void WindowLayout(int windowID)
        {
            //  TODOJEFFGIFFEN
            //  make transmit light blue just be 5 px back off dark blue

            GUILayout.BeginArea(m_listPaneRect/*, HighLogic.Skin.window*/);
            {
                LayoutListToggles();
                LayoutListFields();

            } GUILayout.EndArea();

            GUILayout.BeginArea(m_infoPaneRect/*, m_dlgSkin.window*/);
            {
                GUILayout.BeginVertical();
                {
                    LayoutTitleBar();

                    GUILayout.BeginHorizontal();
                    {
                        //Main left column
                        m_layoutInfoPaneBody();

                        //Rightside button column
                        LayoutActionButtons();

                    } GUILayout.EndHorizontal();

                } GUILayout.EndVertical();

            } GUILayout.EndArea();

            //must be last or it disables all the widgets etc
            GUI.DragWindow();
        }

        public void LayoutListToggles()
        {
            GUILayout.BeginHorizontal();
            {
                //sorter toggles
                foreach (SortField sf in m_sortFields)
                {
                    sf.m_guiToggle = GUILayout.Toggle(sf.m_guiToggle, sf.m_text, HighLogic.Skin.button); //want a ksp button not the fat rslt dlg button
                }
            } GUILayout.EndHorizontal();
        }

        public void LayoutListFields()
        {
            GUIStyle ngs = new GUIStyle(m_dlgSkin.scrollView); //HACKJEFFGIFFEN
            ngs.padding = new RectOffset(0, 0, 0, 0); //get rid of stupid left pad
            m_scrollPos = GUILayout.BeginScrollView(m_scrollPos, ngs);
            {
                GUIStyle listField = new GUIStyle(m_dlgSkin.box);
                listField.padding = new RectOffset(0, 0, 0, 0); //nerf padding
                GUIContent nothing = new GUIContent();
                Color oldColor = GUI.color;
                foreach (DataPage pg in m_dataPages)
                {
                    GUI.color = pg.m_selected ? Color.yellow : Color.white;
                    pg.m_rowButton = GUILayout.Button(nothing, listField, GUILayout.MaxHeight(m_listFieldMaxHeight));
                    Rect btRect = GUILayoutUtility.GetLastRect();
                    {
                        //experi, rec sci/%max, trns sci/%max, data mits, biome, sit, body
                        //not atm lab points, disabling
                        const int fields = 7; //skip lab pts
                        float fieldWidth = btRect.width / fields;
                        Rect walker = btRect;
                        walker.width = fieldWidth;

                        //experi
                        GUI.color = Color.white;
                        GUIStyle cliptext = new GUIStyle(m_styleRfText); //HACKJEFFGIFFEN
                        cliptext.clipping = TextClipping.Clip;
                        GUI.Label(walker, pg.m_experi, cliptext);
                        walker.x += walker.width;

                        //recvr
                        GUI.color = Color.green;
                        string recvrString = pg.m_rcvrValue + "/" + (pg.m_rcvrPrcnt * 100).ToString("F0") + "%";
                        GUI.Label(walker, recvrString, m_styleRfText);
                        walker.x += walker.width;

                        //trans
                        GUI.color = Color.cyan;
                        string trnsString = pg.m_trnsValue + "/" + (pg.m_trnsPrcnt * 100).ToString("F0") + "%";
                        GUI.Label(walker, trnsString, m_styleRfText);
                        walker.x += walker.width;

                        //data mits
                        GUI.color = Color.white;
                        GUI.Label(walker, pg.m_kspPage.dataSize + " Mits", m_styleRfText);
                        walker.x += walker.width;

                        //disabling
                        //GUI.Label(walker, pg.showTransmitWarning ? "Disbl" : "-", m_styleRfText);
                        //walker.x += walker.width;

                        //biome
                        GUI.Label(walker, pg.m_biome == string.Empty ? "Global" : pg.m_biome, m_styleRfText);
                        walker.x += walker.width;

                        //situ
                        GUI.Label(walker, pg.m_situ, m_styleRfText);
                        walker.x += walker.width;

                        //body
                        GUI.Label(walker, pg.m_body, m_styleRfText);
                        walker.x += walker.width;

                    }
                }
                GUI.color = oldColor;

            } GUILayout.EndScrollView();
        }

        public void LayoutTitleBar()
        {
            GUILayout.BeginHorizontal();
            {
                //m_prevBtDown = GUILayout.Button("", m_stylePrevPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                GUILayout.Label(m_titleBar);
                //m_nextBtDown = GUILayout.Button("", m_styleNextPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                m_closeBtn = GUILayout.Button("", m_closeBtnStyle);

            } GUILayout.EndHorizontal();
            
            GUILayout.Space(4); //HACKJEFFGIFFEN to align with the list pane's top
        }

        public void LayoutActionButtons()
        {
            GUILayout.BeginVertical(GUILayout.Width(m_rightSideWidth));
            {
                //HACKJEFFGIFFEN tooltips missing: old attempts at the tooltips...why dont you love me WHY
                //GUI.tooltip = "Discard Data";
                //GUILayout.Button(new GUIContent("", "Discard Data"), m_styleDiscardButton);
                m_discardBtn = GUILayout.Button("", m_styleDiscardButton);
                Color oldColor = GUI.color;
                GUI.color = m_moveBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_moveBtn = GUILayout.Button("", m_moveBtnStyle);
                GUI.color = m_labBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_labBtn = GUILayout.Button(m_labBtnMsg, m_styleLabButton);
                GUI.color = m_transmitBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_transmitBtn = GUILayout.Button(m_transmitBtnMsg, m_styleTransmitButton);
                GUI.color = oldColor;

            } GUILayout.EndVertical();
        }

        public void LayoutBodySingle()
        {
            GUILayout.BeginVertical(/*GUILayout.Width(m_leftColumnWidth)*/); //width of left column from orig dialog
            {
                DataPage curPg = m_selectedPages.First();
                //the skin's box GUIStyle already has the green text and nice top left align
                GUILayout.Box(m_boxMsg);

                LayoutInfoField(curPg);
                LayoutRecoverScienceBarField(curPg);
                LayoutTransmitScienceBarField(curPg);

            } GUILayout.EndVertical();
        }

        public void LayoutBodyGroup()
        {
            GUILayout.BeginVertical(/*GUILayout.Width(m_leftColumnWidth)*/); //width of left column from orig dialog
            {
                //the skin's box GUIStyle already has the green text and nice top left align
                GUILayout.Box(m_boxMsg); //HACKJEFFGIFFEN

                //LayoutInfoField(curPg);
                //LayoutRecoverScienceBarField(curPg);
                //LayoutTransmitScienceBarField(curPg);
            
            } GUILayout.EndVertical();
        }

        public void LayoutInfoField(DataPage page)
        {
            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label( m_dataIcon, m_styleRfIcons );
                GUILayout.Label( page.m_dataFieldDataMsg, m_styleRfText );
                if (page.m_dataFieldTrnsWarn != string.Empty)
                {
                    Rect textRect = GUILayoutUtility.GetLastRect();
                    TextAnchor oldAnchor = m_styleRfText.alignment;
                    Color oldColor = GUI.color;

                    Rect totalField = new Rect(m_leftColumnWidth - m_progressBarWidth - m_barToEndPad, textRect.yMin, m_progressBarWidth, textRect.height);
                    m_styleRfText.alignment = TextAnchor.MiddleRight;
                    GUI.color = m_inopWarnOrange;
                    GUI.Label( totalField, page.m_dataFieldTrnsWarn, m_styleRfText);

                    GUI.color = oldColor;
                    m_styleRfText.alignment = oldAnchor;
                     
                }

            } GUILayout.EndHorizontal();
        }

        public void LayoutRecoverScienceBarField( DataPage page )
        {
            //selecting the recover science strings & styles
            LayoutScienceBarField( page.m_rcvrFieldMsg, page.m_rcvrPrcnt, page.m_rcvrFieldBackBar, m_stylePrgBarDarkGreen, m_stylePrgBarLightGreen );
        }

        public void LayoutTransmitScienceBarField( DataPage page )
        {
            //selecting the transmit science strings & styles
            LayoutScienceBarField( page.m_trnsFieldMsg, page.m_trnsPrcnt, page.m_trnsFieldBackBar, m_stylePrgBarDarkBlue, m_stylePrgBarLightBlue );
        }

        public void LayoutScienceBarField( string text, float lightFillPrcnt, float darkFillPrcnt, GUIStyle darkBarStyle, GUIStyle lightBarStyle )
        {
             //info bars white text
            GUIContent nothing = new GUIContent();

            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label( m_scienceIcon, m_styleRfIcons );
                GUILayout.Label( text, m_styleRfText);
                Rect textRect = GUILayoutUtility.GetLastRect();                

                //HACKJEFFGIFFEN 
                Rect totalBar = new Rect(391 - m_rightSideWidth - m_progressBarWidth - 11/* - m_barToEndPad*/, textRect.yMin, m_progressBarWidth, textRect.height);
                GUI.Box( totalBar, nothing, m_stylePrgBarBG );

                //the bars need 7/130 or more to draw right (pad for the caps)
                //if the light bar is invisible, the dark bar has no point.
                float lightFillPx = lightFillPrcnt * m_progressBarWidth;
                if (lightFillPx >= m_barMinPx)
                {
                    Rect darkBar = new Rect(totalBar);
                    darkBar.width *= darkFillPrcnt;
                    GUI.Box(darkBar, nothing, darkBarStyle);

                    Rect lightBar = new Rect(totalBar);
                    lightBar.width = lightFillPx;
                    GUI.Box(lightBar, nothing, lightBarStyle);
                }

            } GUILayout.EndHorizontal();
        }

        

        public void LazyInit()
        {
            m_dlgSkin = ExperimentsResultDialog.Instance.guiSkin;
            //these are the custom styles inside the ExperimentsResultDialog
            m_stylePrevPage =           m_dlgSkin.GetStyle("prev page");
            m_styleDiscardButton =      m_dlgSkin.GetStyle("discard button");
            m_styleKeepButton =         m_dlgSkin.GetStyle("keep button");
            m_styleTransmitButton =     m_dlgSkin.GetStyle("transmit button");
            m_styleTooltips =           m_dlgSkin.GetStyle("tooltips");
            m_styleNextPage =           m_dlgSkin.GetStyle("next page");
            m_styleRfIcons =            m_dlgSkin.GetStyle("icons");
            m_styleRfText =             m_dlgSkin.GetStyle("iconstext");
            m_styleRfBackground =       m_dlgSkin.GetStyle("resultfield");
            m_stylePrgBarBG =           m_dlgSkin.GetStyle("progressBarBG");
            m_stylePrgBarDarkGreen =    m_dlgSkin.GetStyle("progressBarFill");
            m_stylePrgBarLightGreen =   m_dlgSkin.GetStyle("progressBarFill2");
            m_stylePrgBarDarkBlue =     m_dlgSkin.GetStyle("progressBarFill3");
            m_stylePrgBarLightBlue =    m_dlgSkin.GetStyle("progressBarFill4");
            m_styleLabButton =          m_dlgSkin.GetStyle("lab button");
            m_styleResetButton =        m_dlgSkin.GetStyle("reset button");

            m_pageButtonPadding = ExperimentsResultDialog.Instance.pageButtonPadding;
            m_pageButtonSize = ExperimentsResultDialog.Instance.pageButtonSize;
            m_progressBarWidth = ExperimentsResultDialog.Instance.progressBarWidth;
            m_rightSideWidth = ExperimentsResultDialog.Instance.rightSideWidth;
            //m_tooltipOffset = ExperimentsResultDialog.Instance.tooltipOffset;

            //find and use the existing textures from the orig dlg
            Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            m_dataIcon = textures.First<Texture2D>(t => t.name == "resultsdialog_datasize");
            m_scienceIcon = textures.First<Texture2D>(t => t.name == "resultsdialog_scivalue");

            m_closeBtnStyle.margin = new RectOffset(6, 6, 0, 0); //top pad from window, bottom irrelevant
            m_closeBtnStyle.fixedWidth = m_closeBtnStyle.fixedHeight = 25.0f;
            m_closeBtnStyle.normal.background = GameDatabase.Instance.GetTexture("SixHourDays/closebtnnormal", false);
            m_closeBtnStyle.hover.background = GameDatabase.Instance.GetTexture("SixHourDays/closebtnhover", false);
            m_closeBtnStyle.active.background = GameDatabase.Instance.GetTexture("SixHourDays/closebtndown", false);

            m_moveBtnStyle.margin = new RectOffset(7, 1, 2, 2);
            m_moveBtnStyle.fixedWidth = m_moveBtnStyle.fixedHeight = 55.0f;
            m_moveBtnStyle.normal.background = GameDatabase.Instance.GetTexture("SixHourDays/movebtnnormal", false);
            m_moveBtnStyle.hover.background = GameDatabase.Instance.GetTexture("SixHourDays/movebtnhover", false);
            m_moveBtnStyle.active.background = GameDatabase.Instance.GetTexture("SixHourDays/movebtndown", false);

            //CelestialBody minmus = ScaledSpace.Instance.gameObject.transform.FindChild("Minmus").GetComponent<CelestialBody>();
            /*Debug.Log("GA " + m_callCount++ + " bodies");
            List<CelestialBody> bodies = FlightGlobals.Bodies;
            foreach( CelestialBody body in bodies )
            {
                Debug.Log("GA - " + body.name);
                
                List<string> biomeTags = ResearchAndDevelopment.GetBiomeTags( body );
                foreach( string biomeTag in biomeTags )
                {
                    Debug.Log("GA - - " + biomeTag );
                }
            }

            //TODOJEFFGIFFEN  incomplete??!
            Debug.Log("GA " + m_callCount++ + " biome tags");
            List<string> situTags = ResearchAndDevelopment.GetSituationTags();
            foreach (string situTag in situTags)
            {
                Debug.Log("GA - " + situTag);
            }

            Debug.Log("GA " + m_callCount++ + " subjects");
            List<ScienceSubject> subjects = ResearchAndDevelopment.GetSubjects();
            foreach (ScienceSubject s in subjects)
            {
                Debug.Log("GA - " + s.id);
            }
            */
        }

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
            switch (m_state)
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
                        m_dataPages.ForEach(pg => pg.m_kspPage.OnKeepData(pg.m_kspPage.pageData));
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
                //lazy copy of assets from ksp on first run
                if (m_dlgSkin == null) { LazyInit(); }

                //copy result pages to our collection
                ExperimentsResultDialog.Instance.pages.ForEach( newPage => m_dataPages.Add(new DataPage(newPage)) );
                m_dirtyPages = true; //new pages means we need to resort

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

                    //list click handling
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
        public bool ProcessDiscardData(DataPage page) { page.m_kspPage.OnDiscardData(page.m_kspPage.pageData); return true; } //always delete cause always succeed
        public bool ProcessTransmitData(DataPage page) { page.m_kspPage.OnTransmitData(page.m_kspPage.pageData); return true; } //always delete cause always succeed
        public bool ProcessLabData(DataPage page)
        {
            bool labbed = false;
            //partial selection can happen for lab copies
            if (page.m_kspPage.showLabOption)
            {
                try { page.m_kspPage.OnSendToLab(page.m_kspPage.pageData); }
                catch { } //the callback tries to dismiss the murdered ExperimentsResultDialog here, can't do much but catch.

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
                ScienceData sciData = page.m_kspPage.pageData;
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

        public void OnDisable()
        {
            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }

        public void OnDestroy()
        {
            Debug.Log("GA " + m_callCount++ + " OnDestroy()");

            GameEvents.onPartCouple.Remove(OnPartCouple);
            GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
            GameEvents.onPartDie.Remove(OnPartDie);

            GameEvents.onCrewBoardVessel.Remove(OnCrewBoardVessel);
            GameEvents.onCrewOnEva.Remove(OnCrewEva);
            GameEvents.onCrewKilled.Remove(OnCrewKilled);
            //GameEvents.onCrewTransferred.Remove(OnCrewTransferred);

            GameEvents.OnExperimentDeployed.Remove(OnExperimentDeployed);
        }
    }
}
