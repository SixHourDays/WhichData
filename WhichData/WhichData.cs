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

        //subjectID parsed
        public string m_experi;
        public string m_body;
        public string m_situ;
        public string m_biome;

        //hud state
        public int m_index = 0;
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

            //compose data used in row display
            m_rcvrPrcnt = m_kspPage.scienceValue * m_sciHack / m_subject.scienceCap; //shows this experi's value vs max possible
            m_trnsPrcnt = m_kspPage.transmitValue * m_sciHack / m_subject.scienceCap; //TODOJEFFGIFFEN AAAAUGH why god why (always too low)
            m_rcvrValue = m_kspPage.scienceValue.ToString("F1");
            m_trnsValue = m_kspPage.transmitValue.ToString("F1");

            //compose data used in info pane (classic dialog fields)
            m_dataFieldDataMsg = "Data Size: " + m_kspPage.dataSize.ToString("F1") + " Mits";
            m_dataFieldTrnsWarn = m_kspPage.showTransmitWarning ? "Inoperable after Transmitting." : null;
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
            if (m_body == "")
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
            if (m_situ == "")
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

        private static int CmpStr(String a, String b) { return String.Compare(a, b); }
        private static int CmpFlt(float a, float b) { return a < b ? -1 : a == b ? 0 : 1; }
        
        //Comparators for delegation
        public delegate int SortDlgt(DataPage x, DataPage y);
        public static int CmpPart(DataPage x, DataPage y) { return CmpStr(x.m_experi, y.m_experi); }
        public static int CmpRcvrSci(DataPage x, DataPage y) { return CmpFlt(x.m_kspPage.scienceValue, y.m_kspPage.scienceValue); }
        public static int CmpTrnsSci(DataPage x, DataPage y) { return CmpFlt(x.m_kspPage.transmitValue, y.m_kspPage.transmitValue); }
        public static int CmpMits(DataPage x, DataPage y) { return CmpFlt(x.m_kspPage.dataSize, y.m_kspPage.dataSize); }
        public static int CmpBiome(DataPage x, DataPage y) { return CmpStr(x.m_biome, y.m_biome); }
        public static int CmpSitu(DataPage x, DataPage y) { return CmpStr(x.m_situ, y.m_situ); }
        public static int CmpBody(DataPage x, DataPage y) { return CmpStr(x.m_body, y.m_body); }
    }

    public class SortField
    {
        public string m_text;
        public bool m_guiToggle;
        public bool m_enabled;
        public DataPage.SortDlgt m_sortDlgt;

        public SortField( string title, DataPage.SortDlgt sortDlgt )
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

/*        void DiscardCallback(ScienceData data)
        {
            Debug.Log("GA DiscardCallback");
        }
        void KeepCallback(ScienceData data)
        {
            Debug.Log("GA KeepCallback");
        }
        void TransmitCallback(ScienceData data)
        {
            Debug.Log("GA TransmitCallback");
        }
        void SendToLabCallback(ScienceData data)
        {
            Debug.Log("GA SendToLabCallback");
        }

        public void ExperimentDeploy(ScienceData data)
        {
            Debug.Log("GA " + m_callCount++ + " ExperimentDeploy()");
        }
*/

    //public static EventData<ScienceData> OnExperimentDeployed;
    //public static EventData<float, TransactionReasons> OnScienceChanged;
    //public static EventData<float, ScienceSubject, ProtoVessel, bool> OnScienceRecieved;
        //from ModuleScienceContainer
        //CollectDataExternalEvent
        //ReviewDataEvent
        //StoreDataExternalEvent
        //future
        //onHideUI / onShowUI
        //onGUIRecoveryDialogSpawn

        /*[KSPEvent( guiName = "WHOOP" ) ]
        public void Review()
        {
            Debug.Log("GA " + m_callCount++ + " Review()");
        }
        */
        
        public void Awake()
        {
            Debug.Log("GA " + m_callCount++ + " Awake()");
            //GameEvents.OnExperimentDeployed.Add( ExperimentDeploy );
           // GameEvents.OnScienceChanged.Add( ScienceChange );
           // GameEvents.OnScienceRecieved.Add( ScienceReceive );

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
        //default window in 1920x1080 is at 243, 190, 413x240
        //we want to be 428px to the right to compare easily, and 18px taller for our window title)
        public float m_mainDlgHeight = 280 + 18;//240 + 18;
        public Rect m_window = new Rect(243/* + 428*/, 190 - 16, 913/*413*/, 280 + 18/* + 240*/); //double width, 240 + 40 more + 18 height //HACKJEFFGIFFEN
        public Rect m_myDlg = new Rect(0, /*240 + */18, 500 - 4, 280 - 4);
        public int m_windowTitleHeight = 14;
        public int m_padding = 6;
        public int m_titleWidth = 328 - 10; //HACKJEFFGIFFEN close button shoved out by 10
        public int m_leftColumnWidth = 325;
        public int m_resultTextBoxHeight = 110;
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
        public bool m_moveBtn = false;
        public bool m_discardBtn = false;
        public bool m_transmitBtn = false;
        public bool m_labBtn = false;

        public Vector2 m_scrollPos;
        public Texture2D m_dataIcon = null;
        public Texture2D m_scienceIcon = null;
        public GUIStyle m_closeBtnStyle = new GUIStyle();
        public GUIStyle m_moveBtnStyle = new GUIStyle();
    
        //mod state
        public List<SortField> m_sortFields = new List<SortField>
        {
            new SortField("Part", DataPage.CmpPart ),
            new SortField("Recover Sci", DataPage.CmpRcvrSci ), 
            new SortField("Transm. Sci", DataPage.CmpTrnsSci ),
            new SortField("Mits", DataPage.CmpMits ),
            new SortField("Biome", DataPage.CmpBiome ),
            new SortField("Situation", DataPage.CmpSitu ),
            new SortField("Celes. Body", DataPage.CmpBody )
        };
        public List<DataPage> m_selectedPages = new List<DataPage>();
        public bool m_dirtyPages = false;
        public List<DataPage> m_dataPages = new List<DataPage>();
        public RankedSorter m_rankSorter = new RankedSorter();

        void OnGUI()
        {
            if (m_dataPages.Count > 0)
            {
                int uid = GUIUtility.GetControlID(FocusType.Passive); //get a nice unique window id from system
                GUI.skin = m_dlgSkin;
                m_window = GUI.Window(uid, m_window, WindowLayout, "WhichData", HighLogic.Skin.window); //style
            }
        }

        void WindowLayout(int windowID)
        {
            //  TODOJEFFGIFFEN
            //  make transmit light blue just be 5 px back off dark blue
            //  align the top of button with field
            //  figure out how to make a ConverterResults field clickable / highlight and shit

            GUILayout.BeginArea(m_myDlg/*, HighLogic.Skin.window*/);
            {
                GUILayout.BeginHorizontal();
                {
                    //sorter toggles
                    for ( int i = 0; i < m_sortFields.Count; i++ )
                    {
                        SortField sf = m_sortFields[i];
                        sf.m_guiToggle = GUILayout.Toggle(sf.m_guiToggle, sf.m_text, HighLogic.Skin.button); //want a ksp button not the fat rslt dlg button
                    }
                } GUILayout.EndHorizontal();


                GUIStyle ngs = new GUIStyle(m_dlgSkin.scrollView); //HACKJEFFGIFFEN
                ngs.padding = new RectOffset(0, 0, 0, 0); //get rid of stupid left pad
                m_scrollPos = GUILayout.BeginScrollView(m_scrollPos, ngs);
                {
                    foreach (DataPage page in m_dataPages)
                    {
                        LayoutListField(page);
                    }
                } GUILayout.EndScrollView();

            } GUILayout.EndArea();

            //6px border all around.  Then 14px of window title, 6px again below it to sync to orig window top.
            GUILayout.BeginArea(new Rect(500 - 4 + 16/* + m_padding*/, 0/*m_padding * 2 + m_windowTitleHeight*/, m_window.width - m_padding * 2, m_mainDlgHeight - m_padding * 2)/*, m_dlgSkin.window*/);
            {
                if (m_selectedPages.Count == 1) //HACKJEFFGIFFEN
                {
                    DataPage curPg = m_selectedPages[0];
                    GUILayout.BeginVertical();
                    {
                        //title bar
                        GUILayout.BeginHorizontal();
                        {
                            m_prevBtDown = GUILayout.Button("", m_stylePrevPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                            GUILayout.Label(curPg.m_kspPage.title, GUILayout.Width(m_titleWidth));
                            m_nextBtDown = GUILayout.Button("", m_styleNextPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                            m_closeBtn = GUILayout.Button("", m_closeBtnStyle, GUILayout.Height(25.0f), GUILayout.Width(25.0f));
                        } GUILayout.EndHorizontal();

                        GUILayout.Space(m_padding);

                        GUILayout.BeginHorizontal();
                        {
                            //Fat left column
                            GUILayout.BeginVertical(GUILayout.Width(m_leftColumnWidth)); //width of left column from orig dialog
                            {
                                //the skin's box GUIStyle already has the green text and nice top left align
                                GUILayout.Box(curPg.m_kspPage.resultText, GUILayout.Height(m_resultTextBoxHeight));

                                LayoutInfoField(curPg);
                                LayoutRecoverScienceBarField(curPg);
                                LayoutTransmitScienceBarField(curPg);

                            } GUILayout.EndVertical();

                            //Button right column
                            GUILayout.BeginVertical();
                            {
                                //HACKJEFFGIFFEN tooltips missing: old attempts at the tooltips...why dont you love me WHY
                                //GUI.tooltip = "Discard Data";
                                //GUILayout.Button(new GUIContent("", "Discard Data"), m_styleDiscardButton);
                                m_discardBtn = GUILayout.Button("", m_styleDiscardButton);
                                m_moveBtn = GUILayout.Button("", m_moveBtnStyle, GUILayout.Height(55.0f), GUILayout.Width(55.0f));
                                m_labBtn = GUILayout.Button(curPg.m_labBtnData, m_styleLabButton);
                                m_transmitBtn = GUILayout.Button( curPg.m_transBtnPerc, m_styleTransmitButton );

                            } GUILayout.EndVertical();

                        } GUILayout.EndHorizontal();

                    } GUILayout.EndVertical();
                }
                else
                {
                    GUILayout.BeginVertical();
                    {
                        //title bar
                        GUILayout.BeginHorizontal();
                        {
                            //m_prevBtDown = GUILayout.Button("", m_stylePrevPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                            GUILayout.Label( m_selectedPages.Count + " Experiments Selected", GUILayout.Width(m_titleWidth));
                            //m_nextBtDown = GUILayout.Button("", m_styleNextPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                            GUILayout.Button("", m_closeBtnStyle, GUILayout.Height(25.0f), GUILayout.Width(25.0f)); //HACKJEFFGIFFEN
                        } GUILayout.EndHorizontal();

                        GUILayout.Space(m_padding);

                        GUILayout.BeginHorizontal();
                        {
                            //Fat left column
                            GUILayout.BeginVertical(GUILayout.Width(m_leftColumnWidth)); //width of left column from orig dialog
                            {
                                //the skin's box GUIStyle already has the green text and nice top left align
                                GUILayout.Box("multiselect stats");//, GUILayout.Height(m_resultTextBoxHeight));

                                //LayoutInfoField(m_curPg);
                                //LayoutRecoverScienceBarField(m_curPg);
                                //LayoutTransmitScienceBarField(m_curPg);

                            } GUILayout.EndVertical();

                            //Button right column
                            GUILayout.BeginVertical();
                            {
                                //GUI.tooltip = "Discard Data";//HACKJEFFGIFFEN tooltips missing
                                GUILayout.Button(new GUIContent("", "Discard Data"), m_styleDiscardButton);
                                // GUILayout.Button( "", m_dlgSkin.GetStyle("discard button"));
                                //GUI.tooltip = "Keep Data";
                                GUILayout.Button(new GUIContent("", "Keep Data"), m_styleKeepButton);
                                //GUI.tooltip = "Transmit Data";
                                GUILayout.Button("lolz%", m_styleTransmitButton);
                                
                                //TODOJEFFGIFFEN assets and button
                                //GUILayout.Button("Move", m_moveBtnStyle);
                            } GUILayout.EndVertical();

                        } GUILayout.EndHorizontal();

                    } GUILayout.EndVertical();
                }
            } GUILayout.EndArea();

            //must be last or it disables all the widgets etc
            GUI.DragWindow();
        }

        public void LayoutInfoField(DataPage page)
        {
            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label( m_dataIcon, m_styleRfIcons );
                GUILayout.Label( page.m_dataFieldDataMsg, m_styleRfText );
                if (page.m_dataFieldTrnsWarn != null)
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
                Rect totalBar = new Rect( m_leftColumnWidth - m_progressBarWidth - m_barToEndPad, textRect.yMin, m_progressBarWidth, textRect.height );
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

        public void LayoutListField( DataPage pg )
        {
            GUIStyle listField = new GUIStyle( m_dlgSkin.box );
            listField.padding = new RectOffset(0, 0, 0, 0); //nerf padding
            GUIContent nothing = new GUIContent();
            Color oldColor = GUI.color;
            GUI.color = pg.m_selected ? Color.yellow : Color.white;
            //GUILayout.BeginHorizontal(m_styleRfBackground);
            pg.m_rowButton = GUILayout.Button( nothing, listField, GUILayout.MaxHeight(m_listFieldMaxHeight));
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
                GUI.Label(walker, pg.m_experi, m_styleRfText);
                walker.x += walker.width;

                //recvr
                GUI.color = Color.green;
                string recvrString = pg.m_rcvrValue + "/" + (pg.m_rcvrPrcnt * 100).ToString("F0") + "%";
                GUI.Label( walker, recvrString, m_styleRfText);
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
                GUI.Label(walker, pg.m_biome == string.Empty ? "Global" : pg.m_biome , m_styleRfText);
                walker.x += walker.width;

                //situ
                GUI.Label(walker, pg.m_situ, m_styleRfText);
                walker.x += walker.width;

                //body
                GUI.Label(walker, pg.m_body, m_styleRfText);
                walker.x += walker.width;

            }// GUILayout.EndHorizontal();
            GUI.color = oldColor;
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
            for (int i = 0;
                i < textures.Length &&
                ( m_dataIcon == null || m_scienceIcon == null );
                i++)
            {
                Texture2D tex = textures[i];
                if (tex.name == "resultsdialog_datasize")
                {
                    m_dataIcon = tex;
                    continue;
                }
                if (tex.name == "resultsdialog_scivalue")
                {
                    m_scienceIcon = tex;
                    continue;
                }
            }

            m_closeBtnStyle.normal.background = GameDatabase.Instance.GetTexture("SixHourDays/closeBtnNormal", false);
            m_closeBtnStyle.hover.background = GameDatabase.Instance.GetTexture("SixHourDays/closeBtnHover", false);
            m_closeBtnStyle.active.background = GameDatabase.Instance.GetTexture("SixHourDays/closeBtnDown", false);

            m_moveBtnStyle.margin = new RectOffset(7, 7, 0, 0);
            
            m_moveBtnStyle.normal.background = GameDatabase.Instance.GetTexture("SixHourDays/moveBtnNormal", false);
            m_moveBtnStyle.hover.background = GameDatabase.Instance.GetTexture("SixHourDays/moveBtnHover", false);
            m_moveBtnStyle.active.background = GameDatabase.Instance.GetTexture("SixHourDays/moveBtnDown", false);

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

        

        public void Update()
        {
            //when dialog is spawned, steal its data and kill it
            if (ExperimentsResultDialog.Instance != null)
            {
                //lazy copy of assets from ksp on first run
                if (m_dlgSkin == null) { LazyInit(); }

                //steal pages in compare mode (both show)
                /*if (m_pages.Count == 0) //do only once to not flood
                {
                    m_pages.AddRange(ExperimentsResultDialog.Instance.pages);
                }
                 */

                foreach (ExperimentResultDialogPage resultPage in ExperimentsResultDialog.Instance.pages)
                {
                    m_dataPages.Add(new DataPage(resultPage));
                }
                m_dirtyPages = true; //new pages means we need to resort
                
                ExperimentsResultDialog.Instance.pages.Clear();
                Destroy(ExperimentsResultDialog.Instance.gameObject); //1 frame up still...ehh
            }

            if (m_dataPages.Count > 0)
            {
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
                                m_rankSorter.AddSortField( sf );
                                sf.m_enabled = false;
                                sf.m_text = sf.m_text.Insert(0, m_rankSorter.GetTotalRanks().ToString() + "^"); //HACKJEFFGIFFEN shitty arrow
                                m_dirtyPages = true;
                            }
                            else
                            {
                                //can only untoggle most recent
                                if (sf.Equals( m_rankSorter.GetLastSortField() ) )
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
                        m_dataPages.Sort( m_rankSorter );
                        m_dirtyPages = false;

                        //once re-ordered, indices need updating
                        int i = 0;
                        foreach (DataPage page in m_dataPages)
                        {
                            page.m_index = i++;
                        }

                        //if we've never picked a page, default
                        if (m_selectedPages.Count == 0) { m_selectedPages.Add(m_dataPages[0]); }
                    }

                    //TODOJEFFGIFFEN possibly use GUIStyle down state clone to highlight selected list row
                }

                //prev next buttons
                //HACKJEFFGIFFEN
                /*if (m_prevBtDown || m_nextBtDown)
                {
                    int pageIndex = m_dataPages.IndexOf(m_curPg);
                    if (m_prevBtDown) { pageIndex -= 1; }
                    if (m_nextBtDown) { pageIndex += 1; }

                    //mod on size, guard against negative
                    pageIndex %= m_dataPages.Count();
                    if ( pageIndex < 0 ) { pageIndex += m_dataPages.Count(); }

                    m_curPg = m_dataPages[pageIndex];
                }
                else
                */
                {
                    //page buttons
                    foreach (DataPage page in m_dataPages)
                    {
                        if (page.m_rowButton)
                        {
                            //unhighlight the old
                            foreach (DataPage oldPage in m_selectedPages)
                            {
                                oldPage.m_selected = false;
                            }

                            //now discern whether click or altclick
                            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                            {
                                int a = m_selectedPages[0].m_index; //smallest selected index 
                                int b = m_selectedPages[m_selectedPages.Count - 1].m_index; //largest
                                int c = page.m_index; //brand new index chosen
                                int start = Math.Min(Math.Min(a, b), c);
                                int end = Math.Max(Math.Max(a, b), c);
                                m_selectedPages = m_dataPages.GetRange(start, end - start + 1); // inclusive range
                            }
                            else
                            {
                                m_selectedPages.Clear();
                                m_selectedPages.Add(page);
                            }

                            //highlight the new
                            foreach (DataPage newPage in m_selectedPages)
                            {
                                newPage.m_selected = true;
                            }

                            break;
                        }
                    }
                }

                //TODOJEFFGIFFEN
                //buttons should context sensitive - number of experi they apply to displayed like X / All.
                //move button imagery:
                //  onboard should be folder arrow capsule //thought, science symbol instead?
                //  eva get should be folder arrow kerb
                //  eva put should be folder arrow capsule
                //buttons should either be live or ghosted, NEVER gone, NEVER move.
                //on any actual button down:
                //  call that callback for selected list
                //  remove entries from m_dataPages, clear selected
                //  trigger list refresh (resot, reindex, reset selected)
                //button ghost conditions:
                //  no antenna
                //  no lab
                //  lab, but it has copies of all selected
                //  all selected are in only onboard sci container (nowher to go as experi are data out only)
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


        public void OnDisable()
        {
            Debug.Log("GA " + m_callCount++ + " OnDisable()");
        }

        public void OnDestroy()
        {
            Debug.Log("GA " + m_callCount++ + " OnDestroy()");
        }
    }
}
