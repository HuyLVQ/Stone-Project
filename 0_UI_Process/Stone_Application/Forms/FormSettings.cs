using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyModbus;
using Stone_Application.CameraClass;
using Stone_Application;
using System.Diagnostics;
using Stone_Application.Infrastructure;

namespace Stone_Application.Forms
{
    public partial class FormSettings : Form
    {
        private MainForm m_mainForm;
        public FormSettings(MainForm p_mainForm)
        {
            InitializeComponent();
            this.m_mainForm = p_mainForm;

            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
        }


        private void comboBoxPortClick(object p_sender, EventArgs p_e)
        {
            m_comboBoxPort.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            m_comboBoxPort.Items.AddRange(ports);
            if (m_comboBoxPort.Items.Count > 1)
            {
                m_comboBoxPort.SelectedItem = ports[0];
            }
        }

        private void buttonPLC_Click(object p_sender, EventArgs p_e)
        {
            Common.modbusClient = new ModbusClient(m_comboBoxPort.SelectedItem.ToString());
            Common.modbusClient.UnitIdentifier = 1;
            Common.modbusClient.Baudrate = 115200;
            Common.modbusClient.Parity = System.IO.Ports.Parity.None;
            Common.modbusClient.StopBits = System.IO.Ports.StopBits.One;
            Common.modbusClient.ConnectionTimeout = 500;
            Common.modbusClient.NumberOfRetries = 3;

            try
            {
                this.m_buttonModbus.Enabled = false;
                this.m_buttonModbus.ForeColor = System.Drawing.SystemColors.ButtonShadow;
                Common.modbusClient.Connect();

                this.m_buttonModbus.Enabled = true;
                this.m_buttonModbus.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;

                try
                {
                    int[] test = Common.modbusClient.ReadHoldingRegisters(0, 1);
                    MessageBox.Show(this, "Modbus Client successfully connected", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.m_buttonModbus.Enabled = false;
                    this.m_buttonModbus.ForeColor = System.Drawing.SystemColors.ButtonShadow;

                    if (this.m_buttonCamera.Enabled == false)
                    {
                        this.m_buttonAI.Enabled = true;
                        this.m_buttonAI.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
                    }
                }
                catch (Exception ex)
                {
                    Common.modbusClient.Disconnect();
                    MessageBox.Show(this, "Modbus connection failed after retries", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.m_buttonModbus.Enabled = true;
                    this.m_buttonModbus.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not connect to Modbus Client \n" + "Error Code: " + (string)ex.Message + "\nPlease select another ModBus configuration", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                this.Enabled = true;
                this.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
                return;
            }
        }

        private void buttonCameraClick(object p_sender, EventArgs p_e)
        {
            Common.camera = BaslerCamera.getInstance();
            if (Common.camera != null)
            {
                MessageBox.Show(this, "Camera successfully initialized", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.m_buttonCamera.Enabled = false;
                this.m_buttonCamera.ForeColor = System.Drawing.SystemColors.ButtonShadow;
                if (this.m_buttonModbus.Enabled == false)
                {
                    this.m_buttonAI.Enabled = true;
                    this.m_buttonAI.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
                }
            }
        }

        private void buttonAIClick(object p_sender, EventArgs p_e)
        {
            // Implement AI click event
            AIHelper.initializeAI();
            AIHelper.warmUpAI();


            this.Invoke((Action)(() =>
            {
                MessageBox.Show(this, "AI initialized sucessfully.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.m_buttonAI.Enabled = false;
                this.m_buttonAI.ForeColor = System.Drawing.SystemColors.ButtonShadow;
            }));

            Common.s_stopWatchMain.Start();


            MultiThread.thread1Work();
            MultiThread.thread2Work();

        }
    }
}
