using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Reflection;
using System.IO;

namespace WhichData
{
    struct SubjectBreakdown
    {
        public string m_experi;
        public string m_body;
        public string m_situ;
        public bool m_hasBiome;
        public string m_biome;
        public string m_subjectId;

        public SubjectBreakdown(string subjectId)
        {
            m_hasBiome = false;
            m_experi = m_body = m_situ = m_biome = "";

            //parse out subjectIDs of the form (we ditch the @):
            //  crewReport@KerbinSrfLandedLaunchPad
            m_subjectId = subjectId;

            //experiment
            string[] strings = subjectId.Split('@');
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
                Debug.Log("GA ERROR no body in id " + subjectId);
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
                Debug.Log("GA ERROR no situ in id " + subjectId);
            }

            //biome
            //when a situation treats experiment as global, no biome name is appended, and subject will now be string.empty
            if (subject != string.Empty)
            {
                m_hasBiome = true;
                //TODOJEFFGIFFEN can't find a complete list of them programmatically,
                //R&D doesnt return "R&D" or "Flag Pole" etc
                m_biome = subject;                                                      //LaunchPad
            }
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
        public float m_sciHack = 0.5f;
        public float m_barMinPx = 7.0f;
        
        //window pixel positions etc
        //default window in 1920x1080 is at 243, 190, 413x240
        //we want to be 428px to the right to compare easily, and 18px taller for our window title)
        public float m_mainDlgHeight = 240 + 18;
        public Rect m_window = new Rect(243 + 428, 190 - 16, 413, 240 + 18 + 240); //double old height //HACKJEFFGIFFEN
        public Rect m_myDlg = new Rect(0, 240 + 18, 400 - 4, 240 - 4);
        public int m_windowTitleHeight = 14;
        public int m_padding = 6;
        public int m_titleWidth = 328;
        public int m_leftColumnWidth = 325;
        public int m_resultTextBoxHeight = 110;
        public int m_barToEndPad = 2;
        
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
        public List<ExperimentResultDialogPage> m_pages = new List<ExperimentResultDialogPage>();

        //GUI state
        public bool m_prevBtDown = false;
        public bool m_nextBtDown = false;
        //HACKJEFFGIFFEN
        public List<bool> m_pageListButtons = new List<bool>();
        //
        //public bool m_discardBtDown = false;
        //
        public Vector2 m_scrollPos;
        public Texture2D m_dataIcon = null;
        public Texture2D m_scienceIcon = null;
        public float m_sciRet0;
        public float m_sciRet1;
        public float m_sciTrns0;
        public float m_sciTrns1;
        

        //mod state
        public int m_curInd;

        void OnGUI()
        {
            if (m_pages.Count > 0)
            {
                int uid = GUIUtility.GetControlID(FocusType.Passive); //get a nice unique window id from system
                GUI.skin = m_dlgSkin;
                m_window = GUI.Window(uid, m_window, WindowLayout, "WhichData"); //style
            }
        }

        void WindowLayout(int windowID)
        {
            ExperimentResultDialogPage curPg = m_pages[m_curInd];
            //6px border all around.  Then 14px of window title, 6px again below it to sync to orig window top.
            GUILayout.BeginArea(new Rect(m_padding, m_padding * 2 + m_windowTitleHeight, m_window.width - m_padding * 2, m_mainDlgHeight - m_padding * 2));
            {
                GUILayout.BeginVertical();
                {
                    //title bar
                    GUILayout.BeginHorizontal();
                    {
                        m_prevBtDown = GUILayout.Button("", m_stylePrevPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                        GUILayout.Label(curPg.title, GUILayout.Width(m_titleWidth));
                        m_nextBtDown = GUILayout.Button("", m_styleNextPage, GUILayout.Width(m_pageButtonSize), GUILayout.Height(m_pageButtonSize + m_pageButtonPadding));
                    } GUILayout.EndHorizontal();

                    GUILayout.Space(m_padding);

                    GUILayout.BeginHorizontal();
                    {
                        //Fat left column
                        GUILayout.BeginVertical(GUILayout.Width(m_leftColumnWidth)); //width of left column from orig dialog
                        {
                            //the skin's box GUIStyle already has the green text and nice top left align
                            GUILayout.Box(curPg.resultText, GUILayout.Height(m_resultTextBoxHeight));

                            LayoutResultField("Data Size: " + curPg.dataSize.ToString("F1") + " Mits", curPg.showTransmitWarning ? "Inoperable after Transmitting." : null);
                            LayoutResultField("Recovery: +" + curPg.scienceValue.ToString("F1") + " Science", true, m_sciRet0, m_sciRet1);
                            LayoutResultField("Transmit: +" + curPg.transmitValue.ToString("F1") + " Science", false, m_sciTrns0, m_sciTrns1);

                        } GUILayout.EndVertical();

                        //Button right column
                        GUILayout.BeginVertical();
                        {
                            //GUI.tooltip = "Discard Data";//HACKJEFFGIFFEN tooltips missing
                            GUILayout.Button(new GUIContent("", "tooltip"), m_styleDiscardButton);
                            // GUILayout.Button( "", m_dlgSkin.GetStyle("discard button"));
                            //GUI.tooltip = "Keep Data";
                            GUILayout.Button("", m_styleKeepButton);
                            //GUI.tooltip = "Transmit Data";

                            GUILayout.Button((0.69 * 100).ToString() + "%", m_styleTransmitButton); //HACKJEFFGIFFEN only show % on > 0
                        } GUILayout.EndVertical();

                    } GUILayout.EndHorizontal();


                    //  remaining todo:
                    //  make transmit % be 0% when its 0
                    //  make "inoperable after transmit" align and color
                    //  make transmit light blue just be 5 px back off dark blue
                    //  align the top of button with field

                    //  figure out how to make a ConverterResults field clickable / highlight and shit
                } GUILayout.EndVertical();
            } GUILayout.EndArea();

            GUILayout.BeginArea( m_myDlg );
            {
                /*GUILayout.BeginHorizontal();
                {
                //sorter toggles
//                        toggle0 = GUILayout.Button("Aaaaaa", dlgSkin.GetStyle("keep button"));
                    toggle1 = GUILayout.Toggle(toggle1, "Bbbbbb", "button");
                } GUILayout.EndHorizontal();
*/
            
                //TODOJEFFGIFFEN toggle bar of sorters now!
                GUIStyle ngs = new GUIStyle(m_dlgSkin.scrollView); //HACKJEFFGIFFEN
                ngs.padding = new RectOffset( 0, 0, 0, 0); //get rid of stupid left pad
                m_scrollPos = GUILayout.BeginScrollView(m_scrollPos, ngs );
                {
                    for (int i = 0; i < m_pages.Count; i++)
                    {
                        LayoutListField(i);
                    }
                } GUILayout.EndScrollView();

            } GUILayout.EndArea();

            //must be last or it disables all the widgets etc
            GUI.DragWindow();
        }

        public void LayoutResultField(string text, string warning = null)
        {
            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label( m_dataIcon, m_styleRfIcons );
                GUILayout.Label( text, m_styleRfText );
                if (warning != null)
                {
                    TextAnchor oldAnchor = m_styleRfText.alignment;
                    Color oldColor = GUI.color;

                    m_styleRfText.alignment = TextAnchor.MiddleRight; //HACKJEFFGIFFEN align...doesnt
                    GUI.color = Color.cyan;
                    GUILayout.Label(warning, m_styleRfText);
                    GUI.color = oldColor;
                    m_styleRfText.alignment = oldAnchor;
                }

            } GUILayout.EndHorizontal();
        }

        public void LayoutResultField(string text, bool greenFill, float lightFillPercent, float darkFillPercent )
        {
             //info bars white text
            GUIContent nothing = new GUIContent();

            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label( m_scienceIcon, m_styleRfIcons );
                GUILayout.Label( text, m_styleRfText );

                Rect textRect = GUILayoutUtility.GetLastRect();
                Rect totalBar = new Rect( m_leftColumnWidth - m_progressBarWidth - m_barToEndPad, textRect.yMin, m_progressBarWidth, textRect.height );
                GUI.Box( totalBar, nothing, m_stylePrgBarBG );

                //the bars need 7/130 or more to draw right (pad for the caps)
                //if the light bar is invisible, the dark bar has no point.
                float lightFillPx = lightFillPercent * m_progressBarWidth;
                if (lightFillPx >= m_barMinPx)
                {
                    Rect darkBar = new Rect(totalBar);
                    darkBar.width *= darkFillPercent;
                    GUI.Box(darkBar, nothing, greenFill ? m_stylePrgBarDarkGreen : m_stylePrgBarDarkBlue);

                    Rect lightBar = new Rect(totalBar);
                    lightBar.width = lightFillPx;
                    GUI.Box(lightBar, nothing, greenFill ? m_stylePrgBarLightGreen : m_stylePrgBarLightBlue);
                }

            } GUILayout.EndHorizontal();
        }

        public void LayoutListField( int i )
        {
            ExperimentResultDialogPage pg = m_pages[ i ];
            GUIStyle listField = new GUIStyle( m_dlgSkin.box );
            listField.padding = new RectOffset(0, 0, 0, 0); //nerf padding
            GUIContent nothing = new GUIContent();
            //GUILayout.BeginHorizontal(m_styleRfBackground);
            m_pageListButtons[i] = GUILayout.Button( nothing, listField, GUILayout.MaxHeight(18)); //HACKJEFFGIFFEN
            Rect btRect = GUILayoutUtility.GetLastRect();
            {
                //experi, rec sci, biome, sit, body
                const int fields = 5;
                float fieldWidth = btRect.width / fields;
                Rect walker = btRect;
                walker.width = fieldWidth;
                

                //HACKJEFFGIFFEN make colleciton of these
                SubjectBreakdown sb = new SubjectBreakdown( pg.pageData.subjectID );

                GUI.Label(walker, sb.m_experi, m_styleRfText);
                walker.x += walker.width;

                Color oldColor = GUI.color;
                GUI.color = Color.green;
                GUI.Label( walker, pg.scienceValue.ToString("F1"), m_styleRfText);
                walker.x += walker.width;
                GUI.color = oldColor;

                GUI.Label(walker, sb.m_hasBiome ? sb.m_biome : "Global", m_styleRfText);
                walker.x += walker.width;

                GUI.Label( walker, sb.m_situ, m_styleRfText);
                walker.x += walker.width;

                GUI.Label(walker, sb.m_body, m_styleRfText);
                walker.x += walker.width;

/*              GUI.Label( btRect, pg.scienceValue.ToString("F1"), m_styleRfText);
                GUI.color = Color.blue;
                GUI.Label( btRect, pg.transmitValue.ToString("F1"), m_styleRfText);
                GUI.color = oldColor;
                */
                
            }// GUILayout.EndHorizontal();
        }

        public void LazyInit()
        {
            m_dlgSkin = ExperimentsResultDialog.Instance.guiSkin;

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
            for (int i = 0; i < textures.Length &&
                (m_dataIcon == null
                || m_scienceIcon == null
                );
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
            bool indUpdate = false;

            //when dialog is spawned, steal its data and kill it
            if (ExperimentsResultDialog.Instance != null)
            {
                //lazy copy of assets from ksp on first run
                if (m_dlgSkin == null) { LazyInit(); }

                //steal pages
                //HACKJEFFGIFFEN left in compare mode (both show)
                /*if (m_pages.Count == 0) //do only once to not flood
                {
                    m_pages.AddRange(ExperimentsResultDialog.Instance.pages);
                }
                */
                int count = ExperimentsResultDialog.Instance.pages.Count();
                m_pages.AddRange(ExperimentsResultDialog.Instance.pages);
                
                //autoselect entry 0
                if (m_pageListButtons.Count == 0)
                {
                    m_pageListButtons.Add(true);
                    m_curInd = 0;
                    indUpdate = true;
                    count--;
                }

                while (count-- > 0) { m_pageListButtons.Add( false ); }


                ExperimentsResultDialog.Instance.pages.Clear();
                Destroy(ExperimentsResultDialog.Instance.gameObject); //1 frame up still...ehh
            }

            //TODOJEFFGIFFEN hide on empty
            {
                //prev next buttons
                if (m_prevBtDown)
                {
                    if ((m_curInd -= 1) < 0) { m_curInd = m_pages.Count() - 1; }
                    indUpdate = true;
                }
                else if (m_nextBtDown)
                {
                    if ((m_curInd += 1) >= m_pages.Count()) { m_curInd = 0; }
                    indUpdate = true;
                }
                else
                {
                    //page toggles
                    int toggleIndex = -1;
                    for (int i = 0; i < m_pageListButtons.Count; i++)
                    {
                        if (m_pageListButtons[i]) { toggleIndex = i; break; }
                    }
                    if (toggleIndex != -1)
                    {
                        m_curInd = toggleIndex;
                        indUpdate = true;
                    }
                }

                if ( indUpdate )
                {
                    ExperimentResultDialogPage curPg = m_pages[m_curInd];
                    ScienceSubject s = ResearchAndDevelopment.GetSubjectByID(curPg.pageData.subjectID);
                    m_sciRet0 = curPg.scienceValue * m_sciHack / s.scienceCap;
                    m_sciRet1 = 1.0f - s.science / s.scienceCap;
                    m_sciTrns0 = curPg.transmitValue * m_sciHack / s.scienceCap; //TODOJEFFGIFFEN AAAAUGH why god why
                    m_sciTrns1 = m_sciRet1 * curPg.xmitDataScalar;
                }
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
