﻿using FTD2XX_NET;
using System.Drawing;

namespace ErgoLux;

partial class FrmMain
{
    private void Exit_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    private void Save_CheckedChanged(object sender, EventArgs e)
    {
        this.mnuMainFrm_File_Save.Enabled = toolStripMain_Save.Checked;
    }

    private void Save_Click(object sender, EventArgs e)
    {
        DialogResult result;
        string filePath;

        // Exit if no data has been received or the matrices are still un-initialized
        if (_nPoints == 0 || _plotData == null)
        {
            using (new CenterWinDialog(this))
            {
                MessageBox.Show(StringsRM.GetString("strMsgBoxNoData", _sett.AppCulture) ?? "There is no data available to be saved.",
                    StringsRM.GetString("strMsgBoxNoDataTitle", _sett.AppCulture) ?? "No data",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        // Displays a SaveFileDialog, so the user can save the data into a file  
        SaveFileDialog SaveDlg = new()
        {
            DefaultExt = "*.elux",
            Filter = StringsRM.GetString("strSaveDlgFilter", _sett.AppCulture) ?? "ErgoLux file (*.elux)|*.elux|Text file (*.txt)|*.txt|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
            FilterIndex = 1,
            Title = StringsRM.GetString("strSaveDlgTitle", _sett.AppCulture) ?? "Save illuminance data",
            OverwritePrompt = true,
            InitialDirectory = _sett.RememberFileDialogPath ? _sett.UserSavePath : _sett.DefaultSavePath,
        };

        using (new CenterWinDialog(this))
            result = SaveDlg.ShowDialog(this.Parent);

        // If the file name is not an empty string, call the corresponding routine to save the data into a file.  
        if (result == DialogResult.OK && SaveDlg.FileName != "")
        {
            //Get the path of specified file and store the directory for future calls
            filePath = SaveDlg.FileName;
            if (_sett.RememberFileDialogPath) _sett.UserSavePath = Path.GetDirectoryName(filePath) ?? string.Empty;

            switch (Path.GetExtension(SaveDlg.FileName).ToLower())
            {
                case ".elux":
                    SaveELuxData(SaveDlg.FileName);
                    break;
                case ".txt":
                    SaveTextData(SaveDlg.FileName);
                    break;
                case ".bin":
                    SaveBinaryData(SaveDlg.FileName);
                    break;
                default:
                    SaveDefaultData(SaveDlg.FileName);
                    break;
            }
        }
    }

    private void Open_Click(object sender, EventArgs e)
    {
        DialogResult result;
        string filePath;

        OpenFileDialog OpenDlg = new()
        {
            DefaultExt = "*.elux",
            Filter = StringsRM.GetString("strOpenDlgFilter", _sett.AppCulture) ?? "ErgoLux file (*.elux)|*.elux|Text file (*.txt)|*.txt|Binary file (*.bin)|*.bin|All files (*.*)|*.*",
            FilterIndex = 1,
            Title = StringsRM.GetString("strOpenDlgTitle", _sett.AppCulture) ?? "Open illuminance data",
            InitialDirectory = _sett.RememberFileDialogPath ? _sett.UserOpenPath : _sett.DefaultOpenPath
        };

        using (new CenterWinDialog(this))
            result = OpenDlg.ShowDialog(this);

        // If the file name is not an empty string open it for saving.
        bool readOK = false;
        if (result == DialogResult.OK && OpenDlg.FileName != "")
        {
            //Get the path of specified file and store the directory for future calls
            filePath = OpenDlg.FileName;
            if (_sett.RememberFileDialogPath) _sett.UserOpenPath = Path.GetDirectoryName(filePath) ?? string.Empty;

            // Read the data file in the corresponding format
            switch (Path.GetExtension(OpenDlg.FileName).ToLower())
            {
                case ".elux":
                    readOK = OpenELuxData(OpenDlg.FileName);
                    break;
                case ".txt":
                    readOK = OpenTextData(OpenDlg.FileName);
                    break;
                case ".bin":
                    readOK = OpenBinaryData(OpenDlg.FileName);
                    break;
                default:
                    //OpenDefaultData(OpenDlg.FileName);
                    break;
            }
        }

        if (readOK)
        {
            // Show data into plots
            Plots_FetchData();
        }
    }

    private void Connect_CheckedChanged(object sender, EventArgs e)
    {
        if (toolStripMain_Connect.Checked == true)
        {
            // Although unnecessary because the ToolStripButton should be disabled, make sure the device is already open
            if (myFtdiDevice == null || myFtdiDevice.IsOpen == false)
            {
                using (new CenterWinDialog(this))
                {
                    MessageBox.Show("The device is closed. Please, go to\n'Settings' to open the device.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                toolStripMain_Connect.Checked = false;
                mnuMainFrm_Tools_Connect.Enabled = false;
                return;
            }

            mnuMainFrm_Tools_Disconnect.Enabled = true;
            toolStripMain_Disconnect.Enabled = true;
            toolStripMain_Open.Enabled = false;
            toolStripMain_Save.Enabled = false;
            toolStripMain_Settings.Enabled = false;
            toolStripMain_About.Enabled = false;
            this.statusStripIconExchange.Image = _sett.Icon_Data;
            _reading = true;

            myFtdiDevice.DataReceived += OnDataReceived;
            if (myFtdiDevice.Write(ClassT10.Command_54))
            {
                _timeStart = DateTime.Now;
                m_timer.Start();
            }
        }
        else if (toolStripMain_Connect.Checked == false)
        {
            mnuMainFrm_Tools_Disconnect.Enabled = false;
            toolStripMain_Disconnect.Enabled = false;
            toolStripMain_Open.Enabled = true;
            toolStripMain_Save.Enabled = true;
            toolStripMain_Settings.Enabled = true;
            toolStripMain_About.Enabled = true;
            this.statusStripIconExchange.Image = null;
            _reading = false;
        }
    }

    private void Disconnect_Click(object sender, EventArgs e)
    {
        // Stop receiving data
        m_timer.Stop();
        _timeEnd = DateTime.Now;
        myFtdiDevice.DataReceived -= OnDataReceived;

        // Update GUI
        toolStripMain_Connect.Checked = false;
        Plots_ShowFull();
    }

    private void Settings_Click(object sender, EventArgs e)
    {
        FTDI.FT_STATUS result;

        var frm = new FrmSettings(_sett);
        // Set form icon
        if (File.Exists(_sett.AppPath + @"\images\logo.ico")) frm.Icon = new Icon(_sett.AppPath + @"\images\logo.ico");
        frm.ShowDialog();

        if (frm.DialogResult == DialogResult.OK)
        {
            UpdateUI_Language();

            // If a device is selected, then set up the parameters
            if (_sett.T10_LocationID > 0)
            {
                this.toolStripMain_Connect.Enabled = true;

                if (myFtdiDevice != null && myFtdiDevice.IsOpen)
                    myFtdiDevice.Close();

                myFtdiDevice = new FTDISample();
                result = myFtdiDevice.OpenDevice(location: (uint)_sett.T10_LocationID,
                    baud: _sett.T10_BaudRate,
                    dataBits: _sett.T10_DataBits,
                    stopBits: _sett.T10_StopBits,
                    parity: _sett.T10_Parity,
                    flowControl: _sett.T10_FlowControl,
                    xOn: _sett.T10_CharOn,
                    xOff: _sett.T10_CharOff,
                    readTimeOut: 0,
                    writeTimeOut: 0);

                if (result == FTDI.FT_STATUS.FT_OK)
                {
                    // Set the timer interval according to the sampling frecuency
                    m_timer.Interval = 1000 / _sett.T10_Frequency;

                    // Update the status strip with information
                    this.statusStripLabelLocation.Text = (StringsRM.GetString("strStatusLocation", _sett.AppCulture) ?? "Location ID") + $": {_sett.T10_LocationID:X}";
                    this.statusStripLabelType.Text = (StringsRM.GetString("strStatusType", _sett.AppCulture) ?? "Device type") + $": {frm.GetDeviceType}";
                    this.statusStripLabelID.Text = (StringsRM.GetString("strStatusID", _sett.AppCulture) ?? "Device ID") + $": {frm.GetDeviceID}";
                    this.statusStripIconOpen.Image = _sett.Icon_Open;

                    InitializeStatusStripLabelsStatus();
                    InitializeArrays();     // Initialize the arrays containing the data
                    Plots_Clear();          // First, clear all data (if any) in the plots
                    Plots_DataBinding();    // Bind the arrays to the plots
                    Plots_ShowLegends();    // Show the legends in the picture boxes
                }
                else
                {
                    this.statusStripIconOpen.Image = _sett.Icon_Close;
                    using (new CenterWinDialog(this))
                    {
                        MessageBox.Show(StringsRM.GetString("strMsgBoxErrorOpenDevice", _sett.AppCulture) ?? "Could not open the device",
                            StringsRM.GetString("strMsgBoxErrorOpenDeviceTitle", _sett.AppCulture) ?? "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }

            } // End _sett.T10_LocationID
            else
            {
                InitializeStatusStripLabelsStatus();

                if (_plotData != null && _plotRadar != null && _plotRadialGauge != null)
                {
                    Plots_FetchData();
                }
            }

        }   // End DialogResult.OK

    }

    private void About_Click(object sender, EventArgs e)
    {
        var frm = new FrmAbout();
        frm.ShowDialog();
    }
  
    private void statusStripLabelPlots_CheckedChanged(object sender, EventArgs e)
    {
        if (sender is ToolStripStatusLabelEx)
        {
            var label = sender as ToolStripStatusLabelEx;

            // Change the text color
            if (label.Checked)
                label.ForeColor = Color.Black;
            else
                label.ForeColor = Color.LightGray;
        }
    }

    private void statusStripLabelPlots_Click(object sender, EventArgs e)
    {
        if (sender is ToolStripStatusLabelEx)
        {
            var label = sender as ToolStripStatusLabelEx;
            label.Checked = !label.Checked;

            // Update the settings
            switch (label.Text)
            {
                case "W":
                    _sett.Plot_ShowRawData = label.Checked;
                    mnuMainFrm_View_Raw.Checked = label.Checked;
                    break;
                case "D":
                    _sett.Plot_ShowDistribution = label.Checked;
                    mnuMainFrm_View_Distribution.Checked = label.Checked;
                    break;
                case "A":
                    _sett.Plot_ShowAverage = label.Checked;
                    mnuMainFrm_View_Average.Checked = label.Checked;
                    break;
                case "R":
                    _sett.Plot_ShowRatios = label.Checked;
                    mnuMainFrm_View_Ratio.Checked = label.Checked;
                    break;
            }
        }
    }


    private void mnuMainFrm_View_Menu_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Menu.Checked;
        this.mnuMainFrm_View_Menu.Checked = status;
        this.mnuMainFrm.Visible = status;
    }
    private void mnuMainFrm_View_Toolbar_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Toolbar.Checked;
        this.mnuMainFrm_View_Toolbar.Checked = status;
        this.toolStripMain.Visible = status;
    }
    private void mnuMainFrm_View_Raw_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Raw.Checked;
        this.mnuMainFrm_View_Raw.Checked = status;
        this.statusStripLabelRaw.Checked = status;
        _sett.Plot_ShowRawData = status;
    }
    private void mnuMainFrm_View_Radial_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Distribution.Checked;
        this.mnuMainFrm_View_Distribution.Checked = status;
        this.statusStripLabelRadar.Checked = status;
        _sett.Plot_ShowDistribution = status;
    }
    private void mnuMainFrm_View_Average_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Average.Checked;
        this.mnuMainFrm_View_Average.Checked = status;
        this.statusStripLabelMax.Checked = status;
        _sett.Plot_ShowAverage = status;
    }
    private void mnuMainFrm_View_Ratio_Click(object sender, EventArgs e)
    {
        bool status = !this.mnuMainFrm_View_Ratio.Checked;
        this.mnuMainFrm_View_Ratio.Checked = status;
        this.statusStripLabelRatio.Checked = status;
        _sett.Plot_ShowRatios = status;
    }
    private void mnuMainFrm_Tools_Connect_Click(object sender, EventArgs e)
    {
        bool status = !this.toolStripMain_Connect.Checked;
        this.mnuMainFrm_Tools_Connect.Checked = status;
        this.toolStripMain_Connect.Checked = status;
    }

}

