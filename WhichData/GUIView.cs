using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace WhichData
{
    public class ViewPage
    {
        //ksp data
        public DataPage m_src; //TODOJEFFGIFFEN

        //hud state
        public int m_index = 0; //into m_viewPages
        public bool m_selected = false;
        public bool m_rowButton = false;

        //hud display (truncated values, formatted strings etc)
        public string title => m_src.m_subject.title;
        public string experiment => m_src.m_experi;
        public string body => m_src.m_body;
        public string situation => m_src.m_situ;
        public string biome => m_src.m_biome == string.Empty ? "Global" : m_src.m_biome;
        public float mits => m_src.m_scienceData.dataAmount;
        public string m_resultText;
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

        public ViewPage(DataPage src)
        {
            m_src = src;
            ScienceSubject subject = m_src.m_subject;
            ScienceData scidata = m_src.m_scienceData;

            m_resultText = ResearchAndDevelopment.GetResults(subject.id);

            //all displayed science is boosted by this
            float sciGain = HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

            //compose data used in row display
            m_rcvrValue = (m_src.m_fullValue * sciGain).ToString("F1");
            m_rcvrPrcnt = m_src.m_fullValue / subject.scienceCap;

            float transmitValue = m_src.m_transmitValue * sciGain;
            m_trnsValue = transmitValue.ToString("F1");
            m_trnsPrcnt = m_src.m_transmitValue / subject.scienceCap; //TODOJEFFGIFFEN AAAAUGH why god why (always too low)

            //compose data used in info pane (classic dialog fields)
            m_dataFieldDataMsg = "Data Size: " + scidata.dataAmount.ToString("F1") + " Mits";
            m_dataFieldTrnsWarn = m_src.m_trnsWarnEnabled ? "Inoperable after Transmitting." : string.Empty;
            m_rcvrFieldMsg = "Recovery: +" + m_rcvrValue + " Science";
            m_rcvrFieldBackBar = 1.0f - subject.science / subject.scienceCap; //shows this experi's value vs max possible
            m_trnsFieldMsg = "Transmit: +" + m_trnsValue + " Science";
            //HACKJEFFGIFFEN
            m_trnsFieldBackBar = (m_src.m_transmitValue + m_src.m_nextTransmitValue) / subject.scienceCap;
            m_transBtnPerc = (transmitValue >= 0.1f ? scidata.transmitValue * 100 : 0.0f).ToString("F0") + "%"; //if transmit sci is 0pts, then % is 0
            m_labBtnData = "+" + m_src.m_labPts.ToString("F0");
        }
    }

    class GUIView
    {
        //UI
        private bool m_showUI = true;
        public void HideUI() { m_showUI = false; }
        public void ShowUI() { m_showUI = true; }
        //public static EventVoid onHideUI; //should respond to this if its not forced
        //public static EventVoid onShowUI;

        public void OnAwake()
        {
            GameEvents.onHideUI.Add(HideUI);
            GameEvents.onShowUI.Add(ShowUI);
        }

        public void OnDestroy()
        {
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
        }

        //window pixel positions etc
        //default window in 1920x1080 is at 243, 190, 413x240 (10px of grey round thinger at left)
        //500px of list pane, 413 of info pane ( -10 left grey thing -9 fat left pad of info. y+60 for 'all' button height //HACKJEFFGIFFEN
        public Rect m_window = new Rect(243, 190, 500 + 394, 240 + 64);
        public Rect m_listPaneRect = new Rect(6, 6, 500 - 6 * 2, 240 + 64 - 6 * 2); //y origin is m_padding really
        public int m_listFieldMaxHeight = 22;  //height of each list row. min be 18, helps with click probability some

        //state from ksp
        public GUIStyle m_styleRfText;       //
        GUISkin m_dlgSkin;

        //GUI state
        //out state of buttons
        public bool closeBtn => m_infoPane.closeBtn;
        public bool discardBtn => m_infoPane.discardBtn;
        public bool moveBtn => m_infoPane.moveBtn;
        public bool labBtn => m_infoPane.labBtn;
        public bool transmitBtn => m_infoPane.transmitBtn;

        public Vector2 m_scrollPos;


        WhichData m_controller;

        public BaseInfoPane m_baseInfoPane = new BaseInfoPane();
        public ViewPageInfoPane m_viewPageInfoPane = new ViewPageInfoPane();
        public BaseInfoPane m_infoPane; //is always one or the other of above

        //push button state to view, update view info pane w selected pages
        public void SetViewInfo(bool moveEnable, bool labEnable, bool transEnable)
        {
            if ( m_selectedPages.Count > 0)
            {
                Debug.Log("GA view setup info pane & action buttons");
                if (m_selectedPages.Count == 1)
                {
                    //display one page, traditional info pane
                    m_viewPageInfoPane.Set(m_selectedPages.First(), moveEnable, labEnable, transEnable);
                    m_infoPane = m_viewPageInfoPane;
                }
                else
                {
                    //display summary of selected pages, custom info pane
                    m_baseInfoPane.Set(m_selectedPages, moveEnable, labEnable, transEnable);
                    m_infoPane = m_baseInfoPane;
                }
            }
        }

        //returns empty string on success, error string on failure
        public string Initialize(WhichData controller)
        {
            Debug.Log("GA GUIView::Initialize");
            string errorMsg = m_baseInfoPane.Initialize();
            if ( errorMsg != string.Empty)
            {
                //HACKJEFFGIFFEN
                return errorMsg;
            }
            errorMsg = m_viewPageInfoPane.Initialize();
            if (errorMsg != string.Empty)
            {
                //HACKJEFFGIFFEN
                return errorMsg;
            }

            m_infoPane = m_baseInfoPane;
            m_controller = controller;

            //TODOJEFFGIFFEN start validating the loads against bad data, return appropriate errors
            GUISkin[] skins = Resources.FindObjectsOfTypeAll<GUISkin>();
            /* Found skins:
                GameSkin
                ExpRecoveryDialogSkin
                ExperimentsDialogSkin
                FlagBrowserSkin
                KSCContextMenuSkin
                KSP window 1
                KSP window 2
                KSP window 3
                KSP window 4
                KSP window 5
                KSP window 6
                KSP window 7
                MainMenuSkin
                MiniSettingsSkin
                OrbitMapSkin
                PartTooltipSkin
                PlaqueDialogSkin
            */
            //find ExperimentsResultDialog.guiSkin
            m_dlgSkin = skins.First(skin => skin.name == "ExperimentsDialogSkin");
            //these are the custom styles inside the ExperimentsResultDialog
            m_styleRfText = m_dlgSkin.GetStyle("iconstext");

            return errorMsg;
        }

        //HACKJEFFGIFFEN
        public List<ViewPage> m_viewPages = new List<ViewPage>();
        public bool m_dirtySelection = false;
        public List<ViewPage> m_selectedPages = new List<ViewPage>();
        public void OnGUI()
        {
            if (m_showUI && m_viewPages.Count > 0)
            {
                int uid = GUIUtility.GetControlID(FocusType.Passive); //get a nice unique window id from system
                GUI.skin = m_dlgSkin;
                m_window = GUI.Window(uid, m_window, WindowLayout, "", HighLogic.Skin.window); //style
            }
        }

        void WindowLayout(int windowID)
        {
            GUILayout.BeginArea(m_listPaneRect/*, HighLogic.Skin.window*/);
            {
//                LayoutListToggles();
                LayoutListFields();

            }
            GUILayout.EndArea();

            m_infoPane.WindowLayout();

            //must be last or it disables all the widgets etc
            GUI.DragWindow();
        }

/*        public void LayoutListToggles()
        {
            GUILayout.BeginHorizontal();
            {
                //sorter toggles
                foreach (SortField sf in m_sortFields)
                {
                    sf.m_guiToggle = GUILayout.Toggle(sf.m_guiToggle, sf.m_text, HighLogic.Skin.button); //want a ksp button not the fat rslt dlg button
                }
            }
            GUILayout.EndHorizontal();
        }
*/
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
                foreach (ViewPage pg in m_viewPages)
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
                        GUI.Label(walker, pg.experiment, cliptext);
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
                        GUI.Label(walker, pg.mits + " Mits", m_styleRfText);
                        walker.x += walker.width;

                        //disabling
                        //GUI.Label(walker, pg.showTransmitWarning ? "Disbl" : "-", m_styleRfText);
                        //walker.x += walker.width;

                        //biome
                        GUI.Label(walker, pg.biome, m_styleRfText);
                        walker.x += walker.width;

                        //situ
                        GUI.Label(walker, pg.situation, m_styleRfText);
                        walker.x += walker.width;

                        //body
                        GUI.Label(walker, pg.body, m_styleRfText);
                        walker.x += walker.width;

                    }
                }
                GUI.color = oldColor;

            }
            GUILayout.EndScrollView();
        }

        public void Update()
        {
            m_dirtySelection = false;

            //when controller updates, view updates
            if (m_controller.m_dirtyPages)
            {
                Debug.Log("GA rebuild view pages");
                //HACKJEFFGIFFEN the selected need preserved across this
                m_viewPages.Clear();
                m_selectedPages.Clear();

                m_controller.m_dataPages.ForEach(page => m_viewPages.Add(new ViewPage(page)));

                //rebuild indices
                int i = 0;
                m_viewPages.ForEach(viewPage => viewPage.m_index = i++);

                m_dirtySelection = true;
            }

            if (m_showUI && m_viewPages.Count > 0)
            {
                //list click handling

                //when there is no selection, default
                if (m_selectedPages.Count == 0)
                {
                    m_viewPages.First().m_rowButton = true; //fake a click
                }

                //find first page with its row button clicked (or none, giving null)
                ViewPage selectedPage = m_viewPages.Find(page => page.m_rowButton); //my first lambda EVER!  hooray

                if (selectedPage != null)
                {
                    Debug.Log("GA view new selection");

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
                        m_selectedPages = m_viewPages.GetRange(start, end - start + 1); // inclusive range
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
        }
    }

    public class BaseInfoPane
    {
        //ksp state
        public GUIStyle m_styleDiscardButton;
        public GUIStyle m_styleTransmitButton;
        public GUIStyle m_styleLabButton;
        public GUIStyle m_styleResetButton;
        //handmade styles
        public GUIStyle m_closeBtnStyle = new GUIStyle();
        public GUIStyle m_moveBtnStyle = new GUIStyle();

        public virtual string Initialize()
        {
            string errorMsg = string.Empty;

            //TODOJEFFGIFFEN start validating the loads against bad data, return appropriate errors

            GUISkin[] skins = Resources.FindObjectsOfTypeAll<GUISkin>();
            //find ExperimentsResultDialog.guiSkin
            GUISkin dlgSkin = skins.First(skin => skin.name == "ExperimentsDialogSkin");
            //these are the custom styles inside the ExperimentsResultDialog
            m_styleDiscardButton = dlgSkin.GetStyle("discard button");
            m_styleTransmitButton = dlgSkin.GetStyle("transmit button");
            m_styleLabButton = dlgSkin.GetStyle("lab button");
            m_styleResetButton = dlgSkin.GetStyle("reset button");
            
            //our on disk assets
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

            return errorMsg;
        }

        //out state of buttons
        public bool closeBtn { get; set; }
        public bool discardBtn { get; set; }
        public bool moveBtn { get; set; }
        public bool labBtn { get; set; }
        public bool transmitBtn { get; set; }

        protected string m_title;
        protected string m_resultText;
        protected string m_labBtnData;
        protected string m_transBtnPerc;//0% when sci is 0 pts, and integer % otherwise
        protected bool m_moveBtnEnabled;
        protected bool m_labBtnEnabled;
        protected bool m_transBtnEnabled;

        public void Set( List<ViewPage> selectedPages, bool moveEnable, bool labEnable, bool transEnable)
        {
            //sums of selected stats
            float recoverSci = 0.0f;
            int resetable = 0;
            int labAble = 0;
            float labCopyData = 0.0f;
            float transmitAvg = 0.0f;
            float transmitSci = 0.0f;
            foreach (ViewPage viewPage in selectedPages)
            {
                DataPage page = viewPage.m_src;
                recoverSci += page.m_fullValue;

                if (page.m_dataModule is ModuleScienceExperiment) { resetable += 1; }
                if (page.m_labPts > 0) { labAble += 1; }
                labCopyData += page.m_labPts;

                transmitAvg += page.m_scienceData.transmitValue * 100f; //HACKJEFFGIFFEN discarding the 0 trans = 0 % cases
                transmitSci += page.m_transmitValue;
            }

            //layout info pane stats
            m_title = selectedPages.Count + " Experiments Selected";

            float sciGain = HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;
            recoverSci *= sciGain;
            transmitSci *= sciGain;
            m_resultText =
                recoverSci.ToString("F1") + "pts recoverable science selected!\n" +
                (selectedPages.Count - resetable) + " can discard, " + resetable + " can reset.\n" +
                labAble + "/" + selectedPages.Count + " experiments available for lab copy.\n" +
                transmitSci.ToString("F1") + "pts via transmitting science.";

            m_labBtnData = "+" + labCopyData.ToString("F0");

            transmitAvg /= selectedPages.Count;
            m_transBtnPerc = transmitAvg.ToString("F0") + "%";

            //enable state from controller
            m_moveBtnEnabled = moveEnable;
            m_labBtnEnabled = labEnable;
            m_transBtnEnabled = transEnable;
            
        }

        //6px border all around.  Then 14px of window title, 6px again below it to sync to orig window top.
        public Rect m_infoPaneRect = new Rect(500 + 3, 6, 391 - 3, 240 + 64 - 6 * 2); //note, top/bottom pad, 3 combined w list's 6 = 9 left pad.  no right pad (made of dropshadow).
        public int m_padding = 6;
        public int m_barToEndPad = 2;
        public Color m_inopWarnOrange = new Color(1.0f, 0.63f, 0.0f); //orangey gold
        public float m_rightSideWidth = 60;//stolen from ExperimentsResultDialog

        public void WindowLayout()
        {
            GUILayout.BeginArea(m_infoPaneRect);
            {
                GUILayout.BeginVertical();
                {
                    LayoutTitleBar();

                    GUILayout.BeginHorizontal();
                    {
                        //Main left column
                        GUILayout.BeginVertical();
                        {
                            LayoutBody();
                        }
                        GUILayout.EndVertical();

                        //Rightside button column
                        LayoutActionButtons();

                    }
                    GUILayout.EndHorizontal();

                }
                GUILayout.EndVertical();

            }
            GUILayout.EndArea();
        }

        public void LayoutTitleBar()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.Label(m_title);
                closeBtn = GUILayout.Button("", m_closeBtnStyle);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4); //HACKJEFFGIFFEN to align with the list pane's top
        }

        public virtual void LayoutBody()
        {
            //the skin's box GUIStyle already has the green text and nice top left align
            GUILayout.Box(m_resultText);
        }

        public void LayoutActionButtons()
        {
            GUILayout.BeginVertical(GUILayout.Width(m_rightSideWidth));
            {
                discardBtn = GUILayout.Button("", m_styleDiscardButton);
                Color oldColor = GUI.color;
                GUI.color = m_moveBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                moveBtn = GUILayout.Button("", m_moveBtnStyle);
                GUI.color = m_labBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                labBtn = GUILayout.Button(m_labBtnData, m_styleLabButton);
                GUI.color = m_transBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                transmitBtn = GUILayout.Button(m_transBtnPerc, m_styleTransmitButton);
                GUI.color = oldColor;
            }
            GUILayout.EndVertical();
        }
    }

    public class ViewPageInfoPane : BaseInfoPane
    {
        //state from ksp
        public GUIStyle m_styleRfIcons;      //result field
        public GUIStyle m_styleRfText;       //
        public GUIStyle m_styleRfBackground; //
        public GUIStyle m_stylePrgBarBG;
        public GUIStyle m_stylePrgBarDarkGreen;
        public GUIStyle m_stylePrgBarLightGreen;
        public GUIStyle m_stylePrgBarDarkBlue;
        public GUIStyle m_stylePrgBarLightBlue;

        public Texture2D m_dataIcon = null;
        public Texture2D m_scienceIcon = null;

        public override string Initialize()
        {
            string errorMsg = base.Initialize();
            if ( errorMsg != string.Empty )
            {
                //HACKJEFFGIFFEN cleanup
                return errorMsg;
            }

            //TODOJEFFGIFFEN start validating the loads against bad data, return appropriate errors

            GUISkin[] skins = Resources.FindObjectsOfTypeAll<GUISkin>();
            //find ExperimentsResultDialog.guiSkin
            GUISkin dlgSkin = skins.First(skin => skin.name == "ExperimentsDialogSkin");
            //these are the custom styles inside the ExperimentsResultDialog
            m_styleRfIcons = dlgSkin.GetStyle("icons");
            m_styleRfText = dlgSkin.GetStyle("iconstext");
            m_styleRfBackground = dlgSkin.GetStyle("resultfield");
            m_stylePrgBarBG = dlgSkin.GetStyle("progressBarBG");
            m_stylePrgBarDarkGreen = dlgSkin.GetStyle("progressBarFill");
            m_stylePrgBarLightGreen = dlgSkin.GetStyle("progressBarFill2");
            m_stylePrgBarDarkBlue = dlgSkin.GetStyle("progressBarFill3");
            m_stylePrgBarLightBlue = dlgSkin.GetStyle("progressBarFill4");

            //find and use the existing textures from the orig dlg
            Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            m_dataIcon = textures.First<Texture2D>(t => t.name == "resultsdialog_datasize");
            m_scienceIcon = textures.First<Texture2D>(t => t.name == "resultsdialog_scivalue");

            return errorMsg;

        }
        //HACKJEFFGIFFEN maybe hack maybe not
        public float m_progressBarWidth = 130; //stolen from ExperimentsResultDialog
        public int m_leftColumnWidth = 325;
        public float m_barMinPx = 7.0f;

        //page we'll be displaying
        private ViewPage m_page;
        public void Set( ViewPage page, bool moveEnable, bool labEnable, bool transEnable )
        {
            m_page = page;
            m_title = page.title;
            m_resultText = page.m_resultText;
            m_labBtnData = page.m_labBtnData;
            m_transBtnPerc = page.m_transBtnPerc;

            m_moveBtnEnabled = moveEnable;
            m_labBtnEnabled = labEnable;
            m_transBtnEnabled = transEnable;
        }

        public override void LayoutBody()
        {

            //the skin's box GUIStyle already has the green text and nice top left align
            GUILayout.Box(m_resultText);

            LayoutInfoField(m_page);
            LayoutRecoverScienceBarField(m_page);
            LayoutTransmitScienceBarField(m_page);
        }

        public void LayoutInfoField(ViewPage page)
        {
            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label(m_dataIcon, m_styleRfIcons);
                GUILayout.Label(page.m_dataFieldDataMsg, m_styleRfText);
                if (page.m_dataFieldTrnsWarn != string.Empty)
                {
                    Rect textRect = GUILayoutUtility.GetLastRect();
                    TextAnchor oldAnchor = m_styleRfText.alignment;
                    Color oldColor = GUI.color;

                    //HACKJEFFGIFFEN this is a goddamn atrocity
                    Rect totalField = new Rect(m_leftColumnWidth - m_progressBarWidth - m_barToEndPad, textRect.yMin, m_progressBarWidth, textRect.height);
                    m_styleRfText.alignment = TextAnchor.MiddleRight;
                    GUI.color = m_inopWarnOrange;
                    GUI.Label(totalField, page.m_dataFieldTrnsWarn, m_styleRfText);

                    GUI.color = oldColor;
                    m_styleRfText.alignment = oldAnchor;

                }
            }
            GUILayout.EndHorizontal();
        }

        public void LayoutRecoverScienceBarField(ViewPage page)
        {
            //selecting the recover science strings & styles
            LayoutScienceBarField(page.m_rcvrFieldMsg, page.m_rcvrPrcnt, page.m_rcvrFieldBackBar, m_stylePrgBarDarkGreen, m_stylePrgBarLightGreen);
        }

        public void LayoutTransmitScienceBarField(ViewPage page)
        {
            //selecting the transmit science strings & styles
            LayoutScienceBarField(page.m_trnsFieldMsg, page.m_trnsPrcnt, page.m_trnsFieldBackBar, m_stylePrgBarDarkBlue, m_stylePrgBarLightBlue);
        }

        public void LayoutScienceBarField(string text, float lightFillPrcnt, float darkFillPrcnt, GUIStyle darkBarStyle, GUIStyle lightBarStyle)
        {
            //info bars white text
            GUIContent nothing = new GUIContent();

            GUILayout.BeginHorizontal(m_styleRfBackground);
            {
                GUILayout.Label(m_scienceIcon, m_styleRfIcons);
                GUILayout.Label(text, m_styleRfText);
                Rect textRect = GUILayoutUtility.GetLastRect();

                //HACKJEFFGIFFEN this is a goddamn atrocity
                //Rect totalBar = new Rect(391 - m_rightSideWidth - m_progressBarWidth - 11 - m_barToEndPad, textRect.yMin, m_progressBarWidth, textRect.height);
                Rect totalBar = new Rect(391 - m_rightSideWidth - m_progressBarWidth - 11, textRect.yMin, m_progressBarWidth, textRect.height);
                GUI.Box(totalBar, nothing, m_stylePrgBarBG);

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

            }
            GUILayout.EndHorizontal();
        }
    }
}
