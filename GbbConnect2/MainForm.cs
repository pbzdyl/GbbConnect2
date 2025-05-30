using System.Diagnostics.Eventing.Reader;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using GbbEngine2.Configuration;
using GbbEngine2.Drivers;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace GbbConnect2
{
    public partial class MainForm : Form, GbbLibSmall.IOurLog
    {


        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                Program.Parameters_Load();

                DateTime td = DateTime.Today;

                // dictionaries
                this.inverterInfoBindingSource.DataSource = GbbEngine2.Drivers.DriverInfo.OurGetDriveInfos();


                // parameters
                this.ParametersBindingSource.DataSource = Program.Parameters;


                //this.ImportVRM_FromDate_dateTimePicker.Value = DateTime.Today.AddDays(-28);
                //this.ImportVRM_ToDate_dateTimePicker.Value = DateTime.Today.AddDays(-1);

                //GbbLib.Application.StatusBar.OnStatusBarMessage += (arg) => { this.toolStripStatusLabel1.Text = arg.sInfo; };


                // About
                if (td.Year == 2023)
                    this.About_label2.Text = this.About_label2.Text + " 2023";
                else
                    this.About_label2.Text = this.About_label2.Text + " 2023 - {DateTime.Today.Year}";

                this.Version_label.Text = $"Version: {GbbEngine2.Configuration.Parameters.APP_VERSION}";
            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }

        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            try
            {
                // Autostart Server
                if (Program.Parameters.Server_AutoStart)
                    StartServer_button_Click(sender, e);

            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                Program.Parameters_Save();
            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }

        }

        // ======================================
        // Operations
        // ======================================

        private void Save_button_Click(object sender, EventArgs e)
        {
            try
            {
                this.ValidateChildren();
                Program.Parameters_Save();
            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }

        }


        private void Clear_button_Click(object sender, EventArgs e)
        {
            try
            {
                Log_textBox.Clear();

            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }

        }


        // ======================================
        // Plants
        // ======================================

        private void plantsBindingSource_AddingNew(object sender, System.ComponentModel.AddingNewEventArgs e)
        {
            Plant ret = new();

            // calculate next InverterNumber
            ret.Number = 1;
            foreach (var itm in Program.Parameters.Plants)
                if (itm.Number >= ret.Number)
                    ret.Number = itm.Number + 1;

            e.NewObject = ret;
        }

        private void Plants_DataGridView_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            // check new/change row
            if (e.RowIndex >= 0
            && !Plants_DataGridView.Rows[e.RowIndex].IsNewRow
            && e.RowIndex < Program.Parameters.Plants.Count)
                Program.Parameters.Plants[e.RowIndex].OurCheckDataForUI();

        }

        // ======================================
        // Tests
        // ======================================

        private async void ReadRegisters_button_Click(object sender, EventArgs e)
        {
            try
            {
                this.ValidateChildren();

                if (this.plantsBindingSource.Current != null)
                {
                    Plant itm = (Plant)this.plantsBindingSource.Current;


                    if (GbbLibWin.Log.LogMsgBox(this, $"Do you want to start read registers (plant={itm.Name})?", MessageBoxButtons.YesNo, DialogResult.Yes, Microsoft.Extensions.Logging.LogLevel.Information) == DialogResult.Yes)
                    {
                        try
                        {
                            Log($"Plant: {itm.Name}");
                            IDriver? drv = null;
                            switch (itm.DriverNo)
                            {
                                case (int)GbbEngine2.Drivers.DriverInfo.Drivers.i000_SolarmanV5:
                                    {
                                        GbbEngine2.Drivers.SolarmanV5.SolarmanV5Driver sm = new(Program.Parameters, itm.AddressIP, itm.PortNo, itm.SerialNumber, this);
                                        sm.Connect();
                                        drv = sm;
                                    }
                                    break;

                                case (int)GbbEngine2.Drivers.DriverInfo.Drivers.i001_ModbusTCP:
                                    {
                                        GbbEngine2.Drivers.SolarmanV5.ModbusTcpDriver sm = new (Program.Parameters, itm.AddressIP, itm.PortNo, itm.SerialNumber, this);
                                        sm.Connect();
                                        drv = sm;
                                    }
                                    break;

                                case (int)GbbEngine2.Drivers.DriverInfo.Drivers.i999_Random:
                                    drv = new GbbEngine2.Drivers.Random.RandomDriver();
                                    break;

                                default:
                                    throw new ApplicationException("Unknown driver no: " + itm.DriverNo);
                            }
                            try
                            {


                                byte[] answer = { 0, 66 };
                                //driver.WriteMultipleRegister(0, 1, 184, answer);
                                answer = await drv.ReadHoldingRegister(1, (ushort)RegisterNo_numericUpDown.Value, (ushort)RegisterCount_numericUpDown.Value);

                                System.Text.StringBuilder sb = new();
                                for (int i = 0; i < RegisterCount_numericUpDown.Value; i++)
                                {
                                    sb.Append(RegisterNo_numericUpDown.Value + i);
                                    sb.Append('=');
                                    sb.Append(answer[i * 2] * 256 + answer[i * 2 + 1]);
                                    sb.Append(", ");
                                }

                                Log($"Answer: {sb.ToString()}");
                            }
                            finally
                            {
                                drv.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex.Message);
                        }
                        GbbLibWin.Log.LogMsgBox(this, "Done");
                    }
                }
            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }
        }

        private void Search_button_Click(object sender, EventArgs e)
        {
            try
            {
                this.ValidateChildren();
                if (GbbLibWin.Log.LogMsgBox(this, "Do you want to search for SolarmanV5?", MessageBoxButtons.YesNo, DialogResult.Yes, Microsoft.Extensions.Logging.LogLevel.Information) == DialogResult.Yes)
                {

                    Log($"{DateTime.Now}: Start searching");

                    int Counter = 0;
                    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (nic.OperationalStatus == OperationalStatus.Up)
                            foreach (var ua in nic.GetIPProperties().UnicastAddresses)
                            {
                                if (ua.Address.AddressFamily == AddressFamily.InterNetwork)
                                {
                                    Log($"{DateTime.Now}: Search Network: {ua.Address.ToString()} (5sec)");

                                    try
                                    {
                                        var ll = GbbEngine2.Drivers.SolarmanV5.SolarmanV5Driver.OurSearchSolarman(ua.Address);

                                        if (ll.Count == 0)
                                            Log($"{DateTime.Now}: Nothing found...");
                                        else
                                        {
                                            Log($"{DateTime.Now}: ==========================");
                                            Log($"{DateTime.Now}: IpAddress, MAC address, SerialNo");
                                            foreach (var itm in ll)
                                            {
                                                Log($"{DateTime.Now}: {itm}");
                                                Counter++;
                                            }
                                            Log($"{DateTime.Now}: ==========================");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log($"{DateTime.Now}: ERROR: {ex.Message}");
                                    }
                                }


                            }

                    }

                    Log($"{DateTime.Now}: Done. Found {Counter} inverters.");




                }

            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }
        }

        // ======================================
        // Log
        // ======================================

        private object LogSync = new();

        private void Log(string message)
        {
            this.Log_textBox.AppendText($"{DateTime.Now}: {message}\r\n");
        }

        // log from engine
        public void OurLog(GbbLibSmall.LogLevel LogLevel, string message, params object?[] args)
        {
            var nw = DateTime.Now;

            if (args.Length > 0)
                message = string.Format(message, args);

            // add time
            string msg;
            if (LogLevel == GbbLibSmall.LogLevel.Error)
                msg = $"{nw}: ERROR: {message}\r\n";
            else
                msg = $"{nw}: {message}\r\n";

            lock (LogSync)
            {

                // directory for log
                string FileName = Path.Combine(GbbEngine2.Configuration.Parameters.OurGetUserBaseDirectory(), "Log");
                Directory.CreateDirectory(FileName);

                // filename of log
                FileName = Path.Combine(FileName, $"{nw:yyyy-MM-dd}.txt");
                File.AppendAllText(FileName, msg);
            }



            // log also to Log_textbox
            if (this.InvokeRequired)
                this.Invoke(new Action(() =>
                {
                    if (Log_textBox.Text.Length > 50000)
                        Log_textBox.Text = Log_textBox.Text.Substring(45000) + msg;
                    else
                        Log_textBox.AppendText(msg);
                }));
            else
                Log_textBox.AppendText(msg);


        }

        // ======================================
        // Server
        // ======================================
        private GbbEngine2.Server.JobManager? JobManeger;

        private void StartServer_button_Click(object sender, EventArgs e)
        {
            try
            {
                this.ValidateChildren();
                this.tabControl1.SelectedTab = this.Log_tabPage2;

                if (JobManeger == null)
                {
                    JobManeger = new();
                    JobManeger.OurStartJobs(Program.Parameters, this);
                }
                this.StartServer_button.Enabled = false;
                this.StopServer_button.Enabled = true;

            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }

        }

        private void StopServer_button_Click(object sender, EventArgs e)
        {
            try
            {
                if (JobManeger != null)
                {
                    JobManeger.OurStopJobs(Program.Parameters);
                    JobManeger = null;
                }
                this.StartServer_button.Enabled = true;
                this.StopServer_button.Enabled = false;

            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }
        }

        // ======================================
        // Prevent sleep
        // ======================================

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                // Prevent Idle-to-Sleep (monitor not affected)
                SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_AWAYMODE_REQUIRED);
            }
            catch (Exception ex)
            {
                GbbLibWin.Log.ErrMsgBox(this, ex);
            }
        }

        public void ChangeParameterProperty(Action action)
        {
            this.Invoke(action);
        }
    }
}