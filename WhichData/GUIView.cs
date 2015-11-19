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
        public int m_index = 0; //into m_dataPages
        public bool m_selected = false;
        public bool m_rowButton = false;

        //hud display (truncated values, formatted strings etc)
        public string title => m_src.m_subject.title;
        public string experiment => m_src.m_experi;
        public string body => m_src.m_body;
        public string situation => m_src.m_situ;
        public string biome => m_src.m_biome == string.Empty ? "Global" : m_src.m_biome;
        public float mits => m_src.m_scienceData.dataAmount;
        public string m_boxText;
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
        public float m_sciHack = 2.0f;
        public ViewPage(DataPage src)
        {
            m_src = src;
            ScienceSubject subject = m_src.m_subject;
            ScienceData data = m_src.m_scienceData;

            m_boxText = ResearchAndDevelopment.GetResults(subject.id);
            //compose data used in row display
            //displayed science in KSP is always 2x the values used in bgr
            m_rcvrValue = (m_src.m_fullValue * m_sciHack).ToString("F1");
            m_rcvrPrcnt = m_src.m_fullValue / subject.scienceCap;

            float transmitValue = m_src.m_transmitValue * m_sciHack;
            m_trnsValue = transmitValue.ToString("F1");
            m_trnsPrcnt = m_src.m_transmitValue / subject.scienceCap; //TODOJEFFGIFFEN AAAAUGH why god why (always too low)

            //compose data used in info pane (classic dialog fields)
            m_dataFieldDataMsg = "Data Size: " + m_src.m_scienceData.dataAmount.ToString("F1") + " Mits";
            m_dataFieldTrnsWarn = m_src.m_trnsWarnEnabled ? "Inoperable after Transmitting." : string.Empty;
            m_rcvrFieldMsg = "Recovery: +" + m_rcvrValue + " Science";
            m_rcvrFieldBackBar = 1.0f - subject.science / subject.scienceCap; //shows this experi's value vs max possible
            m_trnsFieldMsg = "Transmit: +" + m_trnsValue + " Science";
            //HACKJEFFGIFFEN
            m_trnsFieldBackBar = (m_src.m_transmitValue + m_src.m_nextTransmitValue) / subject.scienceCap;
            m_transBtnPerc = (transmitValue >= 0.1f ? m_src.m_scienceData.transmitValue * 100 : 0.0f).ToString("F0") + "%"; //if transmit sci is 0pts, then % is 0
            m_labBtnData = "+" + m_src.m_scienceData.labValue.ToString("F0");
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
            Debug.Log("GA GUIView::OnAwake");
            GameEvents.onHideUI.Add(HideUI);
            GameEvents.onShowUI.Add(ShowUI);
        }

        public void OnDestroy()
        {
            GameEvents.onHideUI.Remove(HideUI);
            GameEvents.onShowUI.Remove(ShowUI);
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
        public GUIStyle m_styleDiscardButton;
        public GUIStyle m_styleTransmitButton;
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
        public float m_progressBarWidth = 130; //stolen from ExperimentsResultDialog
        public float m_rightSideWidth = 60;//stolen from ExperimentsResultDialog
        GUISkin m_dlgSkin;

        //GUI state
        public bool m_closeBtn = false;
        public bool m_discardBtn = false;
        public bool m_moveBtn = false;
        public bool m_labBtn = false;
        public bool m_transmitBtn = false;
        public bool m_moveBtnEnabled = false;
        public bool m_labBtnEnabled = false;
        public bool m_transmitBtnEnabled = false;

        public Vector2 m_scrollPos;
        public Texture2D m_dataIcon = null;
        public Texture2D m_scienceIcon = null;
        public GUIStyle m_closeBtnStyle = new GUIStyle();
        public GUIStyle m_moveBtnStyle = new GUIStyle();

        //returns empty string on success, error string on failure
        public string Initialize()
        {
            Debug.Log("GA GUIView::Initialize");
            string errorMsg = string.Empty;

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
            m_styleDiscardButton = m_dlgSkin.GetStyle("discard button");
            m_styleTransmitButton = m_dlgSkin.GetStyle("transmit button");
            m_styleRfIcons = m_dlgSkin.GetStyle("icons");
            m_styleRfText = m_dlgSkin.GetStyle("iconstext");
            m_styleRfBackground = m_dlgSkin.GetStyle("resultfield");
            m_stylePrgBarBG = m_dlgSkin.GetStyle("progressBarBG");
            m_stylePrgBarDarkGreen = m_dlgSkin.GetStyle("progressBarFill");
            m_stylePrgBarLightGreen = m_dlgSkin.GetStyle("progressBarFill2");
            m_stylePrgBarDarkBlue = m_dlgSkin.GetStyle("progressBarFill3");
            m_stylePrgBarLightBlue = m_dlgSkin.GetStyle("progressBarFill4");
            m_styleLabButton = m_dlgSkin.GetStyle("lab button");
            m_styleResetButton = m_dlgSkin.GetStyle("reset button");

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

            return errorMsg;
        }

        //HACKJEFFGIFFEN
        public List<ViewPage> m_copyDataPages = new List<ViewPage>();
        public void OnGUI()
        {
            if (m_showUI)
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
//                LayoutListToggles();
                LayoutListFields();

            }
            GUILayout.EndArea();

            GUILayout.BeginArea(m_infoPaneRect/*, m_dlgSkin.window*/);
            {
                GUILayout.BeginVertical();
                {
                    LayoutTitleBar();

                    GUILayout.BeginHorizontal();
                    {
                        //Main left column
                        LayoutBodySingle();

                        //Rightside button column
                        LayoutActionButtons();

                    }
                    GUILayout.EndHorizontal();

                }
                GUILayout.EndVertical();

            }
            GUILayout.EndArea();

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
                foreach (ViewPage pg in m_copyDataPages)
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

        public void LayoutTitleBar()
        {
            GUILayout.BeginHorizontal();
            {
                ViewPage page = m_copyDataPages.First();
                GUILayout.Label(page.title);
                m_closeBtn = GUILayout.Button("", m_closeBtnStyle);

            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4); //HACKJEFFGIFFEN to align with the list pane's top
        }

        public void LayoutActionButtons()
        {
            GUILayout.BeginVertical(GUILayout.Width(m_rightSideWidth));
            {
                ViewPage pg = m_copyDataPages.First(); //HACKJEFFGIFFEN single mode
                m_discardBtn = GUILayout.Button("", m_styleDiscardButton);
                Color oldColor = GUI.color;
                GUI.color = m_moveBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_moveBtn = GUILayout.Button("", m_moveBtnStyle);
                GUI.color = m_labBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_labBtn = GUILayout.Button(pg.m_labBtnData, m_styleLabButton);
                GUI.color = m_transmitBtnEnabled ? Color.white : Color.grey; //HACKJEFFGIFFEN crap, need a state
                m_transmitBtn = GUILayout.Button(pg.m_transBtnPerc, m_styleTransmitButton);
                GUI.color = oldColor;

            }
            GUILayout.EndVertical();
        }

        //TODOJEFFGIFFEN encapsulate further into a pane display?
        public void LayoutBodySingle()
        {
            //GUILayout.BeginVertical(GUILayout.Width(m_leftColumnWidth)); //width of left column from orig dialog
            GUILayout.BeginVertical();
            {
                //HACKJEFFGIFFEN m_selectedPages.First();
                ViewPage curPg = m_copyDataPages.First();
                //the skin's box GUIStyle already has the green text and nice top left align
                GUILayout.Box(curPg.m_boxText);

                //HACKJEFFGIFFEN
                //if (singlehaxxxx)
                {
                    LayoutInfoField(curPg);
                    LayoutRecoverScienceBarField(curPg);
                    LayoutTransmitScienceBarField(curPg);
                }

            }
            GUILayout.EndVertical();
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

                //HACKJEFFGIFFEN 
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

        public void Update()
        { }
    }
}
