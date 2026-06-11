using System.Windows.Forms;
using Stone_Application.IPC;

namespace Stone_Application.Forms
{
    partial class FormSettings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer m_components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool p_disposing)
        {
            if (p_disposing && (m_components != null))
            {
                m_components.Dispose();
            }
            base.Dispose(p_disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.m_layoutSettings = new System.Windows.Forms.TableLayoutPanel();
            this.m_groupBoxPLC = new System.Windows.Forms.GroupBox();
            this.m_comboBoxPort = new System.Windows.Forms.ComboBox();
            this.m_buttonModbus = new System.Windows.Forms.Button();
            this.m_groupBoxCamera = new System.Windows.Forms.GroupBox();
            this.m_buttonCamera = new System.Windows.Forms.Button();
            this.m_groupBoxAI = new System.Windows.Forms.GroupBox();
            this.m_buttonAI = new System.Windows.Forms.Button();
            this.m_layoutSettings.SuspendLayout();
            this.m_groupBoxPLC.SuspendLayout();
            this.m_groupBoxCamera.SuspendLayout();
            this.m_groupBoxAI.SuspendLayout();
            this.SuspendLayout();
            // 
            // layoutSettings
            // 
            this.m_layoutSettings.ColumnCount = 1;
            this.m_layoutSettings.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.m_layoutSettings.Controls.Add(this.m_groupBoxPLC, 0, 0);
            this.m_layoutSettings.Controls.Add(this.m_groupBoxCamera, 0, 1);
            this.m_layoutSettings.Controls.Add(this.m_groupBoxAI, 0, 2);
            this.m_layoutSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_layoutSettings.Location = new System.Drawing.Point(12, 12);
            this.m_layoutSettings.Name = "layoutSettings";
            this.m_layoutSettings.RowCount = 3;
            this.m_layoutSettings.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.m_layoutSettings.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.m_layoutSettings.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.m_layoutSettings.Size = new System.Drawing.Size(1056, 776);
            this.m_layoutSettings.TabIndex = 0;
            // 
            // groupBoxPLC
            // 
            this.m_groupBoxPLC.Controls.Add(this.m_comboBoxPort);
            this.m_groupBoxPLC.Controls.Add(this.m_buttonModbus);
            this.m_groupBoxPLC.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxPLC.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_groupBoxPLC.Location = new System.Drawing.Point(12, 12);
            this.m_groupBoxPLC.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxPLC.Name = "groupBoxPLC";
            this.m_groupBoxPLC.Size = new System.Drawing.Size(1032, 364);
            this.m_groupBoxPLC.TabIndex = 0;
            this.m_groupBoxPLC.TabStop = false;
            this.m_groupBoxPLC.Text = "PLC Connection";
            // 
            // comboBoxPort
            // 
            this.m_comboBoxPort.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_comboBoxPort.Font = new System.Drawing.Font("Nirmala UI", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_comboBoxPort.FormattingEnabled = true;
            this.m_comboBoxPort.Location = new System.Drawing.Point(24, 58);
            this.m_comboBoxPort.Name = "comboBoxPort";
            this.m_comboBoxPort.Size = new System.Drawing.Size(984, 25);
            this.m_comboBoxPort.TabIndex = 1;
            this.m_comboBoxPort.Click += new System.EventHandler(this.comboBoxPortClick);
            // 
            // buttonModbus
            // 
            this.m_buttonModbus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonModbus.ForeColor = System.Drawing.SystemColors.ControlText;
            this.m_buttonModbus.Location = new System.Drawing.Point(821, 302);
            this.m_buttonModbus.Name = "buttonModbus";
            this.m_buttonModbus.Size = new System.Drawing.Size(187, 46);
            this.m_buttonModbus.TabIndex = 0;
            this.m_buttonModbus.Text = "Connect PLC";
            this.m_buttonModbus.UseVisualStyleBackColor = true;
            this.m_buttonModbus.Click += new System.EventHandler(this.buttonPLC_Click);
            // 
            // groupBoxCamera
            // 
            this.m_groupBoxCamera.Controls.Add(this.m_buttonCamera);
            this.m_groupBoxCamera.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxCamera.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.m_groupBoxCamera.Location = new System.Drawing.Point(12, 400);
            this.m_groupBoxCamera.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxCamera.Name = "groupBoxCamera";
            this.m_groupBoxCamera.Size = new System.Drawing.Size(1032, 170);
            this.m_groupBoxCamera.TabIndex = 1;
            this.m_groupBoxCamera.TabStop = false;
            this.m_groupBoxCamera.Text = "Camera Connection";
            // 
            // buttonCamera
            // 
            this.m_buttonCamera.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonCamera.Location = new System.Drawing.Point(821, 109);
            this.m_buttonCamera.Name = "buttonCamera";
            this.m_buttonCamera.Size = new System.Drawing.Size(187, 46);
            this.m_buttonCamera.TabIndex = 3;
            this.m_buttonCamera.Text = "Connect Camera";
            this.m_buttonCamera.UseVisualStyleBackColor = true;
            this.m_buttonCamera.Click += new System.EventHandler(this.buttonCameraClick);
            // 
            // groupBoxAI
            // 
            this.m_groupBoxAI.Controls.Add(this.m_buttonAI);
            this.m_groupBoxAI.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_groupBoxAI.Font = new System.Drawing.Font("Nirmala UI", 10.2F, System.Drawing.FontStyle.Bold);
            this.m_groupBoxAI.Location = new System.Drawing.Point(12, 594);
            this.m_groupBoxAI.Margin = new System.Windows.Forms.Padding(12);
            this.m_groupBoxAI.Name = "groupBoxAI";
            this.m_groupBoxAI.Size = new System.Drawing.Size(1032, 170);
            this.m_groupBoxAI.TabIndex = 2;
            this.m_groupBoxAI.TabStop = false;
            this.m_groupBoxAI.Text = "AI Initialization";
            // 
            // buttonAI
            // 
            this.m_buttonAI.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonAI.ForeColor = System.Drawing.SystemColors.ButtonShadow;
            this.m_buttonAI.Location = new System.Drawing.Point(821, 109);
            this.m_buttonAI.Name = "buttonAI";
            this.m_buttonAI.Size = new System.Drawing.Size(187, 46);
            this.m_buttonAI.TabIndex = 4;
            this.m_buttonAI.Text = "Connect AI";
            this.m_buttonAI.UseVisualStyleBackColor = true;
            this.m_buttonAI.Click += new System.EventHandler(this.buttonAIClick);
            // 
            // FormSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1080, 800);
            this.Controls.Add(this.m_layoutSettings);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Padding = new System.Windows.Forms.Padding(12);
            this.Name = "FormSettings";
            this.Text = "FormSettings";
            this.m_layoutSettings.ResumeLayout(false);
            this.m_groupBoxPLC.ResumeLayout(false);
            this.m_groupBoxCamera.ResumeLayout(false);
            this.m_groupBoxAI.ResumeLayout(false);
            this.ResumeLayout(false);

        }
        public static void CleanupAllResources()
        {
            // Dispose IPC resources
            IPCServices.IPCCleanUp();

            // Dispose camera if needed
            if (Common.camera != null)
            {
                Common.camera.cameraClose();
                Common.camera = null;
            }

            // Dispose Modbus client if needed
            if (Common.modbusClient != null)
            {
                Common.modbusClient.Disconnect();
                Common.modbusClient = null;
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs p_e)
        {
            CleanupAllResources();
            base.OnFormClosing(p_e);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel m_layoutSettings;
        private System.Windows.Forms.GroupBox m_groupBoxPLC;
        private System.Windows.Forms.GroupBox m_groupBoxCamera;
        private System.Windows.Forms.GroupBox m_groupBoxAI;
        private System.Windows.Forms.Button m_buttonModbus;
        private System.Windows.Forms.Button m_buttonCamera;
        private System.Windows.Forms.Button m_buttonAI;
        private System.Windows.Forms.ComboBox m_comboBoxPort;
    }
}
